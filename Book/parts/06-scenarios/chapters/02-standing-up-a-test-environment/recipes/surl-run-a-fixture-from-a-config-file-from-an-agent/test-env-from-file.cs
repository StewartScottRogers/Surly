// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts the test environment test-env.conf describes with surl-mcp, which does not read
// config files itself, tries each server once with curl-mcp, and checks what the server recorded.
// Run: dotnet run test-env-from-file.cs -- <case>
//   start    read the file into start_server's arguments and try the servers (the default)
//   config   pass --config test-env.conf to start_server instead
//   <file>   read another file in place of test-env.conf, such as outside.conf
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "start";

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

// The file, read the way surl reads it: url lines are listen URLs, every other line an option.
List<string> listenUrls = [], arguments = [];
foreach (string line in File.ReadLines(which is "start" or "config" ? "test-env.conf" : which).Select(l => l.Trim()))
{
    if (line.Length == 0 || line.StartsWith('#'))
    {
        continue;
    }

    int gap = line.IndexOfAny([' ', '\t', '=']);
    string name = gap < 0 ? line : line[..gap];
    string rest = gap < 0 ? "" : line[gap..].TrimStart(' ', '\t', '=');
    string value = rest.StartsWith('"') ? rest[1..rest.IndexOf('"', 1)] : rest.Split('#')[0].Trim();
    if (name == "url")
    {
        listenUrls.Add(value);
    }
    else
    {
        arguments.Add("--" + name);
        if (value.Length > 0)
        {
            arguments.Add(value);
        }
    }
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = listenUrls,
    ["arguments"] = which == "config" ? new List<string> { "--config", "test-env.conf" } : arguments,
});
if (started.IsError == true)
{
    Console.WriteLine(started.Content.OfType<TextContentBlock>().Single().Text);
    Console.WriteLine("tool error: yes");
    return 1;
}

JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string[] urls = server.GetProperty("bound_urls").EnumerateArray().Select(u => u.GetString()!).ToArray();
Console.WriteLine($"started {listenUrls.Count} listeners with: {string.Join(" ", arguments)}");

// The tests, with curl-mcp's curl tool: HTTP and FTP share the directory; SMTP takes the app's mail.
int before = await MessageCount();
Console.WriteLine((await Curl(["-fsS", "-u", "tester:secret", urls[0] + "hello.txt"])).TrimEnd());
await Curl(["-sS", "-u", "tester:secret", "-T", "-", urls[1] + "upload.txt"], "uploaded over FTP\n");
Console.WriteLine((await Curl(["-fsS", "-u", "tester:secret", urls[0] + "upload.txt"])).TrimEnd());
await Curl(["-sS", "-u", "app@example.com:secret", "--mail-from", "app@example.com", "--mail-rcpt", "app@example.com",
    "-T", "-", urls[2] + "example.com"], "Subject: Ready\r\n\r\nThe environment is up.\r\n");
Console.WriteLine($"stored: {await MessageCount() - before} more message in INBOX");
return 0;

// One curl command line; curl-mcp answers in text, as JSON. A failure stops the run.
async Task<string> Curl(string[] curlArgs, string? stdin = null)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs, ["stdin"] = stdin });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    if (answer.GetProperty("exitCode").GetInt32() != 0)
    {
        Console.WriteLine($"curl exit {answer.GetProperty("exitCode")}: {answer.GetProperty("standardError").GetProperty("content").GetString()!.Trim()}");
        Environment.Exit(answer.GetProperty("exitCode").GetInt32());
    }

    return answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
}

// The app's INBOX, read from the server's side; the directory keeps mail from earlier runs.
async Task<int> MessageCount()
{
    CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "app@example.com" });
    return boxes.StructuredContent!.Value.GetProperty("mailboxes")[0].GetProperty("message_count").GetInt32();
}
