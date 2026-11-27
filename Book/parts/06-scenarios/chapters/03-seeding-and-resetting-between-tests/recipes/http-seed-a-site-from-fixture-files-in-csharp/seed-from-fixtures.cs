// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Seeds an in-process server's memory from the fixtures folder, lets a test change the files
// over HTTP and read them over FTP, and shows the fixtures on disk untouched and a reset back
// to the originals.
// Run: dotnet run seed-from-fixtures.cs [-- test|strict|no-uploads|on-disk]
//   test        seed, change over HTTP, read over FTP, check the disk, reset (the default)
//   strict      the same server with AllowAnonymous and AllowUploads left off
//   no-uploads  AllowAnonymous on, AllowUploads left off
//   on-disk     ask for the fixtures folder as DataDirectory and in Files at once
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "test";

// Every file under fixtures/, by its '/' separated path, read into the server's memory.
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0", "ftp://127.0.0.1:0" },
    AllowAnonymous = mode != "strict",
    AllowUploads = mode is not ("strict" or "no-uploads"),
};
foreach (string file in Directory.EnumerateFiles("fixtures", "*", SearchOption.AllDirectories).Order())
{
    options.Files[Path.GetRelativePath("fixtures", file).Replace('\\', '/')] = File.ReadAllBytes(file);
}

if (mode == "on-disk")
{
    options.DataDirectory = "fixtures";
    try
    {
        await SurlServer.StartAsync(options);
    }
    catch (ArgumentException refused)
    {
        Console.WriteLine($"ArgumentException: {refused.Message}");
        return 1;
    }
}

await using var server = await SurlServer.StartAsync(options);
string http = server.BoundUrls[0].ToString(), ftp = server.BoundUrls[1].ToString();
Console.WriteLine($"seeded: {string.Join(", ", options.Files.Keys)}");
await using CurlClient client = CurlClient.Create();

// The test: replace users.json and add orders/2.json over HTTP, then read users.json over FTP.
Console.WriteLine($"HTTP PUT users.json: {await Put(http + "users.json")}");
Console.WriteLine($"HTTP PUT orders/2.json: {await Put(http + "orders/2.json")}");
Console.WriteLine($"FTP GET users.json: {await Get(ftp + "users.json")}");

// The fixtures on disk are as the repository holds them.
Console.WriteLine($"fixtures/users.json on disk: {File.ReadAllText("fixtures/users.json").TrimEnd()}");
Console.WriteLine($"fixtures/orders on disk: {string.Join(", ", Directory.EnumerateFiles("fixtures/orders").Select(Path.GetFileName).Order())}");

// The next test starts from the originals: ResetAsync returns the store to the Files given at start.
await server.ResetAsync();
Console.WriteLine($"after reset, HTTP GET users.json: {await Get(http + "users.json")}");
Console.WriteLine($"after reset, orders/: {string.Join(", ", (await server.Files.ListAsync("/orders")).Entries.Select(e => e.Name))}");
return 0;

// An HTTP PUT as a test makes it, with no login: the status.
async Task<string> Put(string url)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Put, url) { Body = "[]\n"u8.ToArray() }, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
    return response.StatusCode.ToString();
}

// One GET over HTTP or FTP: the body, the status when it is an HTTP error, or curl's error.
async Task<string> Get(string url)
{
    try
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url), CancellationToken.None);
        await using Stream body = response.Body;
        string text = await new StreamReader(body).ReadToEndAsync();
        return response.StatusCode >= 400 ? response.StatusCode.ToString() : text.TrimEnd();
    }
    catch (CurlException failed)
    {
        return $"CurlException {failed.Code}";
    }
}
