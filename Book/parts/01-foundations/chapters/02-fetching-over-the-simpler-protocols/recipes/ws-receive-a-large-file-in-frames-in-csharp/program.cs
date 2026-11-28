// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Receives files bigger than 64 KiB over WebSocket with Curl.Api from an in-process Surl.Api server, compares
// each with the original, and reads from the server's log how many frames it sent. Run: dotnet run program.cs
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using Curl.Api;
using Surl.Api;
using Surl.Output;

// The server: three files of random bytes, in memory. Its log, at the verbose level, says how each was sent.
// The server writes to it from its own threads, so the writer is synchronized and read under the same lock.
var text = new StringWriter();
TextWriter log = TextWriter.Synchronized(text);
string Logged() { lock (log) { return text.ToString(); } }
var options = new SurlServerOptions { ListenUrls = { "ws://127.0.0.1:0" }, Log = log, LogLevel = LogLevel.Verbose };
var files = new Dictionary<string, byte[]>
{
    ["big.bin"] = new byte[200_000],
    ["at-limit.bin"] = new byte[65_536],
    ["over-limit.bin"] = new byte[65_537],
};
var random = new Random(7);
foreach ((string name, byte[] bytes) in files)
{
    random.NextBytes(bytes);
    options.Files[name] = bytes;
}

await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();
using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(30));

foreach ((string name, byte[] original) in files)
{
    await using CurlWebSocket socket = await client.ConnectWebSocketAsync($"{server.BoundUrl}{name}", bound.Token);

    // One receive call returns the whole message, however many frames it crossed in.
    CurlWebSocketMessage file = await socket.ReceiveAsync(bound.Token);
    bool same = file.Data.Span.SequenceEqual(original);
    Console.WriteLine($"{name}: {file.Type}, {file.Data.Length} bytes, identical: {(same ? "yes" : "no")}");

    // The next message is the server's close.
    CurlWebSocketMessage next = await socket.ReceiveAsync(bound.Token);
    Console.WriteLine($"{name}: then {next.Type}");
}

// The server's log has one line for each file it sent, with the frame count. The file is sent before the
// line is written, so wait for the last one (5 s at most).
for (int tries = 0; tries < 50 && !Logged().Contains("/over-limit.bin as a binary"); tries++)
{
    await Task.Delay(100);
}

foreach (string line in Logged().Split('\n').Where(line => line.Contains("Sent ")))
{
    Console.WriteLine($"log: {line[(line.IndexOf("Sent ", StringComparison.Ordinal))..].Trim()}");
}

return 0;
