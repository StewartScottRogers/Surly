// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Opens a WebSocket to an in-process server with Curl.Api, receives the file it sends as one message,
// and closes. Run: dotnet run program.cs [-- <case>]
//   receive   ReceiveAsync for the file, then for the server's close, then CloseAsync (the default)
//   read-all  ReadMessagesAsync, which stops after the server's close
//   recorded  what the server recorded from the client after it closed
//   no-close  the same, when the program disposes the socket without CloseAsync
//   missing   a path the server does not have
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using System.Security.Cryptography;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "receive";

// The server: a 100,000-byte file, sent whole as one message to whoever opens a WebSocket on its path.
byte[] report = new byte[100_000];
new Random(7).NextBytes(report);
var options = new SurlServerOptions { ListenUrls = { "ws://127.0.0.1:0" }, RecordExchanges = true };
options.Files["report.bin"] = report;
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();
string path = which == "missing" ? "missing.bin" : "report.bin";

// Every call gets a bound: a server that never answers fails the program instead of hanging it.
using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(30));
try
{
    await using CurlWebSocket socket = await client.ConnectWebSocketAsync($"{server.BoundUrl}{path}", bound.Token);
    if (which == "read-all")
    {
        await foreach (CurlWebSocketMessage message in socket.ReadMessagesAsync(bound.Token))
        {
            Console.WriteLine($"message: {message.Type}, {message.Data.Length} bytes");
        }

        Console.WriteLine("enumeration ended");
    }
    else
    {
        CurlWebSocketMessage file = await socket.ReceiveAsync(bound.Token);
        bool same = SHA256.HashData(file.Data.Span).SequenceEqual(SHA256.HashData(report));
        Console.WriteLine($"message: {file.Type}, {file.Data.Length} bytes, same SHA-256: {(same ? "yes" : "no")}");
        CurlWebSocketMessage next = await socket.ReceiveAsync(bound.Token);
        Console.WriteLine($"then: {next.Type}, status {(next.CloseStatus?.ToString() ?? "none")}");

        // Answer the server's close with ours: 1000 is a normal closure.
        if (which != "no-close")
        {
            await socket.CloseAsync(1000, "done", bound.Token);
            Console.WriteLine("closed: 1000 done");
        }
    }
}
catch (CurlException refused)
{
    Console.WriteLine($"refused: {refused.Code} ({(int)refused.Code})");
    return 1;
}

if (which is "recorded" or "no-close")
{
    // The last bytes the server received from the client, once the exchange has ended (5 s at most).
    // A close frame starts 0x88 (final, opcode 8); the next byte has the mask bit, as every client frame must.
    for (int tries = 0; tries < 50 && server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded }).Count == 0; tries++)
    {
        await Task.Delay(100);
    }

    SurlExchangeRecord exchange = server.Exchanges.Last();
    byte[] last = exchange.Chunks.Last(c => c.Direction == SurlExchangeDirection.Received).Bytes.ToArray();
    string frame = last[0] == 0x88 ? $"a close frame, masked: {((last[1] & 0x80) != 0 ? "yes" : "no")}" : "not a close frame";
    Console.WriteLine($"server's last received from the client: {frame}");
}

return 0;
