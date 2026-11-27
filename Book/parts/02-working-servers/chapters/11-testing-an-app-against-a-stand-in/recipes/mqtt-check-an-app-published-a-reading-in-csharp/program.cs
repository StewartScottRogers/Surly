// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that checks what an app published: an in-process MQTT broker keeps the last reading on a topic, Curl.Api
// stands in for the app and publishes, the test waits for the broker to apply the publish, and then reads the
// reading the broker kept with Messages.ReadRetainedAsync.
// Run: dotnet run program.cs [-- case]
//   (no argument)  the app publishes 21.5, the check expects 21.5
//   wrong          the check expects 22.0
//   twice          the check runs twice: reading does not use the reading up
//   replaced       the app publishes 21.5 and then 22.0; the check expects 22.0
//   earlier        the same, and the check expects 21.5
//   nothing        the app publishes nothing
//   misspelt       the app publishes to sensors/hall/temprature; the check looks at sensors/hall/temp
//   misspelt-kept  the same, and the check looks at the misspelling
//   outside        the app publishes outside the declared filter
//   no-filter      a broker with no topic filter
//   closed         a broker with no AllowAnonymous
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "solution";

// The broker: it keeps what is published under sensors/ and lets a client in with no login. RecordExchanges
// makes it record each client's exchange, which is what the test waits for.
var options = new SurlServerOptions
{
    ListenUrls = { "mqtt://127.0.0.1:0" },
    AllowAnonymous = which != "closed",
    RecordExchanges = true,
};
if (which != "no-filter")
{
    options.Topics.Add("sensors/#;retain");
}

await using var server = await SurlServer.StartAsync(options);
long lastSequence = 0;

try
{
    switch (which)
    {
        case "nothing":
            return Check("sensors/hall/temp", "19.0", await Kept("sensors/hall/temp"));
        case "misspelt":
        case "misspelt-kept":
            await App("sensors/hall/temprature", "19.0");
            return which == "misspelt"
                ? Check("sensors/hall/temp", "19.0", await Kept("sensors/hall/temp"))
                : Check("sensors/hall/temprature", "19.0", await Kept("sensors/hall/temprature"));
        case "outside":
            await App("lab/kitchen/temp", "21.5");
            return Check("lab/kitchen/temp", "21.5", await Kept("lab/kitchen/temp"));
        case "replaced":
        case "earlier":
            await App("sensors/kitchen/temp", "21.5");
            await App("sensors/kitchen/temp", "22.0");
            return Check("sensors/kitchen/temp", which == "replaced" ? "22.0" : "21.5", await Kept("sensors/kitchen/temp"));
        default:
            // The app under test: one publish, as the app's own client would send it.
            await App("sensors/kitchen/temp", "21.5");
            string expected = which == "wrong" ? "22.0" : "21.5";
            int result = Check("sensors/kitchen/temp", expected, await Kept("sensors/kitchen/temp"));
            if (which == "twice")
            {
                result |= Check("sensors/kitchen/temp", expected, await Kept("sensors/kitchen/temp"));
            }

            return result;
    }
}
catch (CurlException refused)
{
    // The app's client was refused: Curl.Api reports the code curl would exit with.
    Console.WriteLine($"curl error {(int)refused.Code}: {refused.Message}");
    return (int)refused.Code;
}

// The app: Curl.Api publishes with the retain flag clear, as `curl -d` does. A publish at QoS 0 gets no answer, so
// SendAsync can return before the broker has applied it; the test waits for the broker to record the end of that
// exchange, with a bound that only a failure reaches, and never sleeps.
async Task App(string topic, string reading)
{
    await using CurlClient client = CurlClient.Create();
    var publish = new CurlRequest(HttpMethod.Post, $"{server.BoundUrl}{topic}") { Body = CurlContent.FromBytes(Encoding.UTF8.GetBytes(reading)) };
    CurlResponse sent = await client.SendAsync(publish, CancellationToken.None);
    await sent.Body.DisposeAsync();
    SurlEvent ended = await server.WaitForEventAsync(
        new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, AfterSequence = lastSequence }, TimeSpan.FromSeconds(10));
    lastSequence = ended.Sequence;
}

// What the broker holds for a topic, read from its store with no client and no login: null when nothing is kept.
async Task<string?> Kept(string topic)
{
    SurlRetainedMessage? kept = await server.Messages.ReadRetainedAsync(topic, CancellationToken.None);
    return kept is null ? null : $"{Encoding.UTF8.GetString(kept.Payload.Span)} at QoS {kept.QualityOfService}";
}

// The test's assertion: 0 when the broker holds the expected reading, 1 with a message that names what it holds.
static int Check(string topic, string expected, string? kept)
{
    if (kept is null)
    {
        Console.WriteLine($"FAIL: nothing kept on {topic}");
        return 1;
    }

    if (kept != $"{expected} at QoS 0")
    {
        Console.WriteLine($"FAIL: {topic} is {kept}, expected {expected}");
        return 1;
    }

    Console.WriteLine($"ok: {topic} is {kept}");
    return 0;
}
