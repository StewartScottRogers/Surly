// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Refuses MQTT 5 publishes and subscribes by topic grant on an in-process broker with Surl.Api, and reads the reason
// codes back through Curl.Api. Run: dotnet run program.cs [-- case]
//   (no argument)  ann publishes 21, bob reads it
//   outside        ann subscribes outside every grant
//   pub-only       dan, granted pub, subscribes
//   no-grant       cy, with no grant, subscribes
//   sub-only       bob, granted sub, publishes: dropped
//   no-grant-pub   cy publishes: dropped
//   wrong-password a wrong password
//   no-login       no login at all
//   clear          a right password, with no AllowPlaintextAuthentication
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "solution";
const string Topic = "plant/line1/temp";

// The broker: MQTT 5 for the readers, and MQTT 3.1.1 beside it on the same message bus for the publishers, because
// Curl.Api reads over mqtt5:// but does not publish over it. One grant line gives each account its right.
var options = new SurlServerOptions
{
    ListenUrls = { "mqtt5://127.0.0.1:0", "mqtt://127.0.0.1:0" },
    AllowPlaintextAuthentication = which != "clear",
    RecordExchanges = true,
    Accounts =
    {
        new SurlAccount("ann", "annpw"), new SurlAccount("bob", "bobpw"),
        new SurlAccount("cy", "cypw"), new SurlAccount("dan", "danpw"),
    },
    Topics = { "plant/#;retain" },
    TopicAccess = { "plant/#=ann:pubsub,bob:sub,dan:pub" },
    AdditionalArguments = { "--mqtt5-disconnect-after-retained" },
};
await using var server = await SurlServer.StartAsync(options);
string readers = server.BoundUrls[0].ToString();
string writers = server.BoundUrls[1].ToString();
await using CurlClient client = CurlClient.Create();
long lastSequence = 0;

switch (which)
{
    case "outside":
        await Read("ann", "annpw", "office/desk");
        break;
    case "pub-only":
        await Read("dan", "danpw", Topic);
        break;
    case "no-grant":
        await Read("cy", "cypw", Topic);
        break;
    case "sub-only":
        await Publish("ann", "annpw", "21");
        await Publish("bob", "bobpw", "99");
        await Read("ann", "annpw", Topic);
        break;
    case "no-grant-pub":
        await Publish("ann", "annpw", "21");
        await Publish("cy", "cypw", "77");
        await Read("ann", "annpw", Topic);
        break;
    case "wrong-password":
        await Read("ann", "wrong", Topic);
        break;
    case "no-login":
        await Read(null, null, Topic);
        break;
    case "clear":
        await Read("ann", "annpw", Topic);
        break;
    default:
        await Publish("ann", "annpw", "21");
        await Read("bob", "bobpw", Topic);
        break;
}

return 0;

CurlRequest Request(HttpMethod method, string url, string? user, string? password) =>
    new(method, url)
    {
        Authentication = user is null ? null : new CurlAuthenticationOptions { UserName = user, Password = password },
    };

// A publish at quality of service 0 is not answered, so the call can return before the broker has applied it or dropped
// it. The end of the client's exchange is recorded after, and that is what the program waits for, with a bound.
async Task Publish(string user, string password, string text)
{
    CurlRequest request = Request(HttpMethod.Post, writers + Topic, user, password) with
    {
        Body = CurlContent.FromBytes(Encoding.UTF8.GetBytes(text)),
    };
    await client.SendAsync(request, CancellationToken.None);
    SurlEvent ended = await server.WaitForEventAsync(
        new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, AfterSequence = lastSequence }, TimeSpan.FromSeconds(10));
    lastSequence = ended.Sequence;
}

// A subscription over mqtt5://: what comes back is the retained message, the payload alone. A refusal is a CurlException.
async Task Read(string? user, string? password, string topic)
{
    string who = user ?? "anonymous";
    try
    {
        CurlResponse response = await client.SendAsync(Request(HttpMethod.Get, readers + topic, user, password), CancellationToken.None);
        await using Stream body = response.Body;
        using var bytes = new MemoryStream();
        await body.CopyToAsync(bytes);
        string text = Encoding.UTF8.GetString(bytes.ToArray());
        Console.WriteLine($"{who} reads {(text.Length == 0 ? "nothing" : text)}");
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{who} refused: curl error {(int)failure.Code}: {failure.Message}");
    }
}
