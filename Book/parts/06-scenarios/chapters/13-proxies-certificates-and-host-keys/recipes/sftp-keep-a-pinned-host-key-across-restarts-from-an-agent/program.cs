// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests that a pinned SFTP host key survives restarts (surl-mcp start_server and stop_server)
// and that a changed key is refused (curl-mcp's curl tool with --hostpubsha256).
// Run: dotnet run program.cs -- <scenario>
//   kept       three starts with host_key; fetch with the pin after each
//   rekeyed    one start with other_key; fetch with host_key's pin
//   throwaway  two starts with --throwaway-hostkey; compare the keys served
//   nokey      a start with no host key at all
#:package ModelContextProtocol.Core@2.4.0

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string scenario = args[0];

// Both hosts are confined to this folder, which holds the keys ssh-keygen made.
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

// The pin, made once from host_key.pub before any server runs, as ssh-keygen -l prints it.
byte[] blob = Convert.FromBase64String(File.ReadAllText("host_key.pub").Split(' ')[1]);
string pin = Convert.ToBase64String(SHA256.HashData(blob)).TrimEnd('=');

// One start: the key option, -v so the log names the key served, one account and one file.
async Task<JsonElement> Start(params string[] keyOption)
{
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { "sftp://127.0.0.1:0" },
        ["arguments"] = keyOption.Concat(["-v", "--user", "alice:s3cret"]).ToArray(),
        ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
    });
    return started.StructuredContent!.Value;
}

// The fingerprint a start's log_lines say it served.
string Served(JsonElement server) => server.GetProperty("log_lines").EnumerateArray()
    .Select(line => Regex.Match(line.GetString()!, @"--hostpubsha256 (\S+?)[ )]")).First(m => m.Success).Groups[1].Value;

// A fetch with the pin through curl-mcp's curl tool: the file, or the exit code and first error line.
async Task<string> Fetch(JsonElement server)
{
    string url = server.GetProperty("bound_urls")[0].GetString()!;
    CallToolResult fetched = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-sS", "--hostpubsha256", pin, "-u", "alice:s3cret", url + "hello.txt" },
    });
    JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    return answer.GetProperty("exitCode").GetInt32() == 0
        ? answer.GetProperty("standardOutput").GetProperty("content").GetString()!.TrimEnd()
        : $"exitCode {answer.GetProperty("exitCode")}: {answer.GetProperty("standardError").GetProperty("content").GetString()!.Split('\n')[0].TrimEnd('\r')}";
}

async Task Stop(JsonElement server) =>
    await surl.CallToolAsync("stop_server", new Dictionary<string, object?> { ["server_id"] = server.GetProperty("server_id").GetString() });

switch (scenario)
{
    case "kept":
        var served = new List<string>();
        for (int start = 1; start <= 3; start++)
        {
            JsonElement server = await Start("--hostkey", "host_key");
            Console.WriteLine($"start {start}: {await Fetch(server)}");
            served.Add(Served(server));
            await Stop(server);
        }

        Console.WriteLine(served.Distinct().Count() == 1 ? "one key served" : "keys differ");
        return 0;
    case "rekeyed":
        JsonElement rekeyed = await Start("--hostkey", "other_key");
        Console.WriteLine(await Fetch(rekeyed));
        // What the server judged: the connection ended, and no login was ever judged on it.
        string id = rekeyed.GetProperty("server_id").GetString()!;
        await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["kinds"] = new[] { "exchange_ended" }, ["timeout_ms"] = 5000,
        });
        CallToolResult logins = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["kinds"] = new[] { "login_judged" },
        });
        Console.WriteLine($"login_judged events: {logins.StructuredContent!.Value.GetProperty("events").GetArrayLength()}");
        return 1;
    case "throwaway":
        JsonElement first = await Start("--throwaway-hostkey");
        await Stop(first);
        JsonElement second = await Start("--throwaway-hostkey");
        Console.WriteLine(first.GetProperty("log_lines")[0].GetString());
        Console.WriteLine(Served(first) == Served(second) ? "same key" : "a different key after the restart");
        return 0;
    default:
        JsonElement refused = await Start();
        Console.WriteLine($"exit_code: {refused.GetProperty("exit_code")} ({refused.GetProperty("exit_code_name")})");
        Console.WriteLine(refused.GetProperty("lines")[0].GetString());
        return refused.GetProperty("exit_code").GetInt32();
}
