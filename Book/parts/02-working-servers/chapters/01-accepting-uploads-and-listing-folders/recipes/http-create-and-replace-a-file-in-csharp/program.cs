// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Uploads a file with PUT, replaces it, and tries a folder that is not there, then reads the
// server's store back with Files.ReadAsync after each answer.
// Run: dotnet run program.cs [-- read-only]   (read-only: the same server with uploads off)
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;

bool uploads = args.Length == 0 || args[0] != "read-only";

// The server: one account, uploads on (unless read-only), and a drafts folder in memory.
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    Accounts = { new SurlAccount("alice", "s3cret") },
    AllowUploads = uploads,
};
options.Files["drafts/outline.txt"] = "an outline\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);

// The client logs in with Digest on every request.
await using CurlClient client = CurlClient.Create();
var login = new CurlAuthenticationOptions { UserName = "alice", Password = "s3cret", HttpAuthentication = HttpAuthenticationMethods.Digest };

await Put("/report.txt", "version one\n");
await Put("/report.txt", "version two\n");
await Put("/archive/report.txt", "version one\n");
await Put("/drafts", "version one\n");
return 0;

// One PUT, its status, then what the store holds at that path.
async Task Put(string path, string text)
{
    var request = new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}{path.TrimStart('/')}")
    {
        Authentication = login,
        Body = System.Text.Encoding.UTF8.GetBytes(text),
    };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await response.Body.DisposeAsync();
    string holds;
    try
    {
        SurlFileContents stored = await server.Files.ReadAsync(path, CancellationToken.None);
        holds = System.Text.Encoding.UTF8.GetString(stored.Bytes.Span).TrimEnd('\n');
    }
    catch (SurlNotFoundException)
    {
        holds = "no file";
    }

    Console.WriteLine($"PUT {path}: {response.StatusCode} {response.ReasonPhrase}; store holds: {holds}");
}
