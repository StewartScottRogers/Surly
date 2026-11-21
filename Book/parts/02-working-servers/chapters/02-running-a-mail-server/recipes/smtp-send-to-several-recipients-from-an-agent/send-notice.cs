// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent sends one notice to several people: surl-mcp runs a mail server, curl-mcp's curl tool sends
// one message with a --mail-rcpt for each person, and surl-mcp's events and message tools show what
// each mailbox got.
// Run: dotnet run send-notice.cs -- <case>
//   three         one message to Ann, Bob and Cy (the default)
//   header        the To field of Bob's copy
//   exchange      the RCPT and DATA commands the server saw
//   refused       Dee and Carol, who is outside the domain
//   refused-none  the same, and what Dee holds afterwards
//   allowfails    Fay and Carol, with --mail-rcpt-allowfails
//   unknown       Gus and an address in the domain that is no mailbox
//   twice         Hal twice and Ivy once
#:package ModelContextProtocol.Core@*

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "three";

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

// SMTP for example.com: the app's mailbox and one for each person who might be named.
var arguments = new List<string> { "--mail-domain", "example.com" };
foreach (string name in new[] { "app", "ann", "bob", "cy", "dee", "fay", "gus", "hal", "ivy" })
{
    arguments.AddRange(["--mailbox", $"{name}@example.com:{name}pass"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0" },
    ["arguments"] = arguments,
});
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string smtp = server.GetProperty("bound_urls")[0].GetString()!;

// Who the app names, and which mailbox is the last one to be filled.
(string[] to, string last) = which switch
{
    "refused" or "refused-none" => (new[] { "dee@example.com", "carol@elsewhere.org" }, ""),
    "allowfails" => (new[] { "fay@example.com", "carol@elsewhere.org" }, "fay@example.com"),
    "unknown" => (new[] { "gus@example.com", "nobody@example.com" }, "gus@example.com"),
    "twice" => (new[] { "hal@example.com", "hal@example.com", "ivy@example.com" }, "ivy@example.com"),
    _ => (new[] { "ann@example.com", "bob@example.com", "cy@example.com" }, "cy@example.com"),
};

// The app's one send: -s keeps the progress meter out of the answer, -S keeps the error line in.
string[] rcpt = to.SelectMany(t => new[] { "--mail-rcpt", t }).ToArray();
string[] allow = which == "allowfails" ? ["--mail-rcpt-allowfails"] : [];
JsonElement sent = await Curl(["-s", "-S", "--crlf", "-u", "app@example.com:apppass", "--mail-from", "app@example.com", .. rcpt, .. allow, "-T", "notice.eml", smtp + "example.com"]);
int exit = sent.GetProperty("exitCode").GetInt32();
string error = sent.GetProperty("standardError").GetProperty("content").GetString()!.Trim();
Console.WriteLine(exit == 0 ? "curl exit 0" : $"curl exit {exit}: {error}");

// Delivery is asynchronous: each delivery is a store_changed event, and the program waits for the last
// recipient's, with a bound. Deliveries happen in order, so those before it have been dealt with.
if (last != "")
{
    var names = new List<string>();
    long seen = 0;
    while (!names.Any(n => n.StartsWith(last + "/")))
    {
        CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["after_sequence"] = seen, ["timeout_ms"] = 10000,
        });
        JsonElement answer = waited.StructuredContent!.Value;
        if (answer.TryGetProperty("timed_out", out _))
        {
            Console.WriteLine($"no delivery to {last} within 10 s");
            return 1;
        }

        seen = answer.GetProperty("event").GetProperty("sequence").GetInt64();
        names.Add(answer.GetProperty("event").GetProperty("name").GetString()!);
    }

    Console.WriteLine($"delivered: {string.Join(", ", names.Order())}");
}

// The test: what each mailbox holds, read with surl-mcp's message tools.
switch (which)
{
    case "header":
        CallToolResult read = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["owner"] = "bob@example.com", ["mailbox"] = "INBOX", ["uid"] = 1,
        });
        string text = read.StructuredContent!.Value.GetProperty("text").GetString()!;
        Console.WriteLine(text.Split("\r\n").First(l => l.StartsWith("To: ")));
        break;
    case "exchange":
        // The SMTP conversation, byte for byte: what the client sent is the chunks that were received.
        CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["scheme"] = "smtp" });
        var received = new StringBuilder();
        foreach (JsonElement exchange in exchanges.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray())
        {
            foreach (JsonElement chunk in exchange.GetProperty("chunks").EnumerateArray())
            {
                received.Append(chunk.GetProperty("direction").GetString() == "received" ? chunk.GetProperty("data").GetString() : "");
            }
        }

        string conversation = received.ToString();
        Console.WriteLine($"RCPT TO commands: {Regex.Matches(conversation, "RCPT TO:<").Count}");
        Console.WriteLine($"DATA commands: {Regex.Matches(conversation, "(?m)^DATA\r?$").Count}");
        break;
    case "refused":
        break;
    case "refused-none":
        Console.WriteLine($"dee@example.com holds {await Count("dee@example.com")}");
        break;
    case "allowfails" or "unknown":
        Console.WriteLine($"{to[0]} holds {await Count(to[0])}");
        break;
    case "twice":
        Console.WriteLine($"hal@example.com holds {await Count("hal@example.com")}");
        Console.WriteLine($"ivy@example.com holds {await Count("ivy@example.com")}");
        break;
    default:
        foreach (string owner in to)
        {
            Console.WriteLine($"{owner} holds {await Count(owner)}");
        }

        break;
}

return 0;

// The curl tool runs curl's own arguments and answers in text that is JSON.
async Task<JsonElement> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}

async Task<int> Count(string owner)
{
    CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["owner"] = owner, ["mailbox"] = "INBOX",
    });
    return listed.StructuredContent!.Value.GetProperty("messages").GetArrayLength();
}
