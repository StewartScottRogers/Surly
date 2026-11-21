// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs a CI job's tests against surl-mcp with curl-mcp, over the official MCP SDK:
// start HTTP and SMTP on free ports, report the bound URLs, run the tests, stop the server,
// and exit with a code the job can act on.
// Run: dotnet run run-in-ci.cs [-- pass|wrong-password|port-taken]
#:package ModelContextProtocol.Core@*

using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "pass";

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

// port-taken: something else already holds the HTTP port the run asks for.
using var holder = new TcpListener(IPAddress.Loopback, 0);
holder.Start();
string httpListenUrl = mode == "port-taken"
    ? $"http://127.0.0.1:{((IPEndPoint)holder.LocalEndpoint).Port}"
    : "http://127.0.0.1:0";

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { httpListenUrl, "smtp://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "app@example.com:secret" },
    ["files"] = new Dictionary<string, string> { ["greeting.txt"] = "Welcome aboard.\n" },
});
JsonElement server = started.StructuredContent!.Value;

// A server that cannot start is a tool error carrying surl's exit code: fail the run with it.
if (started.IsError == true)
{
    int code = server.GetProperty("exit_code").GetInt32();
    Console.WriteLine($"server did not start: {server.GetProperty("exit_code_name").GetString()} ({code})");
    return code;
}

string serverId = server.GetProperty("server_id").GetString()!;
string httpUrl = server.GetProperty("bound_urls")[0].GetString()!, smtpUrl = server.GetProperty("bound_urls")[1].GetString()!;
Console.WriteLine($"HTTP_URL={httpUrl}");
Console.WriteLine($"SMTP_URL={smtpUrl}");

int result = 0;
try
{
    // The tests, through curl-mcp's curl tool; the first failure ends them.
    string password = mode == "wrong-password" ? "wrong" : "secret";
    if (await Curl("fetch", "-fsS", "-o", "greeting.txt", httpUrl + "greeting.txt") is var fetched and not 0)
    {
        result = fetched;
    }
    else
    {
        result = await Curl("mail", "-sS", "--crlf", "-u", $"app@example.com:{password}", "--mail-from", "app@example.com",
            "--mail-rcpt", "app@example.com", "-T", "greeting.txt", smtpUrl + "example.com");
    }

    // What the server recorded: the messages in the mailbox the tests mailed.
    CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?>
    {
        ["server_id"] = serverId, ["owner"] = "app@example.com",
    });
    JsonElement inbox = boxes.StructuredContent!.Value.GetProperty("mailboxes").EnumerateArray()
        .Single(box => box.GetProperty("name").GetString() == "INBOX");
    Console.WriteLine($"server stored {inbox.GetProperty("message_count")} message(s)");
}
finally
{
    // Stop the server whatever happened; then no server of this host is left running.
    CallToolResult stopped = await surl.CallToolAsync("stop_server", new Dictionary<string, object?> { ["server_id"] = serverId });
    CallToolResult left = await surl.CallToolAsync("list_servers", new Dictionary<string, object?>());
    Console.WriteLine($"stop_server: tool error {stopped.IsError == true}; servers left: {left.StructuredContent!.Value.GetProperty("servers").GetArrayLength()}");
    if (stopped.IsError == true)
    {
        result = result == 0 ? 1 : result;
    }
}

return result;

// One test: the curl tool with these arguments; prints and returns curl's exit code.
async Task<int> Curl(string label, params string[] arguments)
{
    CallToolResult answer = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    int exitCode = JsonDocument.Parse(answer.Content.OfType<TextContentBlock>().Single().Text).RootElement.GetProperty("exitCode").GetInt32();
    Console.WriteLine($"{label}: curl exit {exitCode}");
    return exitCode;
}
