// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A deploy job's SSH key login is refused. An agent reads the server's login events through
// surl-mcp and says what was wrong. Run: dotnet run program.cs -- <case>
//   solution      the job signed with a key that is not the pair of the public key it offered
//   unauthorized-key  the job used a key pair the server was never given
//   no-keys       the server takes passwords only
//   unknown-user  the job logged in as a user the server has no keys for
//   same-record   a wrong key and a password the server never takes leave the same record
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "solution";

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

// The server: one user, deploy-a, with one authorized key. The agent knows this much.
string[] knownUsers = ["deploy-a"];
// The case "no-keys" starts a server that takes passwords only, with one account.
string[] serverOptions = which == "no-keys"
    ? ["--hostkey", "host_key", "--user", "deploy-a:secret"]
    : ["--hostkey", "host_key", "--authorized-keys", "deploy-a:deploy_a.pub"];
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "sftp://127.0.0.1:0" },
    ["arguments"] = serverOptions,
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "hello.txt";

switch (which)
{
    case "solution":
    {
        await Job("job", "deploy-a", "--key", "deploy_b", "--pubkey", "deploy_a.pub");
        string record = await Record(1);
        Console.WriteLine($"  record: {record}");
        // The agent has the key the server was given: log in with it as a control.
        await Job("control", "deploy-a", "--key", "deploy_a", "--pubkey", "deploy_a.pub");
        string control = await Record(2);
        Console.WriteLine($"  control record: {control}");
        Console.WriteLine(control.Contains("publickey accepted") && record.Contains("publickey refused")
            ? "  verdict: the key was offered and refused after signing: deploy-a's public key with a private key that is not its pair"
            : "  verdict: not found");
        break;
    }
    case "unknown-user":
    {
        await Job("job", "deploy-c", "--key", "deploy_a", "--pubkey", "deploy_a.pub");
        string record = await Record(1);
        Console.WriteLine($"  record: {record}");
        string user = record.Split(':')[0];
        Console.WriteLine(knownUsers.Contains(user)
            ? "  verdict: the user is known"
            : $"  verdict: the user is wrong: this server has authorized keys for {string.Join(", ", knownUsers)}, not {user}");
        break;
    }
    case "unauthorized-key":
    {
        // A key pair the server was never given.
        await Job("job", "deploy-a", "--key", "deploy_b", "--pubkey", "deploy_b.pub");
        string record = await Record(1);
        Console.WriteLine($"  record: {record}");
        Console.WriteLine($"  a publickey login was judged: {record.Contains("publickey")}");
        break;
    }
    case "no-keys":
    {
        // A server that takes passwords only, and a job that offers a key it would accept elsewhere.
        await Job("job", "deploy-a", "--key", "deploy_a", "--pubkey", "deploy_a.pub");
        string record = await Record(1);
        Console.WriteLine($"  record: {record}");
        Console.WriteLine($"  a publickey login was judged: {record.Contains("publickey")}");
        break;
    }
    case "same-record":
    {
        await Job("wrong key", "deploy-a", "--key", "deploy_b", "--pubkey", "deploy_b.pub");
        await Job("password", "deploy-a:secret");
        string first = await Record(1), second = await Record(2);
        Console.WriteLine($"  record: {first}");
        Console.WriteLine($"  record: {second}");
        Console.WriteLine($"  the two records are the same: {first == second}");
        break;
    }
}

return 0;

// The job: curl-mcp's curl tool logs in with the options a deploy job would use and fetches a file.
// Its own answer, exit code 67 for a refused login, is all the job tells.
async Task<string> Job(string label, string user, params string[] more)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "-k", "-u", user.Contains(':') ? user : user + ":" }.Concat(more).Append(url).ToArray(),
    });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    int code = answer.GetProperty("exitCode").GetInt32();
    Console.WriteLine($"{label}: curl exit {code}{(code == 0 ? ", " + answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim() : "")}");
    return code.ToString();
}

// What the server recorded for the nth connection, as the account and each login in the order tried.
// The exchange ends a moment after curl has its answer: wait for it, for at most 15 s, before reading.
async Task<string> Record(int exchange)
{
    for (DateTime deadline = DateTime.UtcNow.AddSeconds(15); ; await Task.Delay(100))
    {
        JsonElement[] events = (await surl.CallToolAsync("get_events", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["kinds"] = new[] { "exchange_ended", "login_judged" },
        })).StructuredContent!.Value.GetProperty("events").EnumerateArray().Where(e => e.GetProperty("exchange_id").GetInt64() == exchange).ToArray();
        if (events.Any(e => e.GetProperty("kind").GetString() == "exchange_ended") || DateTime.UtcNow > deadline)
        {
            var logins = events.Where(e => e.GetProperty("kind").GetString() == "login_judged").ToList();
            return logins.Count == 0
                ? "no login judged"
                : $"{logins[0].GetProperty("account").GetString()}: " + string.Join(", ", logins.Select(e => $"{e.GetProperty("mechanism").GetString()} {(e.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused")}"));
        }
    }
}
