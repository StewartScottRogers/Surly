// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that the headers a client adds to every request reach the server exactly once each,
// read from the request Surl.Api recorded.
// Run: dotnet run check-headers.cs [-- twice|empty|empty-on-purpose|no-accept]
//   (no argument) two requests, each with the API key and the trace ID once
//   twice         the caller adds the trace ID again, so it arrives twice
//   empty         the API key is given an empty value in Headers, which removes it
//   empty-on-purpose  the API key is named in EmptyHeaders, so it is sent with no value
//   no-accept     the request also removes the Accept header the client adds of its own
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "once";

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);

// The client under test: every request gets the API key and a trace ID.
CurlRequest Build(string path, int n)
{
    List<KeyValuePair<string, string>> headers = [new("X-Trace-Id", $"7f3a9c-{n}")];
    if (mode != "empty-on-purpose") headers.Insert(0, new("X-Api-Key", mode == "empty" ? "" : "k123"));
    if (mode == "twice") headers.Add(new("X-Trace-Id", "from-the-caller"));
    return new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}")
    {
        Headers = headers,
        EmptyHeaders = mode == "empty-on-purpose" ? ["X-Api-Key"] : [],
        RemovedHeaders = mode == "no-accept" ? ["Accept"] : [],
    };
}

await using (CurlClient client = CurlClient.Create())
{
    for (int n = 1; n <= 2; n++)
    {
        CurlResponse response = await client.SendAsync(Build("hello.txt", n), CancellationToken.None);
        await using Stream body = response.Body;
        await body.CopyToAsync(Stream.Null);
    }
}

// The test: split what the server received into request heads and count each header in each.
await WaitForEndsAsync();
string received = string.Concat(server.Exchanges.Select(e => Encoding.ASCII.GetString(e.Received)));
string[] heads = received.Split("\r\n\r\n", StringSplitOptions.RemoveEmptyEntries);
bool passed = true;
foreach (string head in heads)
{
    string[] lines = head.Split("\r\n");
    Console.WriteLine(lines[0]);
    foreach (string name in new[] { "X-Api-Key", "X-Trace-Id", "Accept" })
    {
        string[] found = lines.Skip(1).Where(l => l.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase)).ToArray();
        Console.WriteLine($"  {name}: {found.Length} [{string.Join(" | ", found)}]");
        if (name != "Accept" && found.Length != 1) passed = false;
    }
}

Console.WriteLine(passed ? "each header arrived once" : "a header did not arrive exactly once");
return passed ? 0 : 1;

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
