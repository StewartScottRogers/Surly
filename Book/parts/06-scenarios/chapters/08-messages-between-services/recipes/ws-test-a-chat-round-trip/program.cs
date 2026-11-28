// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a WebSocket chat round trip with Surl.Api and Curl.Api in one process: surl echoes every message,
// the client sends three chat messages, and the test checks each comes back in order, then what the server sent.
// Run: dotnet run program.cs -- <scenario>
//   solution   three messages and a close
//   too-big    a message past the server's --max-message
//   no-echo    a server without --ws-echo
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string scenario = args.Length > 0 ? args[0] : "solution";

// --ws-echo makes the WebSocket server send every whole message back; it has no SurlServerOptions property.
var options = new SurlServerOptions { ListenUrls = { "ws://127.0.0.1:0" }, RecordExchanges = true };
if (scenario != "no-echo")
{
    options.AdditionalArguments.Add("--ws-echo");
}

if (scenario == "too-big")
{
    options.AdditionalArguments.Add("--max-message");
    options.AdditionalArguments.Add("1k");
}

await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();
using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(20));

if (scenario == "no-echo")
{
    try
    {
        await using CurlWebSocket refused = await client.ConnectWebSocketAsync($"{server.BoundUrl}chat", limit.Token);
    }
    catch (CurlException failed)
    {
        Console.WriteLine($"connect: CurlException {(int)failed.Code} {failed.Code}");
    }

    return 0;
}

await using CurlWebSocket socket = await client.ConnectWebSocketAsync($"{server.BoundUrl}chat", limit.Token);

if (scenario == "too-big")
{
    await socket.SendTextAsync(new string('x', 2000), limit.Token);
    CurlWebSocketMessage answer = await socket.ReceiveAsync(limit.Token);
    Console.WriteLine($"answer: {answer.Type} {answer.CloseStatus}");
    return 0;
}

// The chat: send three messages, then read three; an echo server answers each whole message, in order.
string[] chat = ["hi", "how are you?", "bye for now"];
foreach (string line in chat)
{
    await socket.SendTextAsync(line, limit.Token);
}

var echoed = new List<string>();
for (int i = 0; i < chat.Length; i++)
{
    CurlWebSocketMessage message = await socket.ReceiveAsync(limit.Token);
    echoed.Add($"{message.Type} \"{message.Text}\"");
}

Console.WriteLine($"echoed: {string.Join(", ", echoed)}");
Console.WriteLine($"in order: {echoed.SequenceEqual(chat.Select(c => $"Text \"{c}\""))}");

// The client closes; the server answers with a close frame echoing the code.
await socket.CloseAsync(1000, "done", limit.Token);
CurlWebSocketMessage closed = await socket.ReceiveAsync(limit.Token);
Console.WriteLine($"close: {closed.Type} {closed.CloseStatus}");

// The server's record: wait, at most 10 s, for the exchange to end, then read the frames it sent after the
// 101 head. A server frame is unmasked: a byte of FIN and opcode, a length under 126 here, then the payload.
await server.WaitForEventAsync(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded }, TimeSpan.FromSeconds(10));
byte[] sent = server.Exchanges.Single().Sent;
int at = Encoding.ASCII.GetString(sent).IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;
var frames = new List<string>();
while (at < sent.Length)
{
    int opcode = sent[at] & 0x0F, length = sent[at + 1] & 0x7F;
    byte[] payload = sent[(at + 2)..(at + 2 + length)];
    frames.Add(opcode == 1 ? $"text \"{Encoding.UTF8.GetString(payload)}\"" : opcode == 8 ? $"close {payload[0] << 8 | payload[1]}" : $"opcode {opcode}");
    at += 2 + length;
}

Console.WriteLine($"server sent: {Encoding.ASCII.GetString(sent).Split("\r\n")[0]}, then {string.Join(", ", frames)}");
return 0;
