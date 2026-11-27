// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Seeds a DNS zone into an in-process surl with Surl.Api, looks up MX, TXT and AAAA records with Curl.Api,
// and writes each answer as one line of JSON.
// Run: dotnet run program.cs [-- case]
//   (no argument)    look up MX, TXT and AAAA in the zone
//   outside-the-zone look up a name no zone holds
//   no-such-type     look up a type the name has no record of
//   no-zone          look up in a server that was given no zone
//   read-back        read the seeded zone back as master-file text
//   second-zone      seed a second zone for the same apex
//   no-dns-server    seed a zone into a server with only an HTTP listener
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Curl.Api;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "answers";

// The server: DNS on a free port of the loopback address (UDP and TCP), or HTTP in the one case that has no DNS.
string listen = which == "no-dns-server" ? "http://127.0.0.1:0" : "dns://127.0.0.1:0";
var options = new SurlServerOptions { ListenUrls = { listen } };
await using var server = await SurlServer.StartAsync(options);

// The zone, as the master-file text a zone file holds.
const string zone = """
    $ORIGIN example.test.
    $TTL 300
    @    IN SOA  ns1.example.test. hostmaster.example.test. 1 3600 600 86400 300
    @    IN NS   ns1.example.test.
    @    IN MX   10 mail.example.test.
    @    IN MX   20 mail2.example.test.
    @    IN TXT  "v=spf1 mx -all" "second string"
    @    IN AAAA 2001:db8::25
    mail IN A    192.0.2.25
    """;
if (which != "no-zone")
{
    try
    {
        await server.Zones.AddZoneAsync("example.test", zone);
        if (which == "second-zone")
        {
            await server.Zones.AddZoneAsync("example.test", zone);
        }
    }
    catch (SurlSeedRefusedException refused)
    {
        Console.WriteLine($"seed refused: {refused.Store} {refused.Refusal}");
        return 1;
    }
}

if (which == "read-back")
{
    Console.Write(await server.Zones.ReadAsync("example.test"));
    return 0;
}

// Relaxed escaping keeps the quotes of a TXT string readable.
var plain = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

// The client: one dns:// request per lookup; the answer arrives as dig-like text.
await using CurlClient client = CurlClient.Create();
(string, string)[] lookups = which switch
{
    "outside-the-zone" => [("nope.invalid", "A")],
    "no-such-type" => [("example.test", "SRV")],
    "no-zone" => [("example.test", "MX")],
    _ => [("example.test", "MX"), ("example.test", "TXT"), ("example.test", "AAAA")],
};
foreach ((string name, string type) in lookups)
{
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{name}?type={type}"), CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();

    // Curl.Api has no --dns-json, so the program reads the text: the status from the header line,
    // and one entry per line of the answer section, whose fields are separated by tabs.
    string status = Regex.Match(text, @"status: (\w+)").Groups[1].Value;
    var answers = new JsonArray();
    bool inAnswer = false;
    foreach (string line in text.Split('\n'))
    {
        if (line.StartsWith(";; ")) { inAnswer = line == ";; ANSWER SECTION:"; continue; }
        string[] field = line.Split('\t');
        if (inAnswer && field.Length == 5)
        {
            answers.Add((JsonNode)new JsonObject { ["ttl"] = int.Parse(field[1]), ["data"] = field[4] });
        }
    }

    Console.WriteLine(new JsonObject { ["name"] = name, ["type"] = type, ["status"] = status, ["answers"] = answers }.ToJsonString(plain));
}

return 0;
