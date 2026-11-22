// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent lists the jobs an app queued on an AMQP queue, in delivery order and without taking any, with
// surl-mcp's browse_queue, over the official MCP SDK. Run: dotnet run program.cs -- <case>
//   browse    the app queues three jobs; the agent browses twice (the default)
//   paging    the agent takes the queue two at a time
//   untouched a worker takes one job after the agent has browsed
//   empty     a queue nothing was sent to
//   no-queue  a queue the server does not have
//   closed    a server with no --allow-anonymous
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "browse";

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

// An AMQP broker with the queue jobs from the start. --allow-anonymous lets the app in with no account.
var arguments = new List<string> { "--queue", "jobs" };
if (which != "closed")
{
    arguments.Add("--allow-anonymous");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "amqp://127.0.0.1:0" },
    ["arguments"] = arguments,
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string amqp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string jobs = amqp + "%2F/jobs";
long lastSequence = 0;

// The app under test queues three jobs, in this order.
if (which != "empty" && which != "no-queue")
{
    foreach (string job in new[] { "resize cat.png", "resize dog.png", "mail ann" })
    {
        (int exit, string error) = await Curl("-d", job, jobs);
        if (exit != 0)
        {
            Console.WriteLine($"app could not queue: exit {exit} ({error})");
            break;
        }

        await WaitForEnqueue();
    }
}

switch (which)
{
    case "paging":
        int after = -1;
        while (true)
        {
            JsonElement page = await Browse(after, 2);
            JsonElement[] messages = page.GetProperty("messages").EnumerateArray().ToArray();
            Console.WriteLine($"page after {after}: {string.Join(", ", messages.Select(m => $"{m.GetProperty("position")} {m.GetProperty("text").GetString()}"))} (has_more {page.GetProperty("has_more")})");
            if (!page.GetProperty("has_more").GetBoolean())
            {
                break;
            }

            after = messages[^1].GetProperty("position").GetInt32();
        }

        break;
    case "no-queue":
        CallToolResult missing = await surl.CallToolAsync("browse_queue", new Dictionary<string, object?> { ["server_id"] = server, ["queue"] = "nope" });
        Console.WriteLine($"browse of nope: tool error {missing.IsError}, {missing.Content.OfType<TextContentBlock>().Single().Text}");
        break;
    default:
        Print("browse", await Browse(-1, 100));
        Print("browse again", await Browse(-1, 100));
        if (which == "untouched")
        {
            // A worker takes a job with curl's get: the first one, because the browse took nothing.
            (_, string took) = await CurlOutput(jobs);
            Console.WriteLine($"worker took: {took}");
            Print("browse after the worker", await Browse(-1, 100));
        }

        break;
}

return 0;

// browse_queue lists messages from a position, in delivery order, and counts the ready and in-flight ones.
async Task<JsonElement> Browse(int afterPosition, int limit)
{
    CallToolResult result = await surl.CallToolAsync("browse_queue", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["queue"] = "jobs", ["after_position"] = afterPosition, ["limit"] = limit,
    });
    return result.StructuredContent!.Value;
}

void Print(string label, JsonElement page)
{
    Console.WriteLine($"{label}: ready {page.GetProperty("ready_count")}, in flight {page.GetProperty("in_flight_count")}");
    foreach (JsonElement message in page.GetProperty("messages").EnumerateArray())
    {
        Console.WriteLine($"  {message.GetProperty("position")} {message.GetProperty("text").GetString()}");
    }
}

// The curl tool runs curl's own arguments; -s keeps the progress meter out and -S puts an error message back.
async Task<(int Exit, string Error)> Curl(params string[] curlArguments)
{
    (int exit, string output, string error) = await Run(curlArguments);
    return (exit, error);
}

async Task<(int Exit, string Output)> CurlOutput(params string[] curlArguments)
{
    (int exit, string output, _) = await Run(curlArguments);
    return (exit, output);
}

async Task<(int Exit, string Output, string Error)> Run(string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S" }.Concat(curlArguments).ToArray(),
    });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    return (answer.GetProperty("exitCode").GetInt32(),
        answer.GetProperty("standardOutput").GetProperty("content").GetString()!,
        answer.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd());
}

// A send is done when the broker has recorded the message as queued: wait for that event, at most ten seconds.
async Task WaitForEnqueue()
{
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["after_sequence"] = lastSequence, ["kinds"] = new[] { "store_changed" },
        ["store"] = "bus", ["name"] = "$queue/jobs", ["timeout_ms"] = 10000,
    });
    JsonElement found = waited.StructuredContent!.Value;
    if (!found.TryGetProperty("event", out JsonElement queued))
    {
        throw new TimeoutException("the broker did not record the job in 10 s");
    }

    lastSequence = queued.GetProperty("sequence").GetInt64();
}
