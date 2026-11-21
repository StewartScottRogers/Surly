// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent fetches a file from surl-mcp as four byte ranges at once with curl-mcp, joins them in
// order, compares the result with the original, and reads back the ranges the server received.
// Run: dotnet run segments.cs -- <case>
//   transfer       four transfer calls at once, one Range header each (the default)
//   curl           one curl tool call, four transfers with --parallel, each saved to a file
//   slow           as transfer, against a server that holds every request 1 s
//   past-the-end   one transfer for a range that starts after the last byte
#:package ModelContextProtocol.Core@*

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "transfer";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it.
string here = Directory.GetCurrentDirectory();
await using McpClient surl = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "surl-mcp", Command = "surl-mcp",
    EnvironmentVariables = new Dictionary<string, string?> { ["SURL_MCP_ROOT"] = here },
}));
await using McpClient curl = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "curl-mcp", Command = "curl-mcp",
    EnvironmentVariables = new Dictionary<string, string?> { ["CURL_MCP_ROOT"] = here },
}));

// surl-mcp: 400,000 bytes that are the same on every run, seeded as base64.
byte[] original = new byte[400_000];
new Random(2026).NextBytes(original);
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files_base64"] = new Dictionary<string, string> { ["big.bin"] = Convert.ToBase64String(original) },
    ["arguments"] = which == "slow" ? new[] { "--fault", "delay;stage=command;ms=1000" } : Array.Empty<string>(),
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "big.bin";
string[] ranges = ["0-99999", "100000-199999", "200000-299999", "300000-399999"];

if (which == "past-the-end")
{
    JsonElement refused = await Transfer("800000-899999");
    Console.WriteLine($"statusCode: {refused.GetProperty("statusCode")}, Content-Range {Header(refused, "Content-Range")}, failure: {refused.GetProperty("failure").GetRawText()}");
    return 0;
}

byte[][] parts;
if (which == "curl")
{
    // curl: one command line, a transfer per range separated by --next, run side by side.
    var line = new List<string> { "--parallel", "--parallel-immediate" };
    for (int i = 0; i < ranges.Length; i++)
    {
        if (i > 0)
        {
            line.Add("--next");
        }

        line.AddRange(["-s", "-f", "-r", ranges[i], "-o", $"segment{i}", url]);
    }

    CallToolResult fetched = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = line.ToArray() });
    Console.WriteLine($"exitCode: {JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement.GetProperty("exitCode")}");
    parts = ranges.Select((_, i) => File.ReadAllBytes($"segment{i}")).ToArray();
}
else
{
    // transfer: one call per range, all four sent before any answer is awaited.
    JsonElement[] answers = await Task.WhenAll(ranges.Select(Transfer));
    foreach (JsonElement answer in answers)
    {
        JsonElement body = answer.GetProperty("body");
        Console.WriteLine($"{answer.GetProperty("statusCode")} {Header(answer, "Content-Range")}, isBase64 {body.GetProperty("isBase64")}, isTruncated {body.GetProperty("isTruncated")}");
    }

    parts = answers.Select(a => Convert.FromBase64String(a.GetProperty("body").GetProperty("content").GetString()!)).ToArray();
}

// Join in range order and compare digests.
byte[] joined = parts.SelectMany(p => p).ToArray();
bool same = SHA256.HashData(joined).SequenceEqual(SHA256.HashData(original));
Console.WriteLine($"joined: {joined.Length} bytes, {(same ? "identical" : "differs")}");

// The server's side: get_exchanges holds each request head it received. The record can trail the
// client by a moment, so ask again until all four ranges are there, for up to 10 s.
List<string> received = [];
for (var waited = System.Diagnostics.Stopwatch.StartNew(); received.Count < 4 && waited.Elapsed.TotalSeconds < 10; await Task.Delay(100))
{
    CallToolResult seen = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id });
    received = seen.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray()
        .SelectMany(e => e.GetProperty("chunks").EnumerateArray())
        .Where(c => c.GetProperty("direction").GetString() == "received")
        .SelectMany(c => c.GetProperty("data").GetString()!.Split("\r\n"))
        .Where(l => l.StartsWith("Range: "))
        .OrderBy(l => long.Parse(l[13..l.IndexOf('-')]))
        .ToList();
}
foreach (string range in received)
{
    Console.WriteLine($"server received {range}");
}

if (which == "slow")
{
    // get_events: connections opened and closed, in order. The most open at once were in flight together.
    CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "exchange_started", "exchange_ended" },
    });
    int open = 0, most = 0;
    foreach (JsonElement e in events.StructuredContent!.Value.GetProperty("events").EnumerateArray().OrderBy(e => e.GetProperty("sequence").GetInt64()))
    {
        open += e.GetProperty("kind").GetString() == "exchange_started" ? 1 : -1;
        most = Math.Max(most, open);
    }

    Console.WriteLine($"connections open at once: {most}");
}

return same ? 0 : 1;

async Task<JsonElement> Transfer(string range)
{
    var headers = new JsonArray { (JsonNode)new JsonObject { ["name"] = "Range", ["value"] = $"bytes={range}" } };
    CallToolResult result = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url, ["headers"] = headers });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}

static string Header(JsonElement answer, string name) => answer.GetProperty("headers").EnumerateArray()
    .First(h => h.GetProperty("name").GetString()!.Equals(name, StringComparison.OrdinalIgnoreCase)).GetProperty("value").GetString()!;
