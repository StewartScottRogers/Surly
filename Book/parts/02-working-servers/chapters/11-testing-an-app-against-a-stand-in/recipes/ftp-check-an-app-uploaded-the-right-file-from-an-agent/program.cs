// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs an app that uploads a nightly export over FTP against a surl-mcp server, using curl-mcp's curl tool
// as the app, and compares what arrived with the expected file through surl-mcp's read_file.
// Run: dotnet run program.cs -- <case>
//   matches        the app uploads the right export, the check passes
//   over-http      the same file, fetched over HTTP from the same server
//   broken-app     the app uploads the export without its last row, and still finishes
//   mismatch       the same app, then the check: it fails
//   missing        the check of a file the app never uploaded
//   uploads-off    a server that takes no uploads: the app
//   login-needed   a server with no anonymous login: the app
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args[0];
const string Expected = "id,region,total\n1,north,42\n2,south,17\n";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. The app writes nightly.csv here.
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

// surl-mcp: the stand-in, one in-memory store written over FTP and served over HTTP, each on a free port of its own.
var serverArguments = new List<string>();
if (which != "login-needed") serverArguments.Add("--allow-anonymous");
if (which != "uploads-off") serverArguments.Add("--allow-uploads");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0", "http://127.0.0.1:0" },
    ["arguments"] = serverArguments,
});
JsonElement bound = started.StructuredContent!.Value.GetProperty("bound_urls");
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string ftp = bound[0].GetString()!;
string http = bound[1].GetString()!;

switch (which)
{
    case "matches":
        await Export(broken: false);
        await Check("/exports/nightly.csv");
        break;
    case "over-http":
        await Export(broken: false);
        Console.Write((await Curl(["-s", http + "exports/nightly.csv"])).Output);
        break;
    case "broken-app":
        await Export(broken: true);
        break;
    case "mismatch":
        await Export(broken: true);
        await Check("/exports/nightly.csv");
        break;
    case "missing":
        await Export(broken: false);
        await Check("/exports/nightly-2.csv");
        break;
    default:
        await Export(broken: false);
        break;
}

return 0;

// The app: writes tonight's export and uploads it with -T, making the exports folder if need be with
// --ftp-create-dirs. Given broken, it leaves out the last row, as a bug would.
async Task Export(bool broken)
{
    File.WriteAllText("nightly.csv", broken ? "id,region,total\n1,north,42\n" : Expected);
    (string exit, _, string said) = await Curl(["-sS", "--ftp-create-dirs", "-T", "nightly.csv", ftp + "exports/nightly.csv"]);
    Console.WriteLine($"app: exit {exit}{(said == "" ? "" : $" {said}")}");
}

// The check: read_file reads the file from the server's store, with no client and no login.
async Task Check(string path)
{
    CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["path"] = path,
    });
    if (read.IsError == true)
    {
        Console.WriteLine($"read_file: {read.Content.OfType<TextContentBlock>().Single().Text}");
        return;
    }

    JsonElement file = read.StructuredContent!.Value;
    string arrived = file.GetProperty("text").GetString()!;
    Console.WriteLine(arrived == Expected
        ? "matches expected"
        : $"differs from expected: expected {Expected.Length} bytes, arrived {file.GetProperty("byte_count")}");
}

// The curl tool runs curl's own arguments, one list item each, with no shell. A refusal is an answer with a
// non-zero exitCode, not a failure of the call.
async Task<(string Exit, string Output, string Said)> Curl(string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string Text(string name) => answer.GetProperty(name).ValueKind == JsonValueKind.Null
        ? "" : answer.GetProperty(name).GetProperty("content").GetString()!;
    return (answer.GetProperty("exitCode").ToString(), Text("standardOutput"), Text("standardError").Trim());
}
