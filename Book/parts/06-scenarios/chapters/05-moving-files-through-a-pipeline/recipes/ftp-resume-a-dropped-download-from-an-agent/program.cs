// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests a resuming FTP download: surl-mcp serves an image and drops the connection at the next
// RETR of it; curl-mcp's curl tool resumes from the partial copy in the folder with -C -; read_file and
// get_exchanges then check the copy and what the server was asked.
// Run: dotnet run program.cs -- <case>
//   retry      one curl call with --retry 1 --retry-all-errors (the default)
//   by-hand    two curl calls without --retry
//   whole      the copy in the folder is already the whole image
//   longer     the copy in the folder is a 300,000-byte file
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "retry";

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

// The image: 200,000 bytes that repeat from run to run. In the folder, what a download cut off at
// 65,536 bytes leaves behind.
byte[] image = new byte[200_000];
new Random(7).NextBytes(image);
File.WriteAllBytes("image.iso", which switch { "whole" => image, "longer" => new byte[300_000], _ => image[..65_536] });

// surl-mcp: an anonymous FTP server in memory, the image put in its store, and one drop at the next RETR.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = new[] { "--allow-anonymous" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "image.iso";
await surl.CallToolAsync("put_file", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = "/image.iso", ["base64"] = Convert.ToBase64String(image) });
CallToolResult fault = await surl.CallToolAsync("add_fault", new Dictionary<string, object?>
{
    ["server_id"] = id, ["fault"] = "drop;stage=command;command=RETR;path=/image.iso;times=1",
});
Console.WriteLine($"fault: {fault.StructuredContent!.Value.GetProperty("fault_id").GetString()}");

// curl-mcp: -C - resumes from the end of image.iso; -w prints the retries and the bytes this call fetched.
if (which == "by-hand")
{
    await Fetch([]);
    await Fetch([]);
}
else if (which is "whole" or "longer")
{
    await Fetch([]);
}
else
{
    await Fetch(["--retry", "1", "--retry-all-errors"]);
}

// surl-mcp: the copy against the server's file, and every REST the server was sent.
CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = "/image.iso" });
byte[] served = Convert.FromBase64String(read.StructuredContent!.Value.GetProperty("base64").GetString()!);
byte[] copy = File.ReadAllBytes("image.iso");
Console.WriteLine($"image.iso: {copy.Length} bytes, {(copy.AsSpan().SequenceEqual(served) ? "identical to the server's" : "DIFFERENT from the server's")}");

CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id });
var rests = new List<string>();
foreach (JsonElement exchange in exchanges.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray())
{
    foreach (JsonElement chunk in exchange.GetProperty("chunks").EnumerateArray())
    {
        string data = chunk.TryGetProperty("data", out JsonElement d) ? d.GetString() ?? "" : "";
        if (chunk.GetProperty("direction").GetString() == "received" && data.StartsWith("REST "))
        {
            rests.Add(data.Trim());
        }
    }
}

Console.WriteLine($"server was sent: {string.Join(", ", rests)}");

// surl-mcp: every time the rule acted.
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "fault_applied" } });
foreach (JsonElement e in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    Console.WriteLine($"fault_applied: {e.GetProperty("rule_id").GetString()} {e.GetProperty("fault_kind").GetString()} {e.GetProperty("target").GetString()}");
}
return 0;

async Task Fetch(string[] extra)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = (string[])["-sS", "-C", "-", .. extra, "-o", "image.iso", "-w", "%{num_retries} %{size_download}", url],
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement root = answer.RootElement;
    string errors = root.GetProperty("standardError").GetProperty("content").GetString()!.Replace("\r", "").Trim();
    Console.WriteLine($"curl: exitCode {root.GetProperty("exitCode")}, tool error {(result.IsError == true ? "yes" : "no")}, "
        + $"retries and bytes {root.GetProperty("standardOutput").GetProperty("content").GetString()}{(errors.Length > 0 ? ", " + errors : "")}");
    Console.WriteLine($"copy: {new FileInfo("image.iso").Length} bytes");
}
