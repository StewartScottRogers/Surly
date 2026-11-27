// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests that mail survives a restart: surl-mcp runs a mail server whose --directory keeps the
// store on disk, curl-mcp's curl tool delivers over SMTP, stop_server and a second start_server on the
// same folder restart it, and the message tools read the same message.
// Run: dotnet run keep-mail.cs -- <case>
//   restart   deliver, stop, start again, read the message (the default)
//   on-disk   the folder's files, while the server runs and after it stops
//   uid       the next message, after the restart
//   seen      a flag set by a fetch before the restart
//   twice     two restarts, with a message in between
//   in-use    a second server on the folder while the first runs
//   memory    the same test with no --directory
#:package ModelContextProtocol.Core@2.4.0

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "restart";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. The mail store goes in
// a folder of its own under it, made for this case and removed at the end.
string here = Directory.GetCurrentDirectory();
string state = Path.Combine(here, "state-" + which);
Directory.CreateDirectory(state);

try
{
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

    (string id, string smtp, string imap) = await Start();
    await Deliver("Order 1042 has shipped");

    switch (which)
    {
        case "on-disk":
            Console.WriteLine($"running: lock {File.Exists(Path.Combine(state, ".surl", "lock"))}, index {File.Exists(Path.Combine(state, ".surl", "mail", "index"))}");
            await Stop();
            Console.WriteLine($"stopped: lock {File.Exists(Path.Combine(state, ".surl", "lock"))}, index {File.Exists(Path.Combine(state, ".surl", "mail", "index"))}");
            break;
        case "in-use":
            CallToolResult second = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
            {
                ["listen_urls"] = new[] { "smtp://127.0.0.1:0" }, ["arguments"] = Arguments(true),
            });
            JsonElement refused = second.StructuredContent!.Value;
            Console.WriteLine(second.IsError == true
                ? $"second server: {refused.GetProperty("exit_code")} {refused.GetProperty("exit_code_name")}, in use: {refused.GetProperty("lines")[0].GetString()!.Contains("is in use by another surl process")}"
                : "second server: started");
            await Stop();
            break;
        case "seen":
            // curl fetches with a plain UID FETCH, which sets \Seen; the store keeps the flag.
            Console.WriteLine($"unseen before the fetch: {await Unseen()}");
            await Curl("-u", "ann@example.com:annpass", imap + "INBOX;UID=1");
            Console.WriteLine($"unseen after the fetch: {await Unseen()}");
            await Stop();
            (id, smtp, imap) = await Start();
            Console.WriteLine($"unseen after the restart: {await Unseen()}");
            await Stop();
            break;
        default:
            await Stop();
            (id, smtp, imap) = await Start();
            await Report("after the restart");
            if (which is "uid" or "twice")
            {
                await Deliver("Order 1043 has shipped");
                await Report("after the next message");
            }

            if (which == "twice")
            {
                await Stop();
                (id, smtp, imap) = await Start();
                await Report("after the second restart");
            }

            await Stop();
            break;
    }

    return 0;

    // The same options every time; the directory is what is kept.
    List<string> Arguments(bool keep)
    {
        var list = new List<string>
        {
            "--mail-domain", "example.com", "--mailbox", "shop@example.com:shoppass", "--mailbox", "ann@example.com:annpass",
        };
        if (keep)
        {
            list.AddRange(["--directory", state]);
        }

        return list;
    }

    async Task<(string Id, string Smtp, string Imap)> Start()
    {
        CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
        {
            ["listen_urls"] = new[] { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" }, ["arguments"] = Arguments(which != "memory"),
        });
        JsonElement server = started.StructuredContent!.Value;
        return (server.GetProperty("server_id").GetString()!, server.GetProperty("bound_urls")[0].GetString()!, server.GetProperty("bound_urls")[1].GetString()!);
    }

    Task Stop() => surl.CallToolAsync("stop_server", new Dictionary<string, object?> { ["server_id"] = id }).AsTask();

    // One mail from the shop to Ann; delivery is asynchronous, so wait for its store_changed event, with a bound.
    async Task Deliver(string subject)
    {
        File.WriteAllText(Path.Combine(here, "order.eml"),
            $"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: {subject}\r\n\r\nYour order is on its way.\r\n", Encoding.ASCII);
        JsonElement sent = await Curl("--crlf", "-u", "shop@example.com:shoppass", "--mail-from", "shop@example.com",
            "--mail-rcpt", "ann@example.com", "-T", "order.eml", smtp + "example.com");
        CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["timeout_ms"] = 10000,
        });
        if (waited.StructuredContent!.Value.TryGetProperty("timed_out", out _))
        {
            throw new TimeoutException("no delivery within 10 s");
        }

        Console.WriteLine($"curl exit {sent.GetProperty("exitCode")}, delivered {waited.StructuredContent!.Value.GetProperty("event").GetProperty("name").GetString()}");
    }

    // What Ann's INBOX holds, read with surl-mcp's message tools: each message's UID and subject.
    async Task Report(string when)
    {
        CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX",
        });
        var parts = new List<string>();
        foreach (JsonElement message in listed.StructuredContent!.Value.GetProperty("messages").EnumerateArray())
        {
            long uid = message.GetProperty("uid").GetInt64();
            CallToolResult read = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
            {
                ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX", ["uid"] = uid,
            });
            string text = read.StructuredContent!.Value.GetProperty("text").GetString()!;
            parts.Add($"UID {uid} {text.Split("\r\n").First(l => l.StartsWith("Subject: "))["Subject: ".Length..]}");
        }

        Console.WriteLine($"{when}: {parts.Count} message(s){(parts.Count == 0 ? "" : ": " + string.Join(", ", parts))}");
    }

    async Task<int> Unseen()
    {
        CallToolResult listed = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "ann@example.com" });
        return listed.StructuredContent!.Value.GetProperty("mailboxes").EnumerateArray()
            .Single(m => m.GetProperty("name").GetString() == "INBOX").GetProperty("unseen_count").GetInt32();
    }

    // The curl tool runs curl's own arguments and answers in text that is JSON; -s keeps the progress meter out.
    async Task<JsonElement> Curl(params string[] curlArguments)
    {
        CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments.Prepend("-s").ToArray() });
        return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
    }
}
finally
{
    Directory.Delete(state, true);
    File.Delete(Path.Combine(here, "order.eml"));
}
