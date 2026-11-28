// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a gateway that observes a CoAP resource: an in-process Surl server holds thermostat.txt, the
// gateway registers as an observer (RFC 7641) over a UdpClient, the file is replaced with Curl.Api, and the
// test prints what the gateway was sent.
// Run: dotnet run program.cs [-- <case>]
//   coap          replace the file with a CoAP PUT (the default)
//   http          replace it with an HTTP PUT
//   delete        delete it with a CoAP DELETE
//   no-uploads    the same CoAP PUT to a server without AllowUploads
//   no-anonymous  the same CoAP PUT to a server without AllowAnonymous
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Net.Sockets;
using System.Text;
using Curl.Api;
using Surl.Api;

string how = args.Length > 0 ? args[0] : "coap";

// CoAP and HTTP on free ports over one in-memory store; AllowUploads and AllowAnonymous let a client with no
// login write.
var options = new SurlServerOptions
{
    ListenUrls = { "coap://127.0.0.1:0", "http://127.0.0.1:0" },
    AllowAnonymous = how != "no-anonymous",
    AllowUploads = how != "no-uploads",
};
options.Files["thermostat.txt"] = "setpoint=68"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
Uri coap = server.BoundUrls[0];

// The gateway registers: a confirmable GET with token 7A, Observe 0 (option 6) and Uri-Path thermostat.txt (option 11).
using var gateway = new UdpClient(coap.Host, coap.Port);
byte[] register = [0x41, 0x01, 0x00, 0x01, 0x7A, 0x60, 0x5D, 0x01, .. "thermostat.txt"u8];
await gateway.SendAsync(register);
Console.WriteLine($"registered: {await Receive()}");

// The file is replaced, or deleted, by another client.
await using CurlClient client = CurlClient.Create();
CurlRequest change = how switch
{
    "http" => new CurlRequest(HttpMethod.Put, $"{server.BoundUrls[1]}thermostat.txt") { Body = CurlContent.FromBytes("setpoint=70"u8.ToArray()) },
    "delete" => new CurlRequest(HttpMethod.Delete, $"{coap}thermostat.txt"),
    _ => new CurlRequest(HttpMethod.Put, $"{coap}thermostat.txt") { Body = CurlContent.FromBytes("setpoint=70"u8.ToArray()) },
};
CurlResponse changed = await client.SendAsync(change, CancellationToken.None);
await changed.Body.DisposeAsync();
Console.WriteLine($"{change.Method} answered {(how == "http" ? changed.StatusCode.ToString() : changed.Headers.First().Key)}");

// The test's assertion: the gateway is sent the change within 5 seconds.
Console.WriteLine($"then: {await Receive()}");
return 0;

// The next message for the gateway, within 5 seconds: its code, its Observe number if it has one, its Max-Age
// (option 14) and its payload.
// A confirmable notification is acknowledged, as RFC 7641 asks.
async Task<string> Receive()
{
    using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    byte[] m;
    try
    {
        m = (await gateway.ReceiveAsync(bound.Token)).Buffer;
    }
    catch (OperationCanceledException)
    {
        return "nothing within 5 s";
    }

    if ((m[0] >> 4 & 3) == 0)
    {
        await gateway.SendAsync(new byte[] { 0x60, 0x00, m[2], m[3] });
    }

    // Options follow the header and token as (delta, length) nibbles; 0xFF starts the payload.
    string observe = "no Observe", maxAge = "";
    int at = 4 + (m[0] & 0x0F), number = 0;
    while (at < m.Length && m[at] != 0xFF)
    {
        int delta = m[at] >> 4, length = m[at] & 0x0F;
        at++;
        if (delta == 13) { delta = m[at++] + 13; }
        if (length == 13) { length = m[at++] + 13; }
        number += delta;
        if (number == 6)
        {
            observe = "Observe " + m.Skip(at).Take(length).Aggregate(0, (v, b) => v << 8 | b);
        }
        else if (number == 14)
        {
            maxAge = ", Max-Age " + m.Skip(at).Take(length).Aggregate(0, (v, b) => v << 8 | b);
        }

        at += length;
    }

    string payload = at < m.Length ? Encoding.UTF8.GetString(m, at + 1, m.Length - at - 1) : "";
    return $"{m[1] >> 5}.{m[1] & 0x1F:00}, {observe}{maxAge}, \"{payload}\"";
}
