// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent's side of the conversation, as a C# file-based app: `dotnet run program.cs -- grants`
// (also: no-uploads, group). It starts surl-mcp and curl-mcp over standard input and output with the
// official MCP SDK, calls their tools as an agent would, and prints what the recipe's claims check.
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// grants: reader r, writer rw. no-uploads: the same without --allow-uploads. group: rw goes to a group.
string mode = args[0];

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

// surl-mcp: one server, its options as surl's own words. --share serves a folder as a share and
// --share-access says who may read (r) or also write (rw) it.
string folder = mode.Replace("-", "");
var serverOptions = new List<string>
{
    "--share", $"reports={folder}/reports", "--auth", "ntlmv1",
    "-u", "reader:r3ad", "-u", "writer:wr1te", "-u", "carol:c4rol",
};
if (mode == "grants") serverOptions.AddRange(["--share", "other=grants/other"]);
if (mode != "no-uploads") serverOptions.Add("--allow-uploads");
if (mode == "group") serverOptions.AddRange(["--group", "editors=writer", "--share-access", "reports=reader:r,@editors:rw"]);
else serverOptions.AddRange(["--share-access", "reports=reader:r,writer:rw"]);
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smb://127.0.0.1:0" },
    ["arguments"] = serverOptions,
});
string smb = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// curl-mcp's curl tool takes curl's own arguments: -T uploads a file from the confined folder.
if (mode == "grants")
{
    await Curl("writer uploads", "writer:wr1te", "reports/new.txt", "-T", "new.txt");
    await Curl("reader reads", "reader:r3ad", "reports/q1.txt");
    await Curl("reader reads new", "reader:r3ad", "reports/new.txt");
    await Curl("reader uploads", "reader:r3ad", "reports/draft.txt", "-T", "draft.txt");
    await Curl("writer reads draft", "writer:wr1te", "reports/draft.txt");
    await Curl("carol reads", "carol:c4rol", "reports/q1.txt");
    await Curl("carol uploads", "carol:c4rol", "reports/carol.txt", "-T", "draft.txt");
    await Curl("carol reads other", "carol:c4rol", "other/o.txt");
    await Curl("carol uploads other", "carol:c4rol", "other/draft.txt", "-T", "draft.txt");
    await Curl("wrong password", "writer:wrong", "reports/q1.txt");
}
else
{
    await Curl("writer uploads", "writer:wr1te", "reports/new.txt", "-T", "new.txt");
    if (mode == "group") await Curl("reader uploads", "reader:r3ad", "reports/draft.txt", "-T", "draft.txt");
    else await Curl("reader reads", "reader:r3ad", "reports/q1.txt");
}

return 0;

// One curl run as one account: prints the exit code, then the first line of standard output or error.
async Task Curl(string label, string login, string path, params string[] extra)
{
    string[] curlArgs = ["-s", "-S", "-u", login, .. extra, smb + path];
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement root = answer.RootElement;
    string text = Content(root, "standardOutput") ?? Content(root, "standardError") ?? "";
    Console.WriteLine($"{label}: exit {root.GetProperty("exitCode").GetInt32()} {text.Trim()}".TrimEnd());
}

static string? Content(JsonElement root, string stream) =>
    root.GetProperty(stream).ValueKind == JsonValueKind.Object && root.GetProperty(stream).GetProperty("content").GetString() is { Length: > 0 } s ? s : null;
