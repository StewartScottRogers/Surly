// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs tasks one after another against one surl-mcp server. Each task gets an account of its
// own with add_account, works over HTTP with it through curl-mcp's curl tool, and loses the account
// (remove_account) and its files (delete_file) when it is done.
// Run: dotnet run task-accounts.cs -- <case>
//   right        two tasks in turn, each cleaned up after (the default)
//   overlap      two tasks at once: what one can read of the other's
//   no-standing  the server has no account of the agent's own
//   imap         a task account over IMAP
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "right";

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

// HTTP and IMAP; uploads allowed; the agent's own account keeps the server needing a login.
var arguments = new List<string> { "--allow-uploads" };
if (which != "no-standing")
{
    arguments.AddRange(["--user", "agent:agentpass"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0", "imap://127.0.0.1:0" }, ["arguments"] = arguments.ToArray(),
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string http = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string imap = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!;

switch (which)
{
    case "right":
        foreach (string task in new[] { "task-1", "task-2" })
        {
            await Surl("add_account", new() { ["user"] = task, ["password"] = $"{task}-secret" });
            string put = await Curl("--digest", "-u", $"{task}:{task}-secret", "-T", "result.txt", "-o", "put.out", "-w", "%{http_code}", $"{http}{task}.txt");
            string get = await Curl("--digest", "-u", $"{task}:{task}-secret", "-o", "get.out", "-w", "%{http_code}", $"{http}{task}.txt");
            Console.WriteLine($"{task}: account added, upload {put}, read back {get}");
            await Surl("delete_file", new() { ["path"] = $"/{task}.txt" });
            await Surl("remove_account", new() { ["user"] = task });
            Console.WriteLine($"{task}: file deleted, account removed");
        }

        Console.WriteLine($"task-1 logs in afterwards: HTTP {await Curl("--digest", "-u", "task-1:task-1-secret", "-o", "get.out", "-w", "%{http_code}", $"{http}task-1.txt")}");
        Console.WriteLine($"files left: {(await Surl("list_files", new() { ["path"] = "/" })).GetProperty("entries").GetArrayLength()}");
        JsonElement changes = await Surl("get_events", new() { ["store"] = "accounts" });
        Console.WriteLine("accounts store: " + string.Join(", ", changes.GetProperty("events").EnumerateArray().Select(e => $"{e.GetProperty("change")} {e.GetProperty("name")}")));
        return 0;

    case "overlap":
        await Surl("add_account", new() { ["user"] = "task-1", ["password"] = "task-1-secret" });
        await Surl("add_account", new() { ["user"] = "task-2", ["password"] = "task-2-secret" });
        await Curl("--digest", "-u", "task-1:task-1-secret", "-T", "result.txt", "-o", "put.out", $"{http}task-1.txt");
        string seen = await Curl("--digest", "-u", "task-2:task-2-secret", "-w", " (%{http_code})", $"{http}task-1.txt");
        Console.WriteLine($"task-2 reads task-1.txt: {seen.Replace("\n", "")}");
        return 1;

    case "no-standing":
        await Surl("put_file", new() { ["path"] = "/report.txt", ["text"] = "left behind\n" });
        await Surl("add_account", new() { ["user"] = "task-1", ["password"] = "task-1-secret" });
        Console.WriteLine($"while task-1 exists, GET with no login: HTTP {await Curl("-o", "get.out", "-w", "%{http_code}", $"{http}report.txt")}");
        await Surl("remove_account", new() { ["user"] = "task-1" });
        Console.WriteLine($"after the last account is removed, GET with no login: HTTP {await Curl("-o", "get.out", "-w", "%{http_code}", $"{http}report.txt")}");
        return 1;

    default:
        await Surl("add_account", new() { ["user"] = "task-1", ["password"] = "task-1-secret" });
        Console.WriteLine($"task-1 IMAP login, no mailbox named: curl exit {await CurlExit("-u", "task-1:task-1-secret", imap)}");
        Console.WriteLine($"task-1 IMAP INBOX: curl exit {await CurlExit("-u", "task-1:task-1-secret", $"{imap}INBOX?ALL")}");
        await Surl("remove_account", new() { ["user"] = "task-1" });
        Console.WriteLine($"task-1 IMAP login after remove_account: curl exit {await CurlExit("-u", "task-1:task-1-secret", imap)}");
        JsonElement logins = await Surl("get_events", new() { ["kinds"] = new[] { "login_judged" }, ["account"] = "task-1" });
        Console.WriteLine("login_judged: " + string.Join(", ", logins.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("is_accepted").GetRawText())));
        return 1;
}

// A surl-mcp tool on this server, answering in structured content.
async Task<JsonElement> Surl(string name, Dictionary<string, object?> toolArguments)
{
    toolArguments["server_id"] = id;
    CallToolResult result = await surl.CallToolAsync(name, toolArguments);
    return result.StructuredContent!.Value;
}

// The curl tool: what curl wrote to standard output, or its exit code. -s keeps the meter out.
async Task<string> Curl(params string[] curlArguments) =>
    (await CurlAnswer(curlArguments)).GetProperty("standardOutput").GetProperty("content").GetString()!;

async Task<int> CurlExit(params string[] curlArguments) =>
    (await CurlAnswer(curlArguments)).GetProperty("exitCode").GetInt32();

async Task<JsonElement> CurlAnswer(string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments.Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
