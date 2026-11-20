// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent lists a blob container with surl-mcp and curl-mcp, three servers in one run: one that allows listings,
// one that does not, and one that also allows anonymous callers. Over the official MCP SDK.
// Run: dotnet run program.cs
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. server.pem and
// server.key, a certificate and key for 127.0.0.1, are in it.
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

// surl-mcp: three servers on the same four blobs, in memory. The container photos is the first folder
// of each path in files.
async Task<(string Id, int Port)> Start(params string[] more)
{
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { "az://127.0.0.1:0" },
        ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key" }.Concat(more).ToArray(),
        ["files"] = new Dictionary<string, string>
        {
            ["photos/readme.txt"] = "read me\n",
            ["photos/2025/dec.jpg"] = "december\n",
            ["photos/2026/jan.jpg"] = "january\n",
            ["photos/2026/feb.jpg"] = "february\n",
        },
    });
    JsonElement answer = started.StructuredContent!.Value;
    return (answer.GetProperty("server_id").GetString()!, new Uri(answer.GetProperty("bound_urls")[0].GetString()!).Port);
}

string[] accounts = { "--user", "devstoreaccount1:c2VjcmV0", "--user", ":t0ken-abc" };
(string mainId, int mainPort) = await Start(accounts.Append("--list-directories").ToArray());
(_, int noListingPort) = await Start(accounts);
(_, int openPort) = await Start("--user", "devstoreaccount1:c2VjcmV0", "--list-directories", "--allow-anonymous");

string[] token = { "--oauth2-bearer", "t0ken-abc" };
await Fetch("prefix", mainPort, "devstoreaccount1/photos/2026/", token);
await Fetch("top", mainPort, "devstoreaccount1/photos/", token);
await Fetch("empty", mainPort, "devstoreaccount1/photos/2030/", token);
await Fetch("no container", mainPort, "devstoreaccount1/videos/", token);
await Fetch("shared key", mainPort, "devstoreaccount1/photos/2026/", "-u", "devstoreaccount1:c2VjcmV0");
await Fetch("no token", mainPort, "devstoreaccount1/photos/2026/");
await Fetch("listing off", noListingPort, "devstoreaccount1/photos/", token);
await Fetch("listing off, get", noListingPort, "devstoreaccount1/photos/readme.txt", token);
await Fetch("anonymous", openPort, "devstoreaccount1/photos/2026/");

// surl-mcp can also list the store itself, which asks no one's permission: the same two names.
CallToolResult files = await surl.CallToolAsync("list_files", new Dictionary<string, object?> { ["server_id"] = mainId, ["path"] = "/photos/2026" });
string names = string.Join(", ", files.StructuredContent!.Value.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("name").GetString()));
Console.WriteLine($"list_files: [{names}]");
return 0;

// curl-mcp's curl tool runs curl's own arguments and answers in text, as JSON. --cacert trusts the server's
// certificate. The program prints the exit code and the lines of standard output, or the Azure error in
// standard error.
async Task Fetch(string label, int port, string path, params string[] more)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-sS", "--cacert", "server.pem" }.Concat(more).Append($"az://127.0.0.1:{port}/{path}").ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string output = answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r", "");
    string error = answer.RootElement.GetProperty("standardError").GetProperty("content").GetString()!;
    string lines = string.Join(", ", output.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    string firstLine = error.Split('\n')[0].Replace("\r", "");
    Console.WriteLine($"{label}: exit {answer.RootElement.GetProperty("exitCode")} [{lines}] {firstLine.Replace("curl: (22) ", "")}".TrimEnd());
}
