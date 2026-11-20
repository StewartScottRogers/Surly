// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent asks an FTP server for a file's size and date with curl -I through curl-mcp, checks in
// surl-mcp's record that nothing was downloaded, and decides whether to fetch, over the official MCP SDK.
// Run: dotnet run check-file.cs [-- copy|missing|refused]
//   (no argument) curl -I report.csv: its size and date, and the commands the server received
//   copy          download the file with -R and read the time of the local copy
//   missing       curl -I on a file that is not there
//   refused       curl -I on a server that accepts no anonymous login
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "ask";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it.
string here = Directory.GetCurrentDirectory();

// The served folder: one file, dated 2 January 2024, 15:04:05 Coordinated Universal Time (UTC).
Directory.CreateDirectory("site");
File.WriteAllText("site/report.csv", "id,total\n1,10\n");
File.SetLastWriteTimeUtc("site/report.csv", new DateTime(2024, 1, 2, 15, 4, 5, DateTimeKind.Utc));

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

// surl-mcp: an FTP server over the folder; --allow-anonymous is left out for the "refused" run.
var surlArguments = new List<string> { "--directory", "site" };
if (which != "refused")
{
    surlArguments.Add("--allow-anonymous");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = surlArguments,
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string baseUrl = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "ask":
        JsonElement head = await Curl(["-s", "-I", baseUrl + "report.csv"]);
        Console.Write(Text(head, "standardOutput").Replace("\r\n", "\n"));

        // The commands the server received, from its own record.
        JsonElement exchanges = await Exchanges();
        string commands = string.Join(" ", exchanges.GetProperty("exchanges")[0].GetProperty("chunks").EnumerateArray()
            .Where(chunk => chunk.GetProperty("direction").GetString() == "received")
            .Select(chunk => chunk.GetProperty("data").GetString()!.Split(' ', '\r')[0])
            .Where(name => name is "MDTM" or "SIZE" or "RETR" or "EPSV" or "PASV"));
        Console.WriteLine($"commands: {commands}");
        break;
    case "copy":
        // -R gives the local copy the server's time; the folder curl-mcp writes in is its root.
        JsonElement copied = await Curl(["-s", "-R", "-o", "copy.csv", baseUrl + "report.csv"]);
        Console.WriteLine($"exit: {copied.GetProperty("exitCode")}");
        Console.WriteLine($"copy dated: {File.GetLastWriteTimeUtc("copy.csv"):yyyy-MM-ddTHH:mm:ssZ}");

        // Without -R the copy is dated when it was written.
        await Curl(["-s", "-o", "plain.csv", baseUrl + "report.csv"]);
        Console.WriteLine($"copy without -R dated 2024: {File.GetLastWriteTimeUtc("plain.csv").Year == 2024}");
        break;
    case "missing" or "refused":
        JsonElement failed = await Curl(["-s", "-I", baseUrl + (which == "missing" ? "missing.csv" : "report.csv")]);
        Console.WriteLine($"exit: {failed.GetProperty("exitCode")}, headers: {Text(failed, "standardOutput").Length}");
        break;
}

return 0;

// One curl-mcp call: curl with these arguments. The answer is text, JSON.
async Task<JsonElement> Curl(string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}

static string Text(JsonElement answer, string stream) => answer.GetProperty(stream).GetProperty("content").GetString()!;

// The exchanges surl-mcp recorded for the server, oldest first.
async Task<JsonElement> Exchanges()
{
    CallToolResult result = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId });
    return result.StructuredContent!.Value;
}
