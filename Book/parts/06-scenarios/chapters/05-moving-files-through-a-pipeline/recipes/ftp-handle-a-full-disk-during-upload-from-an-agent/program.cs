// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests a pipeline step against a full disk: surl-mcp serves one store over FTP and HTTP with
// /archive out of space; curl-mcp's curl tool uploads the export over FTP, its transfer tool reads the
// path back over HTTP as the next step would, and read_file looks in the store.
// Run: dotnet run program.cs -- <case>
//   archive    upload the export to /archive (the default)
//   incoming   upload it to /incoming, which has room
//   big        upload a 300,000-byte export to /incoming, which has 100,000 bytes of room
//   last-night upload nothing; the next step reads last night's export
//   http-put   upload the export to /archive over HTTP instead (curl -T to the http:// listener)
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "archive";

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

// The export to upload, in the folder: tonight's, or one too big for the room left.
string local = "export.csv";
if (which == "big")
{
    byte[] big = new byte[300_000];
    new Random(7).NextBytes(big);
    File.WriteAllBytes(local = "big.csv", big);
}

// surl-mcp: one store in memory behind an FTP and an HTTP listener; /archive full, 100,000 bytes a file elsewhere.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0", "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--allow-anonymous", "--allow-uploads", "--max-filesize", "100000", "--fault", "out-of-space;path=/archive" },
    ["files"] = new Dictionary<string, string>
    {
        ["archive/2026-11-19.csv"] = "id,total\n1,1180\n2,940\n",
        ["incoming/README.txt"] = "drop exports here\n",
    },
});
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string ftp = server.GetProperty("bound_urls")[0].GetString()!, http = server.GetProperty("bound_urls")[1].GetString()!;
string path = which switch
{
    "big" => "incoming/big.csv",
    "incoming" => "incoming/2026-11-20.csv",
    "last-night" => "archive/2026-11-19.csv",
    _ => "archive/2026-11-20.csv",
};

// curl-mcp: the uploader. Any nonzero exitCode is a failed upload.
if (which != "last-night")
{
    string[] argv = which == "http-put" ? ["-sS", "-f", "-w", "%{http_code} ", "-T", local, http + path] : ["-sS", "-T", local, ftp + path];
    CallToolResult uploaded = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = argv });
    using JsonDocument answer = JsonDocument.Parse(uploaded.Content.OfType<TextContentBlock>().Single().Text);
    string said = (answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!
        + answer.RootElement.GetProperty("standardError").GetProperty("content").GetString()!).Replace("\r", "").Trim();
    Console.WriteLine($"upload /{path}: exitCode {answer.RootElement.GetProperty("exitCode")}, tool error {(uploaded.IsError == true ? "yes" : "no")}{(said.Length > 0 ? ", " + said : "")}");
}

// curl-mcp: the next step, reading the same path over HTTP.
CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = http + path });
using (JsonDocument answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text))
{
    JsonElement body = answer.RootElement.GetProperty("body");
    string content = body.ValueKind == JsonValueKind.Object ? body.GetProperty("content").GetString()! : "";
    string what = content.Length == 0 ? "no body"
        : content == (which == "last-night" ? "id,total\n1,1180\n2,940\n" : File.ReadAllText(local)) ? "the export" : "something else";
    Console.WriteLine($"next step: statusCode {answer.RootElement.GetProperty("statusCode")}, tool error {(fetched.IsError == true ? "yes" : "no")}, {what}");
}

// surl-mcp: what the store holds at that path.
CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = "/" + path });
Console.WriteLine(read.IsError == true
    ? $"store: {read.Content.OfType<TextContentBlock>().First().Text}"
    : $"store: {read.StructuredContent!.Value.GetProperty("byte_count")} bytes at /{path}");

// surl-mcp: every time the rule acted, and on what.
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "fault_applied" } });
var applied = events.StructuredContent!.Value.GetProperty("events").EnumerateArray()
    .Select(e => $"{e.GetProperty("rule_id").GetString()} {e.GetProperty("fault_kind").GetString()} {e.GetProperty("target").GetString()}").ToList();
Console.WriteLine($"fault_applied: {(applied.Count == 0 ? "none" : string.Join(", ", applied))}");
return 0;
