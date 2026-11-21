// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Grants MQTT topics to groups of accounts on an in-process broker with Surl.Api, and checks with Curl.Api that a
// member of the wrong group is refused. Run: dotnet run program.cs [-- case]
//   (no argument)  a sensor publishes, a dashboard reads it
//   second         another member of the sensors group publishes
//   dashboard      a dashboard publishes: dropped; a sensor subscribes: refused
//   outsider       an account in neither group publishes and subscribes
//   grown          the outsider joins the sensors group, and publishes
//   wrong-password a wrong password
//   no-plaintext   a right password, with no AllowPlaintextAuthentication
//   bad-grant      three grants that name no account or group
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "solution";
const string Topic = "plant/line1/temp";

if (which == "bad-grant")
{
    return await BadGrants();
}

// The broker: four accounts, two groups, and one grant line that gives each group its right. sensor3 is in no group,
// until the "grown" case puts it in the sensors group and changes nothing else.
var options = new SurlServerOptions
{
    ListenUrls = { "mqtt://127.0.0.1:0" },
    AllowPlaintextAuthentication = which != "no-plaintext",
    RecordExchanges = true,
    Accounts =
    {
        new SurlAccount("sensor1", "s1pass"), new SurlAccount("sensor2", "s2pass"),
        new SurlAccount("sensor3", "s3pass"), new SurlAccount("dash1", "d1pass"),
    },
    Groups = { which == "grown" ? "sensors=sensor1,sensor2,sensor3" : "sensors=sensor1,sensor2", "dashboards=dash1" },
    Topics = { "plant/#;retain" },
    TopicAccess = { "plant/#=@sensors:pub,@dashboards:sub" },
};
await using var server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();
long lastSequence = 0;

switch (which)
{
    case "second":
        await Publish("sensor1", "s1pass", "21");
        await Publish("sensor2", "s2pass", "22");
        await Read("dash1", "d1pass");
        break;
    case "dashboard":
        await Publish("sensor1", "s1pass", "22");
        await Publish("dash1", "d1pass", "99");
        Console.WriteLine($"kept: {await Kept()}");
        await Read("sensor1", "s1pass");
        break;
    case "outsider":
        await Publish("sensor1", "s1pass", "22");
        await Publish("sensor3", "s3pass", "77");
        Console.WriteLine($"kept: {await Kept()}");
        await Read("sensor3", "s3pass");
        break;
    case "grown":
        await Publish("sensor3", "s3pass", "23");
        await Read("dash1", "d1pass");
        break;
    case "wrong-password":
        await Read("dash1", "nope");
        break;
    case "no-plaintext":
        await Read("sensor1", "s1pass");
        break;
    default:
        await Publish("sensor1", "s1pass", "21");
        await Read("dash1", "d1pass");
        break;
}

return 0;

// A client's request for the topic, with the login in the CONNECT; Curl.Api sends a POST as a publish, a GET as a subscribe.
CurlRequest Request(HttpMethod method, string user, string password) =>
    new(method, $"{server.BoundUrl}{Topic}")
    {
        Authentication = new CurlAuthenticationOptions { UserName = user, Password = password },
    };

// A publish is not acknowledged, so the call can return before the broker has applied it, or dropped it. The end of
// the client's exchange is recorded after, and that is what the test waits for, with a bound, not a sleep.
async Task Publish(string user, string password, string text)
{
    CurlRequest request = Request(HttpMethod.Post, user, password) with { Body = CurlContent.FromBytes(Encoding.UTF8.GetBytes(text)) };
    await client.SendAsync(request, CancellationToken.None);
    SurlEvent ended = await server.WaitForEventAsync(
        new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, AfterSequence = lastSequence }, TimeSpan.FromSeconds(10));
    lastSequence = ended.Sequence;
}

// A subscription: what comes back is the topic's two-byte length, the topic and the message. A refusal is a CurlException.
async Task Read(string user, string password)
{
    try
    {
        CurlResponse response = await client.SendAsync(Request(HttpMethod.Get, user, password), CancellationToken.None);
        await using Stream body = response.Body;
        using var bytes = new MemoryStream();
        await body.CopyToAsync(bytes);
        string text = Encoding.UTF8.GetString(bytes.ToArray());
        Console.WriteLine($"{user} reads {(text.Length == 0 ? "nothing" : text[(2 + Topic.Length)..])}");
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{user} refused: curl error {(int)failure.Code}: {failure.Message}");
    }
}

// What the broker holds for the topic, read without a client, so it is not subject to the grants.
async Task<string> Kept()
{
    SurlRetainedMessage? kept = await server.Messages.ReadRetainedAsync(Topic, CancellationToken.None);
    return kept is null ? "nothing" : Encoding.UTF8.GetString(kept.Payload.Span);
}

// Grants and groups are checked as the server starts, so a bad one is a refusal to start, not a late failure.
async Task<int> BadGrants()
{
    // Each pair is a group and a grant; the first names a group, the grant after it is the one under test.
    (string Group, string? Grant)[] bad =
    [
        ("sensors=sensor1", "plant/#=sensors:pub"),
        ("sensors=sensor1", "plant/#=@robots:pub"),
        ("sensors=sensor1,ghost", null),
    ];
    foreach ((string group, string? grant) in bad)
    {
        var broken = new SurlServerOptions { ListenUrls = { "mqtt://127.0.0.1:0" }, Accounts = { new SurlAccount("sensor1", "s1pass") } };
        broken.Groups.Add(group);
        if (grant is not null)
        {
            broken.TopicAccess.Add(grant);
        }

        try
        {
            await using var never = await SurlServer.StartAsync(broken);
            Console.WriteLine("started");
        }
        catch (SurlServerStartException failure)
        {
            Console.WriteLine($"{failure.GetType().Name}: {failure.Message}");
        }
    }

    return 0;
}
