// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves a folder as the SMB share "reports" with per-account grants using Surl.Api, and tries the
// accounts with Curl.Api: the reader's upload is refused, the writer's kept.
// Run: dotnet run program.cs -- grants    (also: no-uploads, group)
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Surl.Api;

// grants: the recipe. no-uploads: the same grants without AllowUploads. group: rw goes to a group.
string mode = args[0];

// The folder behind the share, and a second folder that no grant names.
string root = Path.Combine(Path.GetTempPath(), "smb-grants-" + Guid.NewGuid().ToString("N"));
string reports = Path.Combine(root, "reports");
string other = Path.Combine(root, "other");
Directory.CreateDirectory(reports);
Directory.CreateDirectory(other);
File.WriteAllText(Path.Combine(reports, "q1.txt"), "q1 numbers\n");
File.WriteAllText(Path.Combine(other, "o.txt"), "open to all\n");

var options = new SurlServerOptions
{
    ListenUrls = { "smb://127.0.0.1:0" },
    Shares = { "reports=" + reports, "other=" + other },
    Accounts = { new SurlAccount("reader", "r3ad"), new SurlAccount("writer", "wr1te"), new SurlAccount("carol", "c4rol") },
    AuthenticationMethods = ["ntlmv1"],
    AllowUploads = mode != "no-uploads",
};
if (mode == "group")
{
    options.Groups.Add("editors=writer");
    options.ShareAccess.Add("reports=reader:r,@editors:rw");
}
else
{
    options.ShareAccess.Add("reports=reader:r,writer:rw");
}

await using var server = await SurlServer.StartAsync(options);
string smb = server.BoundUrls[0].ToString();

await using CurlClient client = CurlClient.Create();

if (mode == "grants")
{
    Console.WriteLine($"writer uploads: {await Send("writer", "wr1te", "reports/new.txt", "q2 numbers\n")}");
    Console.WriteLine($"on disk: {OnDisk(reports, "new.txt")}");
    Console.WriteLine($"reader reads: {await Send("reader", "r3ad", "reports/q1.txt")}");
    Console.WriteLine($"reader reads new: {await Send("reader", "r3ad", "reports/new.txt")}");
    Console.WriteLine($"reader uploads: {await Send("reader", "r3ad", "reports/draft.txt", "unfinished\n")}");
    Console.WriteLine($"on disk: {OnDisk(reports, "draft.txt")}");
    Console.WriteLine($"carol reads: {await Send("carol", "c4rol", "reports/q1.txt")}");
    Console.WriteLine($"carol uploads: {await Send("carol", "c4rol", "reports/carol.txt", "x\n")}");
    Console.WriteLine($"carol reads other: {await Send("carol", "c4rol", "other/o.txt")}");
    Console.WriteLine($"carol uploads other: {await Send("carol", "c4rol", "other/draft.txt", "unfinished\n")}");
    Console.WriteLine($"on disk: {OnDisk(other, "draft.txt")}");
    Console.WriteLine($"wrong password: {await Send("writer", "wrong", "reports/q1.txt")}");
}
else
{
    Console.WriteLine($"writer uploads: {await Send("writer", "wr1te", "reports/new.txt", "q2 numbers\n")}");
    Console.WriteLine($"reader uploads: {await Send("reader", "r3ad", "reports/draft.txt", "unfinished\n")}");
    Console.WriteLine($"reader reads: {await Send("reader", "r3ad", "reports/q1.txt")}");
}

return 0;

static string OnDisk(string folder, string name) =>
    File.Exists(Path.Combine(folder, name)) ? File.ReadAllText(Path.Combine(folder, name)).Trim() : "no file";

// One request as one account: a body makes it an upload. The answer is the body read back, "stored"
// for an upload that threw nothing (SMB has no status), or the CurlException's code.
async Task<string> Send(string user, string password, string path, string? body = null)
{
    var request = new CurlRequest(body is null ? HttpMethod.Get : HttpMethod.Put, smb + path)
    {
        Authentication = new CurlAuthenticationOptions { UserName = user, Password = password },
    };
    if (body is not null) request = request with { Body = System.Text.Encoding.UTF8.GetBytes(body) };
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream stream = response.Body;
        string text = (await new StreamReader(stream).ReadToEndAsync()).Trim();
        return body is null ? text : "stored";
    }
    catch (CurlException failure)
    {
        return $"CurlException {(int)failure.Code} {failure.Code}";
    }
}
