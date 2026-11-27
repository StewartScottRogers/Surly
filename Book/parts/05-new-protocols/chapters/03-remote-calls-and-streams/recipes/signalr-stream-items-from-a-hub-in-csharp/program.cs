// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Hosts a SignalR hub with Surl.Api and consumes its Count stream, item by item, over a WebSocket,
// speaking the hub protocol by hand (Curl.Api does not send the call message for signalr://).
// Run: dotnet run program.cs [-- case]
//   (no argument)    stream Count with 5
//   empty            stream Count with 0
//   unknown-method   stream a method the hub does not have
//   wrong-type       invoke Count as an ordinary call, then stream Echo
//   invalid-argument stream Count with -1
//   refused          negotiate with a hub that allows no anonymous client
#:package Surly.Surl.Api@0.1.12

using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "count";

// The hub, in-process, on a free port. AllowAnonymous is --allow-anonymous: without it negotiating is refused.
var options = new SurlServerOptions { ListenUrls = { "signalr://127.0.0.1:0" }, AllowAnonymous = which != "refused" };
await using var server = await SurlServer.StartAsync(options);
string root = $"http://{server.BoundUrl.Host}:{server.BoundUrl.Port}/";

// Negotiate over HTTP: the hub answers a connection token, and the WebSocket opens with it.
using var http = new HttpClient();
HttpResponseMessage negotiated = await http.PostAsync($"{root}negotiate?negotiateVersion=1", null);
if (negotiated.StatusCode != HttpStatusCode.OK)
{
    Console.WriteLine($"negotiate: {(int)negotiated.StatusCode} {negotiated.ReasonPhrase}");
    return 22;
}

string token = JsonNode.Parse(await negotiated.Content.ReadAsStringAsync())!["connectionToken"]!.GetValue<string>();
using var socket = new ClientWebSocket();
await socket.ConnectAsync(new Uri($"ws://{server.BoundUrl.Host}:{server.BoundUrl.Port}/?id={token}"), CancellationToken.None);

// Hub messages are JSON texts, each ended by the record separator, U+001E. The first, both ways, is the handshake.
await Send("""{"protocol":"json","version":1}""");
Console.WriteLine($"handshake: {await Receive()}");

int exit = 0;
switch (which)
{
    case "empty":
        exit = await Stream("Count", "[0]");
        break;
    case "unknown-method":
        exit = await Stream("Shout");
        break;
    case "wrong-type":
        exit = await Invoke("Count", "[3]");
        exit = Math.Max(exit, await Stream("Echo", "[\"hi\"]"));
        break;
    case "invalid-argument":
        exit = await Stream("Count", "[-1]");
        break;
    default:
        exit = await Stream("Count", "[5]");
        break;
}

await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
return exit;

// A stream invocation, type 4: the hub answers one message of type 2 per item, then a completion, type 3.
async Task<int> Stream(string target, string arguments = "[]") => await Call(4, target, arguments);

// An ordinary invocation, type 1.
async Task<int> Invoke(string target, string arguments = "[]") => await Call(1, target, arguments);

async Task<int> Call(int type, string target, string arguments)
{
    await Send($$"""{"type":{{type}},"invocationId":"1","target":"{{target}}","arguments":{{arguments}}}""");
    int items = 0;
    while (true)
    {
        JsonNode message = JsonNode.Parse(await Receive())!;
        switch (message["type"]!.GetValue<int>())
        {
            case 2:
                items++;
                Console.WriteLine($"{target}: item {message["item"]}");
                break;
            case 3 when message["error"] is JsonNode error:
                Console.WriteLine($"{target}: error after {items} items: {error}");
                return 22;
            case 3:
                Console.WriteLine($"{target}: completed after {items} items");
                return 0;
        }
    }
}

async Task Send(string json) =>
    await socket.SendAsync(Encoding.UTF8.GetBytes(json + "\u001e"), WebSocketMessageType.Text, true, CancellationToken.None);

// One hub message: the text of one WebSocket message, without its record separator. Pings, type 6, are skipped.
async Task<string> Receive()
{
    var buffer = new byte[4096];
    while (true)
    {
        WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        string text = Encoding.UTF8.GetString(buffer, 0, result.Count).TrimEnd('\u001e');
        if (!text.Contains("\"type\":6"))
        {
            return text;
        }
    }
}
