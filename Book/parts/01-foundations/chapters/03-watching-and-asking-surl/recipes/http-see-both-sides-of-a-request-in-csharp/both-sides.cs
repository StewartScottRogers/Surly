// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Uploads a file with Curl.Api to a Surl.Api server that refuses uploads, then prints both sides:
// what the client sent and the server answered, from RecordExchanges, and why, from the server's log.
// Run: dotnet run both-sides.cs [-- <case>]
//   refused    log in with Digest; the server was not given AllowUploads (the default)
//   no-login   the same upload with no login
//   allowed    log in with Digest; the server allows uploads
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Output;

string mode = args.Length > 0 ? args[0] : "refused";

// The server: one account, every exchange recorded, and the verbose log kept in memory.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    RecordExchanges = true,
    AllowUploads = mode == "allowed",
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Verbose,
};
options.Accounts.Add(new SurlAccount("alice", "s3cret"));
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
var server = await SurlServer.StartAsync(options);

// The client: a PUT of a small file, with a Digest login unless the case says otherwise.
var request = new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}notes.txt") { Body = "my notes\n"u8.ToArray() };
if (mode != "no-login")
{
    request = request with
    {
        Authentication = new CurlAuthenticationOptions { UserName = "alice", Password = "s3cret", HttpAuthentication = HttpAuthenticationMethods.Digest },
    };
}

await using (CurlClient client = CurlClient.Create())
{
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await response.Body.DisposeAsync();
    Console.WriteLine($"PUT /notes.txt: {response.StatusCode} {response.ReasonPhrase}");
}

// Both sides, from the server's record: each request head received and each status line sent.
await WaitForEndsAsync();
foreach (SurlExchangeRecord exchange in server.Exchanges)
{
    Console.WriteLine($"exchange {exchange.ExchangeId}:");
    foreach (SurlExchangeChunk chunk in exchange.Chunks)
    {
        string[] lines = Encoding.UTF8.GetString(chunk.Bytes.Span).Split("\r\n");
        bool received = chunk.Direction == SurlExchangeDirection.Received;
        if (!lines[0].StartsWith(received ? "PUT " : "HTTP/"))
        {
            continue;   // a body, not a head
        }

        // The headers that explain the outcome: the login the client sent, the methods the server allows.
        IEnumerable<string> telling = lines.Where(l => l.StartsWith("Authorization: ") || l.StartsWith("Allow: "))
            .Select(l => l.StartsWith("Authorization: ") ? l.Split(',')[0] : l);
        Console.WriteLine($"  {(received ? "client sent" : "server sent")}: {string.Join(" | ", telling.Prepend(lines[0]))}");
    }
}

// Why, from the server's verbose log. Read it once the server has stopped.
await server.StopAsync();
foreach (string line in log.ToString().Split('\n').Select(l => l.TrimEnd('\r')))
{
    if (line.Contains("* PUT /notes.txt:") || line.Contains("* Login ") || line.Contains("< Authorization:"))
    {
        Console.WriteLine($"log: {line}");
    }
}

return 0;

// The server records an exchange a moment after the client has gone, so wait (at most 10 s) until
// every exchange it started has ended before reading the record.
async Task WaitForEndsAsync()
{
    var started = new SurlEventQuery { Kind = SurlEventKind.ExchangeStarted };
    var ended = new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded };
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); server.FindEvents(ended).Count < server.FindEvents(started).Count && waited.Elapsed.TotalSeconds < 10;)
    {
        await Task.Delay(100);
    }
}
