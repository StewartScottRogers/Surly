// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Transfers a whole DNS zone from an in-process surl with Curl.Api: refused by default, served over TCP once
// --allow-zone-transfer is given, refused over UDP.
// Run: dotnet run program.cs [-- case]
//   (no argument)       allow transfers, ask over TCP, list the records
//   refused-by-default      no --allow-zone-transfer, over TCP
//   refused-by-default-udp  no --allow-zone-transfer, over UDP
//   refused-over-udp    allow transfers, ask over UDP
//   not-an-apex         allow transfers, ask over TCP for a name inside the zone
//   text-answer         allow transfers, read the dig-like text and not the JSON
//   no-incremental-type ask for IXFR
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text.Json;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "transfer";
bool allowed = !which.StartsWith("refused-by-default");

// The server: DNS on a free port of the loopback address, UDP and TCP on it. --allow-zone-transfer has no
// property, so it goes through AdditionalArguments in surl's own words.
var options = new SurlServerOptions { ListenUrls = { "dns://127.0.0.1:0" } };
if (allowed)
{
    options.AdditionalArguments.Add("--allow-zone-transfer");
}

await using var server = await SurlServer.StartAsync(options);
await server.Zones.AddZoneAsync("example.test", """
    $ORIGIN example.test.
    $TTL 300
    @    IN SOA  ns1.example.test. hostmaster.example.test. 1 3600 600 86400 300
    @    IN NS   ns1.example.test.
    @    IN MX   10 mail.example.test.
    mail IN A    192.0.2.25
    ns1  IN A    192.0.2.53
    """);

// The request: AXFR as the type, the transport and the JSON switch in Dns.
string name = which == "not-an-apex" ? "mail.example.test" : "example.test";
string type = which == "no-incremental-type" ? "IXFR" : "AXFR";
string transport = which is "refused-by-default-udp" or "refused-over-udp" ? "udp" : "tcp";
var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{name}?type={type}")
{
    Dns = new CurlDnsOptions { Transport = transport, Json = which != "text-answer" },
};

await using CurlClient client = CurlClient.Create();
CurlResponse response;
try
{
    response = await client.SendAsync(request, CancellationToken.None);
}
catch (CurlException refused)
{
    Console.WriteLine($"curl exit {(int)refused.Code}: {refused.Message}");
    return 1;
}

await using Stream body = response.Body;
string text = await new StreamReader(body).ReadToEndAsync();

if (which == "text-answer")
{
    // The text form: a header line that counts the answers, then a tab-separated line per record.
    string[] lines = text.Split('\n');
    Console.WriteLine(lines.Single(l => l.StartsWith(";; flags:")));
    string[] records = lines.SkipWhile(l => l != ";; ANSWER SECTION:").Skip(1).TakeWhile(l => l != "").ToArray();
    Console.WriteLine($"first: {records[0].Split('\t')[3]}, last: {records[^1].Split('\t')[3]}");
    return 0;
}

// The JSON form: the status, then one entry per record.
using JsonDocument answer = JsonDocument.Parse(text);
JsonElement[] answers = answer.RootElement.GetProperty("answer").EnumerateArray().ToArray();
Console.WriteLine($"{answer.RootElement.GetProperty("status")}: {answers.Length} records");
foreach (JsonElement record in answers)
{
    Console.WriteLine($"{record.GetProperty("name")} {record.GetProperty("type")}");
}

return 0;
