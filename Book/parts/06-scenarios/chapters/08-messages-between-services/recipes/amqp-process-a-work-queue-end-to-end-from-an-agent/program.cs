// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs a work queue end to end on surl-mcp: curl-mcp queues jobs over AMQP and runs a STOMP worker,
// surl-mcp browses the queue and reports what the bus recorded.
// Run: dotnet run program.cs -- <scenario>
//   solution   three jobs, a worker that acknowledges all three
//   restart    two jobs queued, the server stopped and started on the same folder, a worker takes them
//   fails      three jobs, a worker that acknowledges two of them
//   refused    a server started without --allow-anonymous
#:package ModelContextProtocol.Core@2.2.0

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string scenario = args.Length > 0 ? args[0] : "solution";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. The durable queue goes in a
// folder of its own under it, made for this scenario and removed at the end. The STOMP frames of the worker are
// in worker.stomp and two.stomp beside the program: a NUL ends each frame.
string here = Directory.GetCurrentDirectory();
string state = Path.Combine(here, "state-" + scenario);
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

    (string id, string amqp, string stomp) = await Start(scenario != "refused");
    long lastSequence = 0;

    switch (scenario)
    {
        case "solution":
            await Queue("job-1", "job-2", "job-3");
            Console.WriteLine($"worker received: {await Work("worker.stomp")}");
            await Check(acknowledged: 3);
            break;
        case "restart":
            await Queue("job-4", "job-5");
            await Stop();
            (id, amqp, stomp) = await Start(true);
            Console.WriteLine($"after the restart the worker received: {await Work("two.stomp")}");
            await Check(acknowledged: 2);
            break;
        case "fails":
            await Queue("job-6", "job-7", "job-8");
            Console.WriteLine($"worker received: {await Work("two.stomp")}");
            Console.WriteLine($"after the worker: ready {await Browse()}");
            (_, _, string again, _) = await Curl("-sS", "-m", "5", $"{amqp}%2F/jobs");
            Console.WriteLine($"second receive: [{again}]");
            await Check(acknowledged: 3);
            break;
        case "refused":
            (int code, bool toolError, _, string error) = await Curl("-sS", "-m", "5", $"{amqp}%2F/jobs");
            Console.WriteLine($"receive: exit {code}, tool error {(toolError ? "yes" : "no")}, error {error}");
            Console.WriteLine($"worker: {(await Work("worker.stomp")).Split('\n')[0]}");
            break;
    }

    await Stop();
    return 0;

    // The same options every time; --directory is what keeps the durable queue.
    async Task<(string, string, string)> Start(bool anonymous)
    {
        var arguments = new List<string> { "--directory", state, "--queue", "jobs;durable" };
        if (anonymous)
        {
            arguments.Add("--allow-anonymous");
        }

        CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
        {
            ["listen_urls"] = new[] { "amqp://127.0.0.1:0", "stomp://127.0.0.1:0" }, ["arguments"] = arguments,
        });
        JsonElement server = started.StructuredContent!.Value;
        lastSequence = 0;
        return (server.GetProperty("server_id").GetString()!, server.GetProperty("bound_urls")[0].GetString()!,
            server.GetProperty("bound_urls")[1].GetString()!);
    }

    Task Stop() => surl.CallToolAsync("stop_server", new Dictionary<string, object?> { ["server_id"] = id }).AsTask();

    // curl-mcp's curl tool runs curl with the arguments given; its answer is text, as JSON.
    async Task<(int, bool, string, string)> Curl(params string[] curlArguments)
    {
        CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
        JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
        JsonElement output = answer.GetProperty("standardOutput");
        string text = output.GetProperty("content").GetString()!;
        if (output.GetProperty("isBase64").GetBoolean())
        {
            text = Encoding.UTF8.GetString(Convert.FromBase64String(text));
        }

        return (answer.GetProperty("exitCode").GetInt32(), result.IsError == true, text,
            answer.GetProperty("standardError").GetProperty("content").GetString()!.Trim());
    }

    // The app: curl -d queues one job over AMQP, and a send is asynchronous, so wait, with a bound, for the bus
    // to report it before the next.
    async Task Queue(params string[] jobs)
    {
        foreach (string job in jobs)
        {
            await Curl("-sS", "-m", "5", "-d", job, $"{amqp}%2F/jobs");
            for (int attempt = 0; attempt < 10; attempt++)
            {
                CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
                {
                    ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "bus",
                    ["after_sequence"] = lastSequence, ["timeout_ms"] = 10000,
                });
                if (!waited.StructuredContent!.Value.TryGetProperty("event", out JsonElement change))
                {
                    throw new TimeoutException($"the bus did not report {job} in 10 s");
                }

                lastSequence = change.GetProperty("sequence").GetInt64();
                if (change.GetProperty("change").GetString() == "enqueued")
                {
                    break;
                }
            }
        }
    }

    // The worker: curl -T sends a file of STOMP frames to a telnet:// URL and writes what the server answers.
    async Task<string> Work(string frames)
    {
        Uri port = new(stomp);
        (_, _, string answer, _) = await Curl("-sS", "-m", "10", "-T", frames, $"telnet://{port.Host}:{port.Port}/");
        if (answer.StartsWith("ERROR", StringComparison.Ordinal))
        {
            return answer.Replace("\0", "");
        }

        string jobs = string.Join(", ", Regex.Matches(answer, @"job-\d+").Select(m => m.Value));
        return answer.Contains("RECEIPT", StringComparison.Ordinal) ? $"{jobs}, then the receipt" : jobs;
    }

    // browse_queue counts what is ready and what a consumer holds, and lists the ready messages, without taking any.
    async Task<string> Browse()
    {
        CallToolResult browsed = await surl.CallToolAsync("browse_queue", new Dictionary<string, object?> { ["server_id"] = id, ["queue"] = "jobs" });
        JsonElement page = browsed.StructuredContent!.Value;
        string ready = string.Join(", ", page.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("text").GetString()));
        return $"{page.GetProperty("ready_count").GetInt32()} [{ready}], in flight {page.GetProperty("in_flight_count").GetInt32()}";
    }

    // What the test checks afterwards: the queue, and the bus record of what happened to the messages. The record
    // is written as the server works, so poll get_events, with a bound, until the last acknowledgement is in.
    async Task Check(int acknowledged)
    {
        Console.WriteLine($"queue: ready {await Browse()}");
        var counts = new Dictionary<string, int>();
        for (int attempt = 0; attempt < 50; attempt++)
        {
            CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
            {
                ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "bus", ["max_events"] = 200,
            });
            counts = events.StructuredContent!.Value.GetProperty("events").EnumerateArray()
                .GroupBy(e => e.GetProperty("change").GetString()!).ToDictionary(g => g.Key, g => g.Count());
            if (counts.GetValueOrDefault("acknowledged") >= acknowledged)
            {
                break;
            }

            await Task.Delay(100);
        }

        Console.WriteLine($"events: enqueued {counts.GetValueOrDefault("enqueued")}, delivered {counts.GetValueOrDefault("delivered")}, acknowledged {counts.GetValueOrDefault("acknowledged")}");
    }
}
finally
{
    Directory.Delete(state, true);
}
