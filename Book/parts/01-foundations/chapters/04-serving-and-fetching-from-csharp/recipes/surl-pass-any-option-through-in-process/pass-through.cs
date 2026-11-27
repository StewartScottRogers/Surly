// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Gives an in-process Surl.Api server a surl option SurlServerOptions has no property for, in
// surl's own words, through AdditionalArguments.
// Run: dotnet run pass-through.cs [-- <case>]
//   (no argument)  --max-request-head 1000: a request with a long header is refused
//   without        no AdditionalArguments: the same request is served
//   put            AllowUploads = true and no AdditionalArguments: an upload
//   override       AllowUploads = true, then --no-allow-uploads among the arguments
//   host-option    --log-file, an option that belongs to a host
//   bad-value      --max-request-head abc
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "head-limit";

// The server as properties: uploads on, and an account to upload as.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, AllowUploads = true };
options.Accounts.Add(new SurlAccount("alice", "secret"));

// The rest in surl's own words, read after the properties.
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
string[] extra = mode switch
{
    "without" or "put" => [],
    "override" => ["--no-allow-uploads"],
    "host-option" => ["--log-file", "surl.log"],
    "bad-value" => ["--max-request-head", "abc"],
    _ => ["--max-request-head", "1000"],
};
foreach (string argument in extra)
{
    options.AdditionalArguments.Add(argument);
}

Console.WriteLine($"AdditionalArguments: {string.Join(" ", extra)}");
SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (ArgumentException refused)
{
    Console.WriteLine($"ArgumentException: {refused.Message}");
    return 1;
}
catch (SurlServerStartException refused)
{
    Console.WriteLine($"SurlServerStartException {refused.ExitCode}: {refused.Lines[0]}");
    return 1;
}

await using (server)
await using (CurlClient client = CurlClient.Create())
{
    var login = new CurlAuthenticationOptions { UserName = "alice", Password = "secret", HttpAuthentication = HttpAuthenticationMethods.Digest };
    if (mode is "put" or "override")
    {
        // AllowUploads says yes; --no-allow-uploads, read after it, says no.
        Console.WriteLine($"PUT /new.txt: {await Send(client, new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}new.txt") { Authentication = login, Body = "new\n"u8.ToArray() })}");
        return 0;
    }

    // One ordinary request, then one whose head carries a 2000-character header.
    Console.WriteLine($"short head: {await Send(client, new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt") { Authentication = login })}");
    Console.WriteLine($"long head: {await Send(client, new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt") { Authentication = login, Headers = [new("X-Trace", new string('x', 2000))] })}");
}

return 0;

// One request; its status line, with the body read and discarded.
static async Task<string> Send(CurlClient client, CurlRequest request)
{
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
    return $"{response.StatusCode} {response.ReasonPhrase}";
}
