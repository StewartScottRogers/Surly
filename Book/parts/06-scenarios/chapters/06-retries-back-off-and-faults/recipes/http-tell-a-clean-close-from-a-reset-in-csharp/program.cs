// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# test that tells a clean close from a reset: Surl.Api serves a 2,000,000-byte file and cuts the
// connection once it has sent 1,000,000 bytes, closing it cleanly or resetting it; Curl.Api downloads
// it, and the program says how the download ended, what arrived, and what the server recorded.
// Run: dotnet run program.cs -- <case>
//   graceful  the cut closes the connection cleanly (the default)
//   reset     the cut resets the connection
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "graceful";
FaultClose close = which == "reset" ? FaultClose.Reset : FaultClose.Graceful;

// big.bin: 2,000,000 random bytes. One rule cuts every connection once 1,000,000 bytes, the response head
// included, have gone out on it. The verbose log goes to a StringWriter; the exchanges are recorded.
byte[] big = System.Security.Cryptography.RandomNumberGenerator.GetBytes(2_000_000);
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    Faults = { SurlFault.DropConnection() with { AfterBytes = 1_000_000, Close = close } },
    RecordExchanges = true,
    Log = log,
    LogLevel = LogLevel.Verbose,
};
options.Files["big.bin"] = big;
await using SurlServer server = await SurlServer.StartAsync(options);

// The timeout bounds the whole transfer, the body included, so a cut that never reaches the client
// still ends as a CurlException.
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Timeout = TimeSpan.FromSeconds(30) });
var kept = new MemoryStream();
int status = 0;
try
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}big.bin"), CancellationToken.None);
    status = response.StatusCode;
    await using Stream body = response.Body;
    await body.CopyToAsync(kept);
    Console.WriteLine($"{which}: {status}, {kept.Length} bytes, complete");
}
catch (CurlException failure)
{
    Console.WriteLine($"{which}: {status}, {kept.Length} bytes, then CurlException {(int)failure.Code} {failure.Code}: {failure.Message}");
}

// What the server sent: the recorded exchange's bytes, the response head and then the body.
byte[] sent = server.Exchanges.Single().Sent;
int head = sent.AsSpan().IndexOf("\r\n\r\n"u8) + 4;
int sentBody = sent.Length - head;
bool prefix = kept.ToArray().AsSpan().SequenceEqual(big.AsSpan(0, (int)kept.Length));
Console.WriteLine($"sent {sent.Length} bytes: a {head}-byte head and {sentBody} body bytes");
Console.WriteLine($"received: {kept.Length} of the {sentBody} body bytes sent, the start of the file: {prefix}");

// The server's own account: the event, then the log line about the cut.
foreach (FaultAppliedEvent e in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).OfType<FaultAppliedEvent>())
    Console.WriteLine($"event: rule {e.RuleId}, {e.Kind}");
string line = log.ToString().Split('\n').Single(l => l.Contains("bytes sent by fault rule")).TrimEnd();
Console.WriteLine($"log: {System.Text.RegularExpressions.Regex.Match(line, @"Exchange \d+ (.*)").Groups[1].Value}");
return 0;
