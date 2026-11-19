// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent makes a running surl server fail on purpose through surl-mcp, and fetches with curl
// through curl-mcp to see the fault. Run: dotnet run faults.cs -- <case>
//   solution    add an error rule, fetch, remove it, fetch again
//   spent       a rule with times=1: fetch, list_faults, fetch again
//   from-start  a rule given to start_server, as surl's own --fault option
//   drop        a rule that cuts the connection
//   events      the fault_applied event the server kept
//   refused     a rule surl would refuse, and a rule that is not there
#:package ModelContextProtocol.Core@2.2.0

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

// A plain HTTP server on a free port, holding one file in memory; from-start also gives it a rule.
string[] arguments = which == "from-start" ? ["--fault", "delay;stage=command;path=/slow.txt;ms=100"] : [];
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
    ["arguments"] = arguments,
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "solution":
        string rule = await AddFault("error;scheme=http;reply=503 Service Unavailable");
        await Fetch("hello.txt");
        await RemoveFault(rule);
        await Fetch("hello.txt");
        break;
    case "spent":
        await AddFault("error;scheme=http;reply=503 Service Unavailable;times=1");
        await Fetch("hello.txt");
        await ListFaults();
        await Fetch("hello.txt");
        break;
    case "from-start":
        await ListFaults();
        break;
    case "drop":
        await AddFault("drop;stage=command;path=/hello.txt");
        await Fetch("hello.txt");
        break;
    case "events":
        await AddFault("error;scheme=http;reply=503 Service Unavailable");
        await Fetch("hello.txt");
        CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["kinds"] = new[] { "fault_applied" },
        });
        foreach (JsonElement e in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
        {
            Console.WriteLine($"get_events: {e.GetProperty("rule_id")} {e.GetProperty("fault_kind")} at {e.GetProperty("stage")} on {e.GetProperty("target")}");
        }

        break;
    case "refused":
        await AddFault("error;scheme=http;reply=200 OK");
        await RemoveFault("f9");
        await ListFaults();
        break;
    default:
        Console.Error.WriteLine($"unknown case {which}");
        return 2;
}

return 0;

// add_fault takes one --fault item and answers the rule's ID, or is a tool error that says why not.
async Task<string> AddFault(string fault)
{
    CallToolResult result = await surl.CallToolAsync("add_fault", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["fault"] = fault,
    });
    if (result.IsError == true)
    {
        Console.WriteLine($"add_fault: error: {result.Content.OfType<TextContentBlock>().Single().Text}");
        return "";
    }

    string id = result.StructuredContent!.Value.GetProperty("fault_id").GetString()!;
    Console.WriteLine($"add_fault: {id}");
    return id;
}

async Task RemoveFault(string id)
{
    CallToolResult result = await surl.CallToolAsync("remove_fault", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["fault_id"] = id,
    });
    Console.WriteLine(result.IsError == true
        ? $"remove_fault: error: {result.Content.OfType<TextContentBlock>().Single().Text}"
        : $"remove_fault: {result.StructuredContent!.Value.GetProperty("fault_id").GetString()}");
}

// list_faults answers each rule's ID, its item as surl spells it, and how many times it has left.
async Task ListFaults()
{
    CallToolResult result = await surl.CallToolAsync("list_faults", new Dictionary<string, object?> { ["server_id"] = server });
    JsonElement faults = result.StructuredContent!.Value.GetProperty("faults");
    Console.WriteLine($"list_faults: {faults.GetArrayLength()} rule(s)");
    foreach (JsonElement f in faults.EnumerateArray())
    {
        Console.WriteLine($"  {f.GetProperty("fault_id")} {f.GetProperty("item")} remaining {f.GetProperty("remaining").GetRawText()}");
    }
}

// transfer is curl-mcp's plain fetch; it answers the status and body, or curl's exit code, in text, as JSON.
async Task Fetch(string path)
{
    CallToolResult result = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + path });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement root = answer.RootElement;
    if (root.TryGetProperty("failure", out JsonElement failure) && failure.ValueKind == JsonValueKind.Object)
    {
        Console.WriteLine($"transfer {path}: curl exit {failure.GetProperty("exitCode")}");
        return;
    }

    int status = root.GetProperty("statusCode").GetInt32();
    string body = status == 200 ? " " + root.GetProperty("body").GetProperty("content").GetString()!.TrimEnd('\n') : "";
    Console.WriteLine($"transfer {path}: {status}{body}");
}
