// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs test workers side by side, each with its own surl-mcp server on a free port and its
// own in-memory store, tested through curl-mcp and stopped when its tests end.
// Run: dotnet run workers.cs [-- <count> [<worker whose tests use a wrong password>] | shared-directory | shared-port]
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

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

if (args is ["shared-directory"] or ["shared-port"])
{
    // A first server holds a data directory and a port; a second asks for one of them.
    Directory.CreateDirectory("shared");
    JsonElement first = (await Start("http://127.0.0.1:0", "--directory", "shared")).StructuredContent!.Value;
    CallToolResult second = args[0] == "shared-directory"
        ? await Start("http://127.0.0.1:0", "--directory", "shared")
        : await Start(first.GetProperty("bound_urls")[0].GetString()!);
    JsonElement why = second.StructuredContent!.Value;
    Console.WriteLine($"{args[0]}: tool error {second.IsError == true}, {why.GetProperty("exit_code_name")} ({why.GetProperty("exit_code")})");
    return why.GetProperty("exit_code").GetInt32();
}

int count = args.Length > 0 ? int.Parse(args[0]) : 4;
int broken = args.Length > 1 ? int.Parse(args[1]) : 0;

// All workers at once; each starts, tests and stops its own server.
var results = await Task.WhenAll(Enumerable.Range(1, count).Select(n => Worker(n, n == broken ? "wrong" : "s3cret")));
foreach (var result in results)
{
    Console.WriteLine(result.Line);
}

Console.WriteLine($"ports: {results.Select(result => result.Url).Distinct().Count()} different; " +
    $"servers left: {(await surl.CallToolAsync("list_servers", new Dictionary<string, object?>())).StructuredContent!.Value.GetProperty("servers").GetArrayLength()}");
return results.FirstOrDefault(result => result.ExitCode != 0).ExitCode;

// One worker: its own server, its own tests, its own stop.
async Task<(string Url, int ExitCode, string Line)> Worker(int n, string password)
{
    JsonElement server = (await StartWith("http://127.0.0.1:0", new() { ["whoami.txt"] = $"worker {n}\n" },
        "--user", "tester:s3cret", "--allow-uploads")).StructuredContent!.Value;
    string serverId = server.GetProperty("server_id").GetString()!, url = server.GetProperty("bound_urls")[0].GetString()!;
    try
    {
        // The tests: upload a result under the name every worker uses, then read it and whoami.txt back.
        File.WriteAllText($"result-{n}.txt", $"result of worker {n}\n");
        string[] login = ["-fsS", "--digest", "-u", $"tester:{password}"];
        var upload = await Curl([.. login, "-T", $"result-{n}.txt", url + "result.txt"]);
        if (upload.ExitCode != 0)
        {
            return (url, upload.ExitCode, $"worker {n} failed: {upload.Error.TrimEnd()}");
        }

        string result = (await Curl([.. login, url + "result.txt"])).Output.TrimEnd();
        string whoami = (await Curl([.. login, url + "whoami.txt"])).Output.TrimEnd();

        // What this worker's server recorded, read from its own store.
        CallToolResult stored = await surl.CallToolAsync("read_file", new Dictionary<string, object?>
        {
            ["server_id"] = serverId, ["path"] = "/result.txt",
        });
        return (url, 0, $"worker {n}: {whoami}, {result}; stored {stored.StructuredContent!.Value.GetProperty("text").GetString()!.TrimEnd()}");
    }
    finally
    {
        await surl.CallToolAsync("stop_server", new Dictionary<string, object?> { ["server_id"] = serverId });
    }
}

// start_server on one listen URL with surl's options as its words, and files for its in-memory store.
async Task<CallToolResult> Start(string listenUrl, params string[] arguments) => await StartWith(listenUrl, [], arguments);

async Task<CallToolResult> StartWith(string listenUrl, Dictionary<string, string> files, params string[] arguments) =>
    await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { listenUrl },
        ["arguments"] = arguments,
        ["files"] = files,
    });

// The curl tool with these arguments: curl's exit code, standard output and standard error.
async Task<(int ExitCode, string Output, string Error)> Curl(params string[] arguments)
{
    CallToolResult answer = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    JsonElement root = JsonDocument.Parse(answer.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    return (root.GetProperty("exitCode").GetInt32(),
        root.GetProperty("standardOutput").GetProperty("content").GetString() ?? "",
        root.GetProperty("standardError").GetProperty("content").GetString() ?? "");
}
