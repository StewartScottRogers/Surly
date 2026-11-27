// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# file-based app: `dotnet run batch.cs -- SCENARIO`. host_key and host_key.pub come from setup: in recipe.yml.
//   all (the default), wrong-pin, missing, short, repaired, uploads-off, no-listing
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Security.Cryptography;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Protocols;
using Surl.Api;

string scenario = args.Length > 0 ? args[0] : "all";

// The batch: four files, each with a name and bytes. Sizes are what the comparison checks.
var batch = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
{
    ["notes.txt"] = "release notes\n"u8.ToArray(),
    ["config.ini"] = "[app]\nname = demo\n"u8.ToArray(),
    ["data.csv"] = "id,name\n1,a\n2,b\n"u8.ToArray(),
    ["blob.bin"] = new byte[1500],
};

// The server: host key from setup, one account. Uploads and listings are on unless the scenario turns one off.
var options = new SurlServerOptions
{
    ListenUrls = { "sftp://127.0.0.1:0" },
    Accounts = { new SurlAccount("alice", "s3cret") },
    AllowUploads = scenario != "uploads-off",
    ListDirectories = scenario != "no-listing",
};
options.HostKeyFiles.Add("host_key");
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

// The pin: the SHA-256 of the host key's blob, in base64 without padding, as ssh-keygen -l writes it.
// It is worked out from host_key.pub, so a different key would not match it.
byte[] blob = Convert.FromBase64String(File.ReadAllText("host_key.pub").Split(' ')[1]);
string pin = Convert.ToBase64String(SHA256.HashData(blob)).TrimEnd('=');
string wrongPin = new string('A', 43);

CurlRequest Request(HttpMethod method, string path, string hostPin) => new(method, $"{server.BoundUrl}{path}")
{
    Authentication = new CurlAuthenticationOptions { UserName = "alice", Password = "s3cret" },
    Ssh = new CurlSshOptions { HostPublicKeySha256 = hostPin },
};

// curl -T --ftp-create-dirs: one file into /inbox/. The answer is "ok" or the code that refused it.
async Task<string> Upload(string name, byte[] bytes, string hostPin)
{
    try
    {
        CurlRequest request = Request(HttpMethod.Put, $"inbox/{name}", hostPin) with
        {
            Body = bytes,
            Ftp = new CurlFtpOptions { CreateMissingDirectories = FtpCreateMissingDirectories.Create },
        };
        await client.SendAsync(request, CancellationToken.None);
        return "ok";
    }
    catch (CurlException e)
    {
        return $"{e.Code} ({(int)e.Code})";
    }
}

// The server's side: the long listing of /inbox/, one "name size" pair for each file.
async Task<Dictionary<string, long>> Listing()
{
    CurlResponse response = await client.SendAsync(Request(HttpMethod.Get, "inbox/", pin), CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();
    return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.TrimEnd('\r').Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .ToDictionary(fields => fields[^1], fields => long.Parse(fields[4]));
}

// Every name and size of the local batch against the listing: what matched, or what did not.
async Task Compare()
{
    Dictionary<string, long> remote;
    try { remote = await Listing(); }
    catch (CurlException e) { Console.WriteLine($"listing refused: {e.Code} ({(int)e.Code})"); return; }

    var problems = new List<string>();
    foreach ((string name, byte[] bytes) in batch)
    {
        if (!remote.TryGetValue(name, out long size)) problems.Add($"missing: {name} ({bytes.Length})");
        else if (size != bytes.Length) problems.Add($"size: {name} local {bytes.Length}, remote {size}");
    }
    problems.AddRange(remote.Keys.Where(name => !batch.ContainsKey(name)).Select(name => $"extra: {name}"));
    if (problems.Count == 0) Console.WriteLine($"all {batch.Count} files match: {string.Join(", ", remote.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key} {p.Value}"))}");
    else problems.ForEach(Console.WriteLine);
}

switch (scenario)
{
    case "all":
        foreach ((string name, byte[] bytes) in batch) await Upload(name, bytes, pin);
        await Compare();
        break;
    case "wrong-pin":
        Console.WriteLine($"upload: {await Upload("notes.txt", batch["notes.txt"], wrongPin)}");
        break;
    case "missing":
        foreach (string name in new[] { "notes.txt", "config.ini", "data.csv" }) await Upload(name, batch[name], pin);
        await Compare();
        break;
    case "short":
        foreach (string name in new[] { "notes.txt", "config.ini", "data.csv" }) await Upload(name, batch[name], pin);
        await Upload("blob.bin", new byte[1000], pin);
        await Compare();
        break;
    case "repaired":
        foreach (string name in new[] { "notes.txt", "config.ini", "data.csv" }) await Upload(name, batch[name], pin);
        await Upload("blob.bin", new byte[1000], pin);
        foreach ((string name, byte[] bytes) in batch) await Upload(name, bytes, pin);
        await Compare();
        break;
    case "uploads-off":
        Console.WriteLine($"upload: {await Upload("notes.txt", batch["notes.txt"], pin)}");
        break;
    case "no-listing":
        Console.WriteLine($"upload: {await Upload("notes.txt", batch["notes.txt"], pin)}");
        await Compare();
        break;
}
return 0;
