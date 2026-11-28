// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Puts one in-process Surl.Api server back between tests: a snapshot after seeding, a restore
// after each test, a reset after the last.
// Run: dotnet run between-tests.cs [-- other-server|on-disk]
//   (no argument)  seed, snapshot, let a client change things, restore, reset
//   other-server   restore the snapshot into a second server
//   on-disk        reset and snapshot a server whose files are on disk
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "tests";

if (mode == "on-disk")
{
    return await OnDisk();
}

// The server: uploads on, one account to upload as, one file given at start.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, AllowUploads = true };
options.Accounts.Add(new SurlAccount("alice", "secret"));
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);

// The class fixture: seed what every test needs, then snapshot it.
await server.Files.PutAsync("/user.json", "{\"name\":\"alice\"}\n"u8.ToArray());
SurlSnapshot clean = await server.SnapshotAsync();
await PrintFiles("seeded");

if (mode == "other-server")
{
    await using var other = await SurlServer.StartAsync(new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } });
    try
    {
        await other.RestoreAsync(clean);
    }
    catch (ArgumentException)
    {
        Console.WriteLine("another server's snapshot: ArgumentException");
        return 1;
    }
}

// One test: its client uploads a new file and overwrites the seeded one.
await using (CurlClient client = CurlClient.Create())
{
    Console.WriteLine($"PUT /report.csv: {await Put(client, "report.csv", "a,b\n"u8.ToArray())}");
    Console.WriteLine($"PUT /user.json: {await Put(client, "user.json", "{}\n"u8.ToArray())}");
}

await PrintFiles("after the test");

// Before the next test: back to the snapshot.
await server.RestoreAsync(clean);
await PrintFiles("restored");

// After the last test: back to how the server started.
await server.ResetAsync();
await PrintFiles("reset");
return 0;

// The served root as the test sees it, read without a client: each file and its size.
async Task PrintFiles(string when)
{
    SurlFolderPage page = await server.Files.ListAsync("/");
    Console.WriteLine($"{when}: {string.Join(", ", page.Entries.Select(e => $"{e.Name} {e.ByteCount}"))}");
}

// An upload as the code under test would make it: PUT, logged in with Digest.
async Task<int> Put(CurlClient client, string path, byte[] contents)
{
    CurlRequest put = new(HttpMethod.Put, $"{server.BoundUrl}{path}")
    {
        Body = contents,
        Authentication = new CurlAuthenticationOptions
        {
            UserName = "alice",
            Password = "secret",
            HttpAuthentication = HttpAuthenticationMethods.Digest,
        },
    };
    CurlResponse response = await client.SendAsync(put, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
    return response.StatusCode;
}

// A server whose files are in a real directory refuses to reset or snapshot them.
static async Task<int> OnDisk()
{
    DirectoryInfo data = Directory.CreateTempSubdirectory();
    await using (var server = await SurlServer.StartAsync(
        new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, DataDirectory = data.FullName }))
    {
        await TryIt("ResetAsync", () => server.ResetAsync());
        await TryIt("SnapshotAsync", () => server.SnapshotAsync());
    }

    data.Delete(recursive: true);
    return 0;
}

static async Task TryIt(string name, Func<Task> call)
{
    try
    {
        await call();
        Console.WriteLine($"{name}: done");
    }
    catch (InvalidOperationException)
    {
        Console.WriteLine($"{name}: InvalidOperationException");
    }
}
