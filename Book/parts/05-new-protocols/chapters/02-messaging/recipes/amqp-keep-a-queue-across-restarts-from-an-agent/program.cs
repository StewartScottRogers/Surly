// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent checks that a durable AMQP queue survives a restart: surl-mcp starts a server that keeps its queues
// in a folder, curl-mcp queues jobs and takes them, stop_server and a second start_server on the same
// folder are the restart.
// Run: dotnet run program.cs -- <scenario>
//   restart   queue three jobs, take one, restart, take the next
//   scratch   the same restart, and the plain queue scratch
//   disk      the folder's files, while the server runs and after it stops
//   twice     a job queued after the first restart, then a second and a third restart
//   memory    the same test with no --directory
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string scenario = args.Length > 0 ? args[0] : "restart";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. The queues go in a
// folder of their own under it, made for this scenario and removed at the end.
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

    (string id, string url) = await Start(scenario != "memory");
    long lastSequence = 0;

    // Three jobs and one scratch message are queued, then the worker takes job-1.
    foreach (string job in new[] { "job-1", "job-2", "job-3" })
    {
        await Send("jobs", job);
    }

    await Send("scratch", "tmp");
    Console.WriteLine($"worker took: {await Take("jobs")}");

    switch (scenario)
    {
        case "disk":
            Console.WriteLine($"running: lock {Exists("lock")}, index {Exists("bus", "queues", "index")}");
            await Stop();
            Console.WriteLine($"stopped: lock {Exists("lock")}, index {Exists("bus", "queues", "index")}");
            break;
        case "memory":
            Console.WriteLine($"while it runs, jobs holds {await Ready("jobs")}");
            await Stop();
            (id, url) = await Start(false);
            Console.WriteLine($"after a restart, jobs holds {await Ready("jobs")}");
            await Stop();
            break;
        default:
            // The restart: stop the server, start another on the same folder.
            await Stop();
            (id, url) = await Start(true);
            Console.WriteLine($"after the restart: {await Take("jobs")}");
            if (scenario == "scratch")
            {
                Console.WriteLine($"scratch after the restart: [{await Take("scratch")}]");
            }

            if (scenario == "twice")
            {
                lastSequence = 0;
                await Send("jobs", "job-4");
                await Stop();
                (id, url) = await Start(true);
                Console.WriteLine($"after the second restart: {await Take("jobs")} then {await Take("jobs")}");
                await Stop();
                (id, url) = await Start(true);
                Console.WriteLine($"after the third restart: [{await Take("jobs")}]");
            }

            await Stop();
            break;
    }

    return 0;

    bool Exists(params string[] parts) => File.Exists(Path.Combine([state, ".surl", .. parts]));

    // The same options every time; --directory is what is kept. jobs is durable, scratch is not.
    async Task<(string, string)> Start(bool keep)
    {
        var arguments = new List<string> { "--allow-anonymous", "--queue", "jobs;durable", "--queue", "scratch" };
        if (keep)
        {
            arguments.AddRange(["--directory", state]);
        }

        CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
        {
            ["listen_urls"] = new[] { "amqp://127.0.0.1:0" }, ["arguments"] = arguments,
        });
        JsonElement server = started.StructuredContent!.Value;
        lastSequence = 0;
        return (server.GetProperty("server_id").GetString()!, server.GetProperty("bound_urls")[0].GetString()!);
    }

    Task Stop() => surl.CallToolAsync("stop_server", new Dictionary<string, object?> { ["server_id"] = id }).AsTask();

    // curl-mcp's curl tool: -d sends to the queue, no -d takes one message. AMQP 0-9-1 names the virtual host
    // first; %2F is the virtual host /.
    async Task<string> Curl(params string[] curlArguments)
    {
        CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments.Prepend("-sS").ToArray() });
        using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
        return answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!;
    }

    Task<string> Take(string queue) => Curl($"{url}%2F/{queue}");

    // A send is asynchronous: wait, with a bound, for the bus to report the message on the queue.
    async Task Send(string queue, string text)
    {
        await Curl("-d", text, $"{url}%2F/{queue}");
        for (int attempt = 0; attempt < 10; attempt++)
        {
            CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
            {
                ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "bus",
                ["after_sequence"] = lastSequence, ["timeout_ms"] = 10000,
            });
            JsonElement found = waited.StructuredContent!.Value;
            if (!found.TryGetProperty("event", out JsonElement change))
            {
                throw new TimeoutException("the bus did not report the message in 10 s");
            }

            lastSequence = change.GetProperty("sequence").GetInt64();
            if (change.GetProperty("change").GetString() == "enqueued")
            {
                return;
            }
        }

        throw new TimeoutException("no enqueued event");
    }

    // browse_queue counts what is ready without taking anything.
    async Task<int> Ready(string queue)
    {
        CallToolResult browsed = await surl.CallToolAsync("browse_queue", new Dictionary<string, object?> { ["server_id"] = id, ["queue"] = queue });
        return browsed.StructuredContent!.Value.GetProperty("ready_count").GetInt32();
    }
}
finally
{
    Directory.Delete(state, true);
}
