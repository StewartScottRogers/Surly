// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent serves a firmware image over TFTP and HTTP with surl-mcp and fetches it both ways with curl-mcp.
// Run: dotnet run firmware-over-http.cs -- <case>
//   fetch     a 1300-byte image over both, compared with what the server holds (the default)
//   exact     an image of exactly 1024 bytes, two full blocks
//   missing   ask both for an image that is not there
//   write     send a file to both
#:package ModelContextProtocol.Core@2.4.0

using System.Security.Cryptography;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "fetch";

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

// surl-mcp: one server, two listeners, one store. Binary content goes in as base64.
byte[] image = RandomNumberGenerator.GetBytes(which == "exact" ? 1024 : 1300);
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "tftp://127.0.0.1:0", "http://127.0.0.1:0" },
    ["files_base64"] = new Dictionary<string, string> { ["firmware.bin"] = Convert.ToBase64String(image) },
});
JsonElement server = started.StructuredContent!.Value;
string serverId = server.GetProperty("server_id").GetString()!;
string tftp = server.GetProperty("bound_urls")[0].GetString()!;
string http = server.GetProperty("bound_urls")[1].GetString()!;

// Whether any call was a tool error: the program exits 1 if one was.
bool toolError = false;

// curl-mcp's transfer answers in text, as JSON; a binary body comes back as base64.
async Task<JsonElement> Transfer(string url)
{
    CallToolResult result = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url });
    toolError |= result.IsError == true;
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}

switch (which)
{
    case "missing":
        JsonElement lost = await Transfer(tftp + "nope.bin");
        Console.WriteLine($"tftp: {lost.GetProperty("failure").GetProperty("kind")}, {lost.GetProperty("failure").GetProperty("text")}");
        JsonElement gone = await Transfer(http + "nope.bin");
        Console.WriteLine($"http: {gone.GetProperty("statusCode")} failure {gone.GetProperty("failure").GetRawText()}");
        break;

    case "write":
        // The curl tool takes a command line; a file to send has to be inside CURL_MCP_ROOT.
        foreach (string url in new[] { tftp + "up.txt", http + "up.txt" })
        {
            CallToolResult put = await curl.CallToolAsync("curl", new Dictionary<string, object?>
            {
                ["args"] = new[] { "-s", "-f", "-T", "up.txt", url },
            });
            toolError |= put.IsError == true;
            JsonElement answer = JsonDocument.Parse(put.Content.OfType<TextContentBlock>().Single().Text).RootElement;
            Console.WriteLine($"{new Uri(url).Scheme}: exit {answer.GetProperty("exitCode")}");
        }

        break;

    default:
        JsonElement viaTftp = await Transfer(tftp + "firmware.bin");
        JsonElement viaHttp = await Transfer(http + "firmware.bin");
        string[] bodies = new[] { viaTftp, viaHttp }.Select(a => a.GetProperty("body").GetProperty("content").GetString()!).ToArray();
        Console.WriteLine($"tftp: status {viaTftp.GetProperty("statusCode")}, {viaTftp.GetProperty("body").GetProperty("totalBytes")} bytes, base64 {viaTftp.GetProperty("body").GetProperty("isBase64")}");
        Console.WriteLine($"http: status {viaHttp.GetProperty("statusCode")}, {viaHttp.GetProperty("body").GetProperty("totalBytes")} bytes, base64 {viaHttp.GetProperty("body").GetProperty("isBase64")}");
        Console.WriteLine(bodies[0] == bodies[1] ? "identical" : "different");

        // What the server holds, read back from its store.
        CallToolResult held = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = "/firmware.bin" });
        string stored = held.StructuredContent!.Value.GetProperty("base64").GetString()!;
        Console.WriteLine(stored == bodies[0] ? "same as the store" : "not the store");
        break;
}

Console.WriteLine($"tool error: {(toolError ? "yes" : "no")}");
return toolError ? 1 : 0;
