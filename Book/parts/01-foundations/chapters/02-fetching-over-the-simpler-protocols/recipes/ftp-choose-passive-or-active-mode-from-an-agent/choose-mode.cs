// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent fetches one file from an FTP server four ways with curl-mcp, and reads from surl-mcp
// which data-connection command each way sent, over the official MCP SDK.
// Run: dotnet run choose-mode.cs [-- verbose|other|refused]
//   (no argument) default, --disable-epsv, -P -, -P - --disable-eprt
//   verbose       the same default fetch with -v, to read the commands from the client's side
//   other         active mode naming an address that is not the client's
//   refused       the same fetch from a server that accepts no anonymous login
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "modes";

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

// surl-mcp: an FTP server on a free port with one file in memory. --allow-anonymous is surl's option
// of that name, passed as surl's own words; the "refused" run leaves it out.
var surlArguments = new List<string>();
if (which != "refused")
{
    surlArguments.Add("--allow-anonymous");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = surlArguments,
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "hello.txt";

int lastExchange = 0;
switch (which)
{
    case "modes":
        await Fetch("default", []);
        await Fetch("no EPSV", ["--disable-epsv"]);
        await Fetch("active", ["-P", "-"]);
        await Fetch("active, no EPRT", ["-P", "-", "--disable-eprt"]);
        break;
    case "verbose":
        await Fetch("verbose", ["-v"]);
        break;
    case "other":
        await Fetch("other address", ["-P", "192.0.2.1"]);
        break;
    case "refused":
        await Fetch("refused", []);
        break;
}

return 0;

// One curl-mcp call, then the commands the newest exchange of the server holds.
async Task Fetch(string label, string[] options)
{
    // -s keeps curl's progress meter out of the answer.
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = options.Prepend("-s").Append(url).ToArray(),
    });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string stderr = answer.GetProperty("standardError").GetProperty("content").GetString()!;

    CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?>
    {
        ["server_id"] = serverId,
        ["after_exchange_id"] = lastExchange,
    });
    JsonElement exchange = recorded.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().Last();
    lastExchange = exchange.GetProperty("exchange_id").GetInt32();
    string commands = string.Join(" ", exchange.GetProperty("chunks").EnumerateArray()
        .Where(chunk => chunk.GetProperty("direction").GetString() == "received")
        .Select(chunk => chunk.GetProperty("data").GetString()!.Split(' ', '\r')[0])
        .Where(name => name is "EPSV" or "PASV" or "EPRT" or "PORT"));

    Console.WriteLine($"{label}: {commands} -> exit {answer.GetProperty("exitCode")}, {answer.GetProperty("standardOutput").GetProperty("content").GetString()!.TrimEnd()}");
    if (which == "verbose")
    {
        Console.WriteLine(stderr.Split('\n').Select(line => line.TrimEnd('\r')).First(line => line.StartsWith("> EPSV", StringComparison.Ordinal)));
    }
}
