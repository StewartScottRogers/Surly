// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent reports what a job queue still holds after a worker ran: surl-mcp runs the broker,
// curl-mcp's curl tool plays the app and the worker, and browse_queue lists the jobs left, taking none.
// Run: dotnet run check-queue.cs -- <case>
//   report        the app queues five jobs over AMQP, the worker takes two (the default)
//   in-flight     a worker takes every job and acknowledges two, and is still busy when the agent looks
//   no-queue      the agent browses a queue the server does not have
//   no-plaintext  the server keeps its default: no password in clear without TLS
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "report";

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

// An AMQP 0-9-1 broker with one queue, jobs, and a STOMP listener on the same bus; one account.
string[] options = ["--queue", "jobs", "--user", "worker:s3cret", "--allow-plaintext-auth"];
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "amqp://127.0.0.1:0", "stomp://127.0.0.1:0" },
    ["arguments"] = which == "no-plaintext" ? options[..^1] : options,
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string amqp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string stomp = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!;

// The app queues five jobs through the default exchange, as curl -d does over AMQP.
for (int n = 1; n <= 5; n++)
{
    JsonElement queued = await Curl("-S", "-u", "worker:s3cret", "-d", $"job-{n}", amqp + "%2F/jobs");
    if (queued.GetProperty("exitCode").GetInt32() != 0)
    {
        Console.WriteLine($"app could not queue job-{n}: curl exit {queued.GetProperty("exitCode")}, {queued.GetProperty("standardError").GetProperty("content").GetString()!.Trim()}");
        return 1;
    }
}

switch (which)
{
    case "in-flight":
        // A worker that holds jobs: over STOMP it subscribes with per-message acknowledgement,
        // acknowledges the first two and stays connected for three seconds with the rest.
        Task<JsonElement> worker = Curl("-m", "3", "-T", "worker.stomp", "telnet://" + stomp["stomp://".Length..]);
        await WaitForAcknowledgedAsync(2);
        Report("while the worker runs", await BrowseAsync("jobs"));
        Console.WriteLine($"worker ended: curl exit {(await worker).GetProperty("exitCode")}");
        Report("after it ended", await BrowseAsync("jobs"));
        break;
    case "no-queue":
        CallToolResult missing = await surl.CallToolAsync("browse_queue", new Dictionary<string, object?> { ["server_id"] = id, ["queue"] = "job" });
        Console.WriteLine($"browse_queue job: tool error {(missing.IsError == true ? "yes" : "no")}, {missing.Content.OfType<TextContentBlock>().Single().Text}");
        return 1;
    default:
        // The worker under test takes two jobs, one get each.
        for (int n = 0; n < 2; n++)
        {
            JsonElement taken = await Curl("-u", "worker:s3cret", amqp + "%2F/jobs");
            Console.WriteLine($"worker took {taken.GetProperty("standardOutput").GetProperty("content").GetString()}");
        }

        Report("after the worker", await BrowseAsync("jobs"));
        break;
}

// The bus's own record: one delivered and one acknowledged event per job a worker took.
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["store"] = "bus", ["kinds"] = new[] { "store_changed" } });
var changes = events.StructuredContent!.Value.GetProperty("events").EnumerateArray().GroupBy(e => e.GetProperty("change").GetString());
Console.WriteLine($"get_events: {string.Join(", ", changes.Select(g => $"{g.Key} {g.Count()}"))}");
return 0;

// browse_queue lists a queue's ready messages without taking them, and counts those in flight.
async Task<JsonElement> BrowseAsync(string queue)
{
    CallToolResult result = await surl.CallToolAsync("browse_queue", new Dictionary<string, object?> { ["server_id"] = id, ["queue"] = queue });
    return result.StructuredContent!.Value.Clone();
}

void Report(string when, JsonElement page)
{
    string ready = string.Join(", ", page.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("text").GetString()));
    Console.WriteLine($"{when}: ready {page.GetProperty("ready_count")} [{ready}], in flight {page.GetProperty("in_flight_count")}");
}

// wait_for_event returns the first matching event after a sequence number, so the agent waits
// on the worker's acknowledgements instead of sleeping.
async Task WaitForAcknowledgedAsync(int count)
{
    long after = 0;
    while (count > 0)
    {
        CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["after_sequence"] = after, ["store"] = "bus", ["kinds"] = new[] { "store_changed" }, ["timeout_ms"] = 10000,
        });
        JsonElement found = waited.StructuredContent!.Value;
        if (found.TryGetProperty("timed_out", out _))
        {
            throw new TimeoutException("the worker acknowledged nothing in 10 s");
        }

        JsonElement change = found.TryGetProperty("event", out JsonElement inner) ? inner : found;
        after = change.GetProperty("sequence").GetInt64();
        count -= change.GetProperty("change").GetString() == "acknowledged" ? 1 : 0;
    }
}

// The curl tool runs curl's own arguments and answers in text that is JSON; -s keeps the progress meter out.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
