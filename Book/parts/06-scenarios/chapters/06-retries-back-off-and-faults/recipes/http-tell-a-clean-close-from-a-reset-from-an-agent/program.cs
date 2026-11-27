// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent ends a download early both ways: through surl-mcp it runs an HTTP server with two listeners
// and adds a fault rule to each that cuts a connection after 1,000,000 bytes, one closing cleanly and
// one resetting; it downloads the same file from each with curl-mcp's curl tool, then reads the
// server's events and fault rules to see which rule ended which download.
// Run: dotnet run program.cs -- <case>
//   graceful  download from the listener whose rule closes cleanly (the default)
//   reset     download from the listener whose rule resets
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "graceful";

// site/big.bin: 2,000,000 random bytes, in the folder both hosts are confined to.
string here = Directory.GetCurrentDirectory();
Directory.CreateDirectory("site");
Directory.CreateDirectory("dl");
byte[] big = System.Security.Cryptography.RandomNumberGenerator.GetBytes(2_000_000);
File.WriteAllBytes(Path.Combine("site", "big.bin"), big);

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it.
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

// One server serving site on two HTTP listen URLs, ports chosen by the host.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0", "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--directory", "site" },
});
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string[] urls = server.GetProperty("bound_urls").EnumerateArray().Select(u => u.GetString()!).ToArray();

// One rule per listener: both cut a connection once 1,000,000 bytes, the head included, have gone out
// on it; close says how, graceful on the first, reset (the default, close left out) on the second.
await surl.CallToolAsync("add_fault", new Dictionary<string, object?>
{
    ["server_id"] = id, ["fault"] = $"drop;listener={urls[0]};after-bytes=1000000", ["close"] = "graceful",
});
await surl.CallToolAsync("add_fault", new Dictionary<string, object?>
{
    ["server_id"] = id, ["fault"] = $"drop;listener={urls[1]};after-bytes=1000000",
});

// The download, from the listener the case names. -w prints the status, the head's size and the body
// bytes received; --max-time bounds a transfer that would otherwise wait.
string url = which == "reset" ? urls[1] : urls[0];
CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
{
    ["args"] = new[] { "-sS", "--max-time", "30", "-o", $"dl/{which}.bin", "-w", "%{http_code} %{size_header} %{size_download}", url + "big.bin" },
});
JsonElement run = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
string failure = run.TryGetProperty("failure", out JsonElement f) && f.ValueKind == JsonValueKind.Object ? f.GetProperty("text").GetString()!.Split('\n').Last() : "none";
Console.WriteLine($"{which}: exitCode {run.GetProperty("exitCode").GetInt32()}, -w {run.GetProperty("standardOutput").GetProperty("content").GetString()}");
Console.WriteLine($"failure: {failure}");
Console.WriteLine($"stderr: {run.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd()}");

// What the curl tool left on disk: how many bytes, and whether they are the start of the file.
byte[] kept = File.ReadAllBytes(Path.Combine("dl", $"{which}.bin"));
Console.WriteLine($"kept: {kept.Length} bytes, the start of the file: {kept.AsSpan().SequenceEqual(big.AsSpan(0, kept.Length))}");

// The server's account: the exchange's end with the bytes it sent, and the fault rule that cut it.
// The server records the end a moment after the client sees it, so wait for it first.
await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "exchange_ended" }, ["timeout_ms"] = 10000,
});
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "fault_applied", "exchange_ended" },
});
string rule = "";
foreach (JsonElement e in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    if (e.GetProperty("kind").GetString() == "fault_applied")
    {
        rule = e.GetProperty("rule_id").GetString()!;
        Console.WriteLine($"fault_applied: rule {rule}, {e.GetProperty("fault_kind").GetString()}");
    }
    else
        Console.WriteLine($"exchange_ended: sent {e.GetProperty("sent_byte_count").GetInt64()} bytes");
}

// list_faults: the rule's own item says how it closes; no close key means reset, the default.
CallToolResult faults = await surl.CallToolAsync("list_faults", new Dictionary<string, object?> { ["server_id"] = id });
string item = faults.StructuredContent!.Value.GetProperty("faults").EnumerateArray()
    .Single(r => r.GetProperty("fault_id").GetString() == rule).GetProperty("item").GetString()!;
Console.WriteLine($"rule {rule} closes: {(item.Contains("close=graceful") ? "graceful" : "reset")}");
return 0;
