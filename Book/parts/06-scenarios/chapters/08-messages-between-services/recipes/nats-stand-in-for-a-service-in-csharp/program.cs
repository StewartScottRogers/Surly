// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test stands in for a NATS service: Surl.Api is the NATS server, a NATS session of the test's own answers
// prices.get, the app's request goes through Curl.Api, and the test checks what the app asked and what the
// server recorded.
// Run: dotnet run program.cs -- <scenario>
//   solution   the stand-in answers price=9.99
//   none       no stand-in is subscribed
//   silent     the stand-in receives the request and never answers
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Net.Sockets;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string scenario = args.Length > 0 ? args[0] : "solution";

var options = new SurlServerOptions { ListenUrls = { "nats://127.0.0.1:0" }, AllowAnonymous = true, RecordExchanges = true };
await using SurlServer server = await SurlServer.StartAsync(options);
Uri nats = server.BoundUrls[0];
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Timeout = TimeSpan.FromSeconds(scenario == "silent" ? 3 : 10) });

// A first Curl.Api request sets the engine up, which takes seconds; make it before any timed one.
await client.SendAsync(new CurlRequest(HttpMethod.Post, $"{nats}warm.up") { Body = "x"u8.ToArray() }, CancellationToken.None);

if (scenario == "none")
{
    Console.WriteLine($"app: {await Ask()}");
    return 0;
}

// The stand-in: a NATS client over a TcpClient. It subscribes to prices.get and sends PING; the server's PONG
// says the SUB is in place, so the app's request starts only after that.
using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(20));
using var tcp = new TcpClient();
await tcp.ConnectAsync(nats.Host, nats.Port, limit.Token);
NetworkStream stream = tcp.GetStream();
var reader = new StreamReader(stream, Encoding.ASCII);
await Send("CONNECT {\"verbose\":false}\r\nSUB prices.get 1\r\nPING\r\n");
while (await reader.ReadLineAsync(limit.Token) != "PONG")
{
}

Task<string> app = Ask();

// The request arrives as MSG <subject> <sid> <reply subject> <bytes>, then the payload.
string? line;
while ((line = await reader.ReadLineAsync(limit.Token))?.StartsWith("MSG prices.get ", StringComparison.Ordinal) == false)
{
}

string inbox = line!.Split(' ')[3];
string asked = (await reader.ReadLineAsync(limit.Token))!;
if (scenario == "silent")
{
    Console.WriteLine($"the stand-in received: {asked}");
    Console.WriteLine($"app: {await app}");
    return 0;
}

await Send($"PUB {inbox} 10\r\nprice=9.99\r\n");
Console.WriteLine($"app got: {await app}");
Console.WriteLine($"the app asked: {asked}");
Console.WriteLine($"reply subject: {inbox}");

// The server's record: every byte each client sent, kept as it arrives. Look for both PUB lines, at most 5 s.
string request = $"PUB prices.get {inbox} 6\r\n", reply = $"PUB {inbox} 10\r\n";
int Count(string text) => server.Exchanges.Count(e => Encoding.ASCII.GetString(e.Received).Contains(text, StringComparison.Ordinal));
for (var waited = System.Diagnostics.Stopwatch.StartNew(); (Count(request) == 0 || Count(reply) == 0) && waited.Elapsed.TotalSeconds < 5;)
{
    await Task.Delay(100);
}

Console.WriteLine($"server record, the app's request: {Count(request)}");
Console.WriteLine($"server record, the stand-in's reply: {Count(reply)}");
return 0;

Task Send(string text) => stream.WriteAsync(Encoding.ASCII.GetBytes(text), limit.Token).AsTask();

// The app: a POST to nats://host:port/prices.get?request publishes the body with a reply subject of its own and
// answers with the first reply.
async Task<string> Ask()
{
    try
    {
        CurlResponse response = await client.SendAsync(
            new CurlRequest(HttpMethod.Post, $"{nats}prices.get?request") { Body = "sku=A1"u8.ToArray() }, CancellationToken.None);
        await using Stream body = response.Body;
        return await new StreamReader(body).ReadToEndAsync();
    }
    catch (CurlException failed)
    {
        return $"CurlException {(int)failed.Code} {failed.Code}: {failed.Message}";
    }
}
