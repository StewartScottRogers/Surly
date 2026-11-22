// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent's upload is refused; it reads the status and the Allow header and says what would fix it.
// Run: dotnet run program.cs [-- <case>]
//   post        POST with a login to a server that takes no uploads (the default)
//   post-on     POST with a login to a server started with --allow-uploads
//   put-on      PUT with a login to that server, as the explanation says
//   put-off     PUT with a login to the server that takes no uploads
//   no-login    POST with no login to the server that takes no uploads
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "post";

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

// The server: one account, and uploads only in the -on cases.
bool uploads = which.EndsWith("-on");
var arguments = new List<string> { "--user", "alice:s3cret" };
if (uploads) arguments.Add("--allow-uploads");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = arguments,
});
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "notes.txt";

// The request, through curl-mcp's curl tool: -D - puts the response heads on standard output.
// --digest -u logs in; curl first sends the request without the login and answers the 401 challenge.
string method = which.StartsWith("put") ? "PUT" : "POST";
var curlArgs = new List<string> { "-s", "-D", "-", "--max-time", "10" };
if (which != "no-login") curlArgs.AddRange(["--digest", "-u", "alice:s3cret"]);
curlArgs.AddRange(method == "PUT" ? ["-T", "notes.txt"] : ["-d", "a note"]);
curlArgs.Add(url);
CallToolResult sent = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
JsonElement answer = JsonDocument.Parse(sent.Content.OfType<TextContentBlock>().Single().Text).RootElement;
string output = answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r\n", "\n");

// The last head is the final answer (a 401 challenge may come before it). Each refusal's short
// text body follows its head on standard output, so only the parts that start HTTP/ are heads.
string head = output.Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Last(part => part.StartsWith("HTTP/"));
string[] lines = head.Split('\n');
int status = int.Parse(lines[0].Split(' ')[1]);
string? allow = lines.Skip(1).FirstOrDefault(l => l.StartsWith("Allow:", StringComparison.OrdinalIgnoreCase))?[6..].Trim();
Console.WriteLine($"{method} {(which == "no-login" ? "without" : "with")} a login, uploads {(uploads ? "on" : "off")}: {lines[0][9..]}");
Console.WriteLine($"Allow: {allow ?? "(none)"}");

// What the agent makes of it.
string explanation = status switch
{
    401 => "the server wants a login before it looks at the method: log in and send it again",
    405 when allow?.Split(", ").Contains("PUT") == true => "this server takes uploads, but not by " + method + ": send the file with PUT",
    405 => "this server takes no uploads: it must be started with --allow-uploads, and then the file sent with PUT and a login",
    201 or 204 => "uploaded",
    _ => "not an answer this agent knows",
};
Console.WriteLine($"explanation: {explanation}");

if (status is 201 or 204)
{
    // Read the file back from the server's store, as the agent's check that it landed.
    CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?>
    {
        ["server_id"] = started.StructuredContent!.Value.GetProperty("server_id").GetString(),
        ["path"] = "/notes.txt",
    });
    Console.Write($"stored: {read.StructuredContent!.Value.GetProperty("text").GetString()}");
}

return status is 201 or 204 ? 0 : 1;
