// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Reads retained MQTT messages over WebSocket from an in-process broker with Surl.Api, after publishing them over TCP,
// with Curl.Api. Run: dotnet run program.cs [-- case]
//   (no argument)  a message published over TCP is read over WebSocket
//   later          the same message is read twice, by two clients
//   wildcard       two topics are read with one filter
//   outside        a topic outside the filter keeps nothing
//   clear          an empty publish clears a topic
//   plain          a WebSocket client that is not an MQTT client
//   other-path     a WebSocket upgrade to another path
//   no-login       an account broker, a client with no login
//   clear-text     an account broker, the right password over the WebSocket
//   allowed        the same, with AllowPlaintextAuthentication
//   wrong          the same, with a wrong password
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "solution";
bool accounts = which is "no-login" or "clear-text" or "allowed" or "wrong";

// One broker on two listen URLs: MQTT 3.1.1 over TCP for the devices, which Curl.Api can publish through, and MQTT 5
// over WebSocket for the dashboard. They share one message bus.
var options = new SurlServerOptions
{
    ListenUrls = { "mqtt://127.0.0.1:0", "mqtt5+ws://127.0.0.1:0" },
    AllowAnonymous = !accounts,
    AllowPlaintextAuthentication = which is "allowed" or "wrong",
    RecordExchanges = true,
    Topics = { "dash/#;retain" },
    AdditionalArguments = { "--mqtt5-disconnect-after-retained" },
};
if (accounts)
{
    options.Accounts.Add(new SurlAccount("ann", "s3cret"));
}

await using var server = await SurlServer.StartAsync(options);
string tcp = server.BoundUrls[0].ToString();
string ws = server.BoundUrls[1].ToString();
await using CurlClient client = CurlClient.Create();
long lastSequence = 0;

switch (which)
{
    case "later":
        await Publish("dash/temp", "21");
        await Read("first", "dash/temp");
        await Read("second", "dash/temp");
        break;
    case "wildcard":
        await Publish("dash/temp", "21");
        await Publish("dash/humidity", "22");
        await Read("dashboard", "dash/%23", sorted: true);
        break;
    case "outside":
        await Publish("lab/temp", "5");
        await Read("dashboard", "lab/temp");
        break;
    case "clear":
        await Publish("dash/temp", "21");
        await Publish("dash/temp", "");
        await Read("dashboard", "dash/temp");
        break;
    case "plain":
        await Upgrade("mqtt-less client", ws.Replace("mqtt5+ws://", "ws://") + "mqtt");
        break;
    case "other-path":
        await Upgrade("other path", ws.Replace("mqtt5+ws://", "ws://") + "other");
        break;
    case "no-login":
        await Read("anonymous", "dash/temp");
        break;
    case "clear-text":
        await Read("ann", "dash/temp", "ann", "s3cret");
        break;
    case "allowed":
        await Publish("dash/temp", "21", "ann", "s3cret");
        await Read("ann", "dash/temp", "ann", "s3cret");
        break;
    case "wrong":
        await Read("ann", "dash/temp", "ann", "wrong");
        break;
    default:
        await Publish("dash/temp", "21");
        await Read("dashboard", "dash/temp");
        break;
}

return 0;

CurlRequest Request(HttpMethod method, string url, string? user, string? password) =>
    new(method, url)
    {
        Authentication = user is null ? null : new CurlAuthenticationOptions { UserName = user, Password = password },
    };

// A publish over mqtt:// at quality of service 0 is not answered, so the call can return before the broker has applied
// it. The end of the client's exchange is recorded after, and that is what the program waits for, with a bound.
async Task Publish(string topic, string text, string? user = null, string? password = null)
{
    CurlRequest request = Request(HttpMethod.Post, tcp + topic, user, password) with
    {
        Body = CurlContent.FromBytes(Encoding.UTF8.GetBytes(text)),
    };
    await client.SendAsync(request, CancellationToken.None);
    await WaitForEnd();
}

async Task WaitForEnd()
{
    SurlEvent ended = await server.WaitForEventAsync(
        new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, AfterSequence = lastSequence }, TimeSpan.FromSeconds(10));
    lastSequence = ended.Sequence;
}

// A subscription over mqtt5+ws://: what comes back is the retained messages, the payloads alone. A refusal is a CurlException.
async Task Read(string who, string topic, string? user = null, string? password = null, bool sorted = false)
{
    try
    {
        CurlResponse response = await client.SendAsync(Request(HttpMethod.Get, ws + topic, user, password), CancellationToken.None);
        await using Stream body = response.Body;
        using var bytes = new MemoryStream();
        await body.CopyToAsync(bytes);
        string text = Encoding.UTF8.GetString(bytes.ToArray());
        if (sorted)
        {
            // Two payloads arrive one after the other, in no promised order: show them in a fixed one.
            text = string.Concat(Enumerable.Range(0, text.Length / 2).Select(i => text.Substring(i * 2, 2)).Order());
        }

        Console.WriteLine($"{who} reads {(text.Length == 0 ? "nothing" : text)}");
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{who} refused: curl error {(int)failure.Code}: {failure.Message}");
    }
}

// A WebSocket client that is not an MQTT client: the upgrade is refused, and the refusal is a CurlException.
async Task Upgrade(string who, string url)
{
    try
    {
        await client.SendAsync(new CurlRequest(HttpMethod.Get, url), CancellationToken.None);
        Console.WriteLine($"{who} upgraded");
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{who} refused: curl error {(int)failure.Code}: {failure.Message}");
    }
}
