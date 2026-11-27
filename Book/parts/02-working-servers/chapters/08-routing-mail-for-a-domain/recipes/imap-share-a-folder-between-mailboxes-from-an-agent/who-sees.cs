// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent checks who sees a shared folder: surl-mcp runs an IMAP server that shares team with Ann
// and Bob, and the agent logs in as each mailbox in turn with curl-mcp's curl tool and reports
// whether the folder is listed and what it holds.
// Run: dotnet run who-sees.cs [-- <case>]
//   (no argument)  Ann appends the rota to team, then each mailbox in turn lists and counts it
//   store          the mailboxes list_mailboxes names for each owner
//   read           Carol tries to read team's first message
//   typo           a member's address misspelt in start_server's arguments
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "who";

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

// IMAP for example.com: three mailboxes, and the folder team shared by Ann and Bob.
string bob = which == "typo" ? "bobb@example.com" : "bob@example.com";
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "imap://127.0.0.1:0" },
    ["arguments"] = new[]
    {
        "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpass", "--mailbox", "bob@example.com:bobpass",
        "--mailbox", "carol@example.com:carolpass", "--shared-folder", $"team=ann@example.com,{bob}",
    },
});
if (started.IsError == true)
{
    JsonElement refused = started.StructuredContent!.Value;
    Console.WriteLine($"start_server refused: exit_code {refused.GetProperty("exit_code").GetInt32()}, {refused.GetProperty("lines")[0].GetString()}");
    return 2;
}

string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string imap = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string[] everyone = ["ann", "bob", "carol"];

switch (which)
{
    case "store":
        foreach (string who in everyone)
        {
            CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = $"{who}@example.com" });
            var names = boxes.StructuredContent!.Value.GetProperty("mailboxes").EnumerateArray().Select(b => b.GetProperty("name").GetString());
            Console.WriteLine($"{who}: {string.Join(", ", names)}");
        }

        return 0;
    case "read":
        (int exit, _, string error) = await Curl("-sS", "-u", "carol@example.com:carolpass", imap + "Shared/team;UID=1");
        Console.WriteLine($"carol reads team: exit {exit}, {error.Trim()}");
        return 1;
}

// Ann puts the rota in the folder: an IMAP APPEND.
await Curl("-sS", "-u", "ann@example.com:annpass", "-T", "rota.eml", imap + "Shared/team");

// Each mailbox in turn: is the folder in its LIST, and what does STATUS say of it?
foreach (string who in everyone)
{
    string login = $"{who}@example.com:{who}pass";
    (_, string list, _) = await Curl("-sS", "-u", login, imap);
    bool listed = list.Split('\n').Any(line => line.TrimEnd('\r').EndsWith(" Shared/team"));
    (int exit, string status, _) = await Curl("-sS", "-u", login, "-X", "STATUS Shared/team (MESSAGES)", imap);
    Console.WriteLine($"{who}: {(listed ? "listed" : "not listed")}, STATUS {(exit == 0 ? status.Trim() : $"refused, curl exit {exit}")}");
}

return 0;

// The curl tool: curl's own arguments; answers exitCode, standardOutput and standardError as JSON.
async Task<(int Exit, string Out, string Err)> Curl(params string[] arguments)
{
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    using JsonDocument answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement root = answer.RootElement;
    return (root.GetProperty("exitCode").GetInt32(),
        root.GetProperty("standardOutput").GetProperty("content").GetString() ?? "",
        root.GetProperty("standardError").GetProperty("content").GetString() ?? "");
}
