// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a surl-mcp SFTP server with one authorized key file per deploy user and logs in
// as each through curl-mcp. The keys come from setup: in recipe.yml: host_key, deploy_a, deploy_a2
// and deploy_b, each with a .pub half, and a_keys, two lines.
// Run: dotnet run keys.cs [-- own-key|second-user|a-key-as-b|b-key-as-a|second-key|second-key-as-b|unknown-user|events]
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "own-key";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it, and the key
// files lie in it.
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

// One server, two deploy users: deploy-a has the two keys in a_keys, deploy-b the one in deploy_b.pub.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "sftp://127.0.0.1:0" },
    ["arguments"] = new[] { "--hostkey", "host_key", "--authorized-keys", "deploy-a:a_keys", "--authorized-keys", "deploy-b:deploy_b.pub" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "hello.txt";

switch (which)
{
    case "own-key": Console.Write(await Login("deploy-a", "deploy_a")); break;
    case "second-user": Console.Write(await Login("deploy-b", "deploy_b")); break;
    case "a-key-as-b": Console.Write(await Login("deploy-b", "deploy_a")); break;
    case "b-key-as-a": Console.Write(await Login("deploy-a", "deploy_b")); break;
    case "second-key": Console.Write(await Login("deploy-a", "deploy_a2")); break;
    case "second-key-as-b": Console.Write(await Login("deploy-b", "deploy_a2")); break;
    case "unknown-user": Console.Write(await Login("deploy-c", "deploy_a")); break;
    case "events":
        await Login("deploy-a", "deploy_a");
        await Login("deploy-b", "deploy_a");
        CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = server, ["kinds"] = new[] { "login_judged" } });
        var judged = events.StructuredContent!.Value.GetProperty("events").EnumerateArray().ToList();
        foreach (string user in new[] { "deploy-a", "deploy-b" })
        {
            var mine = judged.Where(e => e.GetProperty("account").GetString() == user).ToList();
            string accepted = string.Join(", ", mine.Where(e => e.GetProperty("is_accepted").GetBoolean()).Select(e => e.GetProperty("mechanism").GetString()));
            Console.WriteLine($"{user}: accepted by [{accepted}], refused logins: {mine.Any(e => !e.GetProperty("is_accepted").GetBoolean())}");
        }
        break;
}
return 0;

// curl-mcp's curl tool: a key login as user. -k skips the host key check, as in the console recipe.
// The answer is the exit code, then the file or curl's message.
async Task<string> Login(string user, string key)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "-k", "-u", user + ":", "--key", key, "--pubkey", key + ".pub", url },
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    int code = answer.RootElement.GetProperty("exitCode").GetInt32();
    JsonElement text = answer.RootElement.GetProperty(code == 0 ? "standardOutput" : "standardError");
    return $"{user}, {key}: exit code {code}\n{text.GetProperty("content").GetString()}";
}
