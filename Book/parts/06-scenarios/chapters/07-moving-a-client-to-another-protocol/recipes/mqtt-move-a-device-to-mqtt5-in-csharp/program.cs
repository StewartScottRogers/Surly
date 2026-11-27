// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves MQTT 3.1.1 and MQTT 5 over one bus with Surl.Api and plays an old and a new device against it with Curl.Api.
// Run: dotnet run program.cs -- <case> [version]
//   old-to-new       the old device publishes, the new one subscribes
//   new-to-old       a message from the MQTT 5 side, the old device subscribes
//   replaced         the old device publishes over what the MQTT 5 side left
//   clear            the MQTT 5 side clears the topic, the old device subscribes
//   outside          the old device publishes outside the --topic filter, the new one subscribes
//   login            one account, the same login on both versions
//   wrong-password   mqtt | mqtt5: a wrong password
//   no-login         mqtt | mqtt5: no user name
//   clear-password   mqtt | mqtt5: the right password, with no --allow-plaintext-auth
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args[0];
string version = args.Length > 1 ? args[1] : "mqtt";
bool accounts = which is "login" or "wrong-password" or "no-login" or "clear-password";

// One broker, two listeners: [0] is MQTT 3.1.1 and [1] is MQTT 5, on one message bus. --topic keeps what a
// device publishes under plant. Exchanges are recorded so that the test waits for the broker, not for a clock.
var options = new SurlServerOptions
{
    ListenUrls = { "mqtt://127.0.0.1:0", "mqtt5://127.0.0.1:0" },
    AllowAnonymous = !accounts,
    RecordExchanges = true,
    Topics = { "plant/#;retain" },
    AdditionalArguments = { "--mqtt5-disconnect-after-retained" },
};
if (accounts)
{
    options.Accounts.Add(new SurlAccount("lamp", "s3cret"));
    options.AllowPlaintextAuthentication = which != "clear-password";
}

await using var server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();
string oldUrl = server.BoundUrls[0].ToString();
string newUrl = server.BoundUrls[1].ToString();
long lastSequence = 0;

string topic = "plant/hall/temp";
try
{
    switch (which)
    {
        case "old-to-new":
            await Send(HttpMethod.Post, oldUrl, topic, "68");
            Console.WriteLine($"mqtt5 subscriber reads: {Text(await Receive(newUrl, topic))}");
            Console.WriteLine($"broker keeps: {await Kept(topic)}");
            break;
        case "new-to-old":
            await Seed(topic, "71");
            byte[] old = await Receive(oldUrl, topic);
            int length = (old[0] << 8) | old[1];
            Console.WriteLine($"mqtt subscriber reads: length {length}, topic {Encoding.UTF8.GetString(old, 2, length)}, payload {Encoding.UTF8.GetString(old, 2 + length, old.Length - 2 - length)}");
            break;
        case "replaced":
            await Seed(topic, "71");
            await Send(HttpMethod.Post, oldUrl, topic, "72");
            Console.WriteLine($"mqtt5 subscriber reads: {Text(await Receive(newUrl, topic))}");
            break;
        case "clear":
            await Send(HttpMethod.Post, oldUrl, topic, "71");
            await Seed(topic, "");
            Console.WriteLine($"mqtt subscriber reads {(await Receive(oldUrl, topic)).Length} bytes");
            Console.WriteLine($"broker keeps: {await Kept(topic)}");
            break;
        case "outside":
            await Send(HttpMethod.Post, oldUrl, "garden/temp", "5");
            Console.WriteLine($"mqtt5 subscriber reads: {Text(await Receive(newUrl, "garden/temp"))}");
            Console.WriteLine($"broker keeps: {await Kept("garden/temp")}");
            break;
        case "login":
            await Send(HttpMethod.Post, oldUrl, "plant/lamp/temp", "21", login: "s3cret");
            Console.WriteLine($"mqtt5 subscriber reads: {Text(await Receive(newUrl, "plant/lamp/temp", login: "s3cret"))}");
            break;
        default:
            string url = version == "mqtt5" ? newUrl : oldUrl;
            string password = which == "no-login" ? "none" : which == "wrong-password" ? "wrong" : "s3cret";
            await Receive(url, "plant/lamp/temp", login: password);
            Console.WriteLine("accepted");
            break;
    }
}
catch (CurlException e)
{
    Console.WriteLine($"{version} refused: curl code {(int)e.Code} {e.Code}: {e.Message}");
    return (int)e.Code;
}

return 0;

// A client call, as a device makes it. A publish is not acknowledged and a subscribe is over when the broker has
// sent its retained messages, so each call is followed by the broker's own record of the exchange ending, awaited
// with a bound; the sequence before the call is what the wait starts after.
async Task<byte[]> Call(HttpMethod method, string baseUrl, string path, string? text, string? login)
{
    long before = lastSequence;
    var request = new CurlRequest(method, $"{baseUrl}{path}");
    if (text is not null)
    {
        request = request with { Body = CurlContent.FromBytes(Encoding.UTF8.GetBytes(text)) };
    }

    if (login is not null && login != "none")
    {
        request = request with { Authentication = new CurlAuthenticationOptions { UserName = "lamp", Password = login } };
    }

    byte[] body = [];
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream stream = response.Body;
        using var bytes = new MemoryStream();
        await stream.CopyToAsync(bytes);
        body = bytes.ToArray();
    }
    finally
    {
        SurlEvent ended = await server.WaitForEventAsync(
            new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, AfterSequence = before }, TimeSpan.FromSeconds(10));
        lastSequence = ended.Sequence;
    }

    return body;
}

// Curl.Api in this build does not publish over mqtt5:// (a POST there sends a subscribe, and nothing is kept), so the
// MQTT 5 side of the bus is played by the server's own publish, which sets the retain flag itself.
async Task Seed(string path, string text) =>
    await server.Messages.PublishAsync(path, Encoding.UTF8.GetBytes(text), true, CancellationToken.None);

Task Send(HttpMethod method, string baseUrl, string path, string text, string? login = null) => Call(method, baseUrl, path, text, login);
Task<byte[]> Receive(string baseUrl, string path, string? login = null) => Call(HttpMethod.Get, baseUrl, path, null, login);

static string Text(byte[] bytes) => bytes.Length == 0 ? "nothing" : Encoding.UTF8.GetString(bytes);

// What the broker holds for a topic, read without a client and so without a login.
async Task<string> Kept(string path)
{
    SurlRetainedMessage? kept = await server.Messages.ReadRetainedAsync(path, CancellationToken.None);
    return kept is null ? "nothing" : Encoding.UTF8.GetString(kept.Payload.Span);
}
