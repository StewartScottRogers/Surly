// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a notification client: the client listens on a SignalR hub, a stand-in hub in this
// process carries what the test sends, and the test checks what the client received.
// Run: dotnet run program.cs [-- <case>]
//   (none)    one notification, from the test to the client (the default)
//   order     three notifications, in the order sent
//   copy      what the sender gets back for its own call
//   two       two clients listening at once
//   missed    a notification sent before the client connected
//   silent    a client that is sent nothing
//   refused   a hub that allows no anonymous client
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using System.Text.Json;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "notify";

// The stand-in hub: Surl's SignalR server, open to anonymous clients unless the case says otherwise.
var options = new SurlServerOptions { ListenUrls = { "signalr://127.0.0.1:0" }, AllowAnonymous = which != "refused" };
await using SurlServer hub = await SurlServer.StartAsync(options);
string hubUrl = hub.BoundUrl.ToString();

// The test's own sender: a call to the hub's Broadcast method, which the hub passes to every client connected.
await using CurlClient sender = CurlClient.Create();
int pings = 0;   // how many numbered pings the test has sent

switch (which)
{
    case "notify":
    {
        await using var client = await NotificationClient.ConnectAsync(hubUrl);
        await Ready(client);
        await Send("order 1042 has shipped");
        Console.WriteLine($"received: {await client.NextAsync(TimeSpan.FromSeconds(30))}");
        break;
    }

    case "order":
    {
        await using var client = await NotificationClient.ConnectAsync(hubUrl);
        await Ready(client);
        foreach (string text in new[] { "first", "second", "third" })
        {
            await Send(text);
        }
        for (int i = 0; i < 3; i++)
        {
            Console.WriteLine($"received: {await client.NextAsync(TimeSpan.FromSeconds(30))}");
        }
        break;
    }

    case "copy":
    {
        Console.WriteLine($"sender got: {await Send("order 1042 has shipped")}");
        break;
    }

    case "two":
    {
        await using var one = await NotificationClient.ConnectAsync(hubUrl);
        await using var two = await NotificationClient.ConnectAsync(hubUrl);
        await Ready(one);
        await Ready(two);
        await Drain(one, null);
        await Send("order 1042 has shipped");
        Console.WriteLine($"one received: {await one.NextAsync(TimeSpan.FromSeconds(30))}");
        Console.WriteLine($"two received: {await two.NextAsync(TimeSpan.FromSeconds(30))}");
        break;
    }

    case "missed":
    {
        await Send("sent before anyone listened");
        await using var client = await NotificationClient.ConnectAsync(hubUrl);
        await Ready(client);
        await Send("sent after");
        Console.WriteLine($"received: {await client.NextAsync(TimeSpan.FromSeconds(30))}");
        break;
    }

    case "silent":
    {
        await using var client = await NotificationClient.ConnectAsync(hubUrl);
        Console.WriteLine($"received: {await client.NextAsync(TimeSpan.FromMilliseconds(1500)) ?? "nothing within 1.5 s"}");
        break;
    }

    case "refused":
    {
        try
        {
            await using var client = await NotificationClient.ConnectAsync(hubUrl);
            await client.NextAsync(TimeSpan.FromSeconds(30));
            Console.WriteLine("connected");
        }
        catch (CurlException failure)
        {
            Console.WriteLine($"{failure.Code}: {failure.Message}");
        }
        break;
    }
}
return 0;

// The hub keeps no history, so a notification sent before the client is registered is lost. The test
// sends numbered pings until the client has one, then reads up to the last, so what follows starts clean.
async Task Ready(NotificationClient client)
{
    DateTime deadline = DateTime.UtcNow.AddSeconds(30);
    string? got = null;
    while (got is null)
    {
        if (DateTime.UtcNow > deadline)
        {
            throw new TimeoutException("the client never received a ping");
        }
        await Send($"ping {++pings}");
        got = await client.NextAsync(TimeSpan.FromMilliseconds(500));
    }
    await Drain(client, got);
}

async Task Drain(NotificationClient client, string? got)
{
    while (got != $"Broadcast: ping {pings}")
    {
        got = await client.NextAsync(TimeSpan.FromSeconds(30)) ?? throw new TimeoutException("a ping did not arrive");
    }
}

async Task<string> Send(string text)
{
    // Invocation message type 1; the texts here need no escaping.
    string call = $$"""{"type":1,"target":"Broadcast","arguments":["{{text}}"]}""";
    CurlResponse response = await sender.SendAsync(new CurlRequest(HttpMethod.Post, hubUrl) { Body = Encoding.UTF8.GetBytes(call) }, CancellationToken.None);
    await using Stream body = response.Body;
    return (await new StreamReader(body).ReadToEndAsync()).Trim();
}

// The client under test: it connects to a hub and hands out the notifications it receives, one at a time.
class NotificationClient : IAsyncDisposable
{
    private readonly CurlClient http = CurlClient.Create();
    private readonly CancellationTokenSource stop = new();
    private Task<CurlResponse>? connecting;
    private StreamReader? lines;

    // A GET with no body only listens. The response is not complete until the hub sends something,
    // so the connection is made in the background and the first read waits for it.
    public static Task<NotificationClient> ConnectAsync(string hubUrl)
    {
        var client = new NotificationClient();
        client.connecting = client.http.SendAsync(new CurlRequest(HttpMethod.Get, hubUrl), client.stop.Token);
        return Task.FromResult(client);
    }

    // The next notification as "target: argument", or null when none comes within the wait.
    // A refused connection is thrown here as a CurlException.
    public async Task<string?> NextAsync(TimeSpan wait)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        timeout.CancelAfter(wait);
        try
        {
            CurlResponse response = await connecting!.WaitAsync(timeout.Token);
            lines ??= new StreamReader(response.Body);
            string? line = await lines.ReadLineAsync(timeout.Token);
            if (line is null)
            {
                return null;
            }
            JsonElement message = JsonDocument.Parse(line).RootElement;
            return $"{message[0].GetString()}: {message[1].GetString()}";
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        try
        {
            await connecting!;
        }
        catch (Exception)
        {
            // Cancelled or refused: either way the connection is over.
        }
        lines?.Dispose();
        await http.DisposeAsync();
    }
}
