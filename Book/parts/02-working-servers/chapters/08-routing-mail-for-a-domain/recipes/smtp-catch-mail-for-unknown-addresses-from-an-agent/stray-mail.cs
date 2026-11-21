// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent finds mail sent to addresses nobody configured: surl-mcp runs a server with a catch-all,
// curl-mcp sends as the app would, and the agent waits for delivery, then reads the catch-all.
// Run: dotnet run stray-mail.cs [-- <case>]
//   (no argument)  two stray addresses and ann, on a server with a catch-all
//   no-catch-all   the same mail, on a server without one
//   letter-case    Ann@Example.COM
//   alias          support@example.com, an alias for ann
//   other-domain   one message to stray@ and team@example.org, a domain with no catch-all
//   envelope-only  a message to jo@example.com whose To header names ann
//   outside        one address outside every mail domain
//   twice          two catch-alls for one domain
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "catch-all";

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

// SMTP on a free port; example.com with mailboxes for the app, ann and the catch-all; example.org with team's.
List<string> options = ["--mail-domain", "example.com", "--mail-domain", "example.org",
    "--mailbox", "app@example.com:apppass", "--mailbox", "ann@example.com:annpass",
    "--mailbox", "catchall@example.com:catchpass", "--mailbox", "team@example.org:teampass",
    "--mail-alias", "support@example.com=ann@example.com"];
if (which != "no-catch-all")
{
    options.AddRange(["--mail-catch-all", "example.com=catchall@example.com"]);
}

if (which == "twice")
{
    options.AddRange(["--mail-catch-all", "example.com=ann@example.com"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0" },
    ["arguments"] = options,
});
JsonElement server = started.StructuredContent!.Value;
if (started.IsError == true)
{
    Console.WriteLine($"start_server: exit_code {server.GetProperty("exit_code")} ({server.GetProperty("exit_code_name")}), {server.GetProperty("lines")[0]}");
    return 1;
}

string id = server.GetProperty("server_id").GetString()!;
string smtp = server.GetProperty("bound_urls")[0].GetString()!;
foreach (JsonElement line in server.GetProperty("log_lines").EnumerateArray().Where(l => l.GetString()!.Contains("warning")))
{
    Console.WriteLine($"start_server: {line.GetString()}");
}

// What the app sends in each case: the envelope recipients and the To header; and whose delivery comes last.
(string[] To, string Header)[] sends = which switch
{
    "letter-case" => [(["Ann@Example.COM"], "Ann@Example.COM")],
    "alias" => [(["support@example.com"], "support@example.com")],
    "other-domain" => [(["stray@example.org", "team@example.org"], "stray@example.org, team@example.org")],
    "envelope-only" => [(["jo@example.com"], "ann@example.com")],
    "outside" => [(["someone@example.net"], "someone@example.net")],
    _ => [(["anne@example.com"], "anne@example.com"), (["bob.smith@example.com"], "bob.smith@example.com"), (["ann@example.com"], "ann@example.com")],
};
string last = which switch { "other-domain" => "team@example.org", "envelope-only" => "catchall@example.com", _ => "ann@example.com" };

// The app: one message file per send, uploaded with the curl tool.
int n = 0;
foreach ((string[] to, string header) in sends)
{
    string file = $"message{++n}.eml";
    File.WriteAllText(Path.Combine(here, file), $"From: app@example.com\r\nTo: {header}\r\nSubject: Welcome\r\n\r\nYour account is ready.\r\n");
    List<string> arguments = ["-s", "-S", "-u", "app@example.com:apppass", "--mail-from", "app@example.com"];
    foreach (string address in to)
    {
        arguments.AddRange(["--mail-rcpt", address]);
    }

    arguments.AddRange(["-T", file, smtp + "example.com"]);
    CallToolResult sent = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.ToArray() });
    JsonElement answer = JsonDocument.Parse(sent.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string error = answer.GetProperty("standardError").GetProperty("content").GetString()!.Trim();
    Console.WriteLine($"sent to {string.Join(", ", to)}: curl exit {answer.GetProperty("exitCode")}{(error == "" ? "" : $", {error}")}");
    if (answer.GetProperty("exitCode").GetInt32() != 0)
    {
        return 1;
    }
}

// Delivery is asynchronous, and in order: once the last delivery is in, the ones before it were dealt with.
CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail",
    ["name"] = last, ["timeout_ms"] = 10000,
});
if (!waited.StructuredContent!.Value.TryGetProperty("event", out JsonElement delivered))
{
    Console.WriteLine($"no delivery to {last} within 10 s");
    return 1;
}

Console.WriteLine($"wait_for_event: {delivered.GetProperty("name")}");

// The catch-all: each message and the address its To header names.
CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
{
    ["server_id"] = id, ["owner"] = "catchall@example.com", ["mailbox"] = "INBOX",
});
int caught = 0;
foreach (JsonElement message in listed.StructuredContent!.Value.GetProperty("messages").EnumerateArray())
{
    long uid = message.GetProperty("uid").GetInt64();
    CallToolResult read = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["owner"] = "catchall@example.com", ["mailbox"] = "INBOX", ["uid"] = uid,
    });
    string to = read.StructuredContent!.Value.GetProperty("text").GetString()!.Split("\r\n").First(h => h.StartsWith("To: "));
    Console.WriteLine($"caught UID {uid}: {to}");
    caught++;
}

Console.WriteLine($"stray messages caught: {caught}");
return caught == 2 ? 0 : 1;
