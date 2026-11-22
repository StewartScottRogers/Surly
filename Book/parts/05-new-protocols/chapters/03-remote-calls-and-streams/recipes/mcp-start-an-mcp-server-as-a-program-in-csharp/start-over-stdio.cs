// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Starts surl as an MCP server program over standard input and output, from .NET.
// Run: dotnet run start-over-stdio.cs -- <case>
//   list         Curl.Api starts surl from an mcp+stdio URL and lists its tools
//   curl-body    the same request with a tools/call as its Body
//   call         the program starts surl itself and calls read_file (the default)
//   no-login     the same, with an account and no --allow-anonymous: list the tools
//   write        call write_file without --allow-uploads
//   upload       call write_file with --allow-uploads
//   in-process   ask Surl.Api to host mcp+stdio:
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Curl.Api;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "call";
const string Read = """{"name":"read_file","arguments":{"path":"hello.txt"}}""";
const string Write = """{"name":"write_file","arguments":{"path":"notes.txt","text":"written over stdio\n"}}""";

switch (which)
{
    case "list" or "curl-body":
        // Curl.Api starts the program the URL names, with the query's items as its arguments.
        await using (CurlClient client = CurlClient.Create())
        {
            const string url = "mcp+stdio://localhost/surl?--directory&site&mcp+stdio:";
            CurlRequest request = which == "curl-body"
                ? new CurlRequest(HttpMethod.Post, url) { Body = System.Text.Encoding.UTF8.GetBytes(Read) }
                : new CurlRequest(HttpMethod.Get, url);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            CurlResponse response = await client.SendAsync(request, timeout.Token);
            await using Stream body = response.Body;
            Console.Write(await new StreamReader(body).ReadToEndAsync());
        }

        return 0;

    case "in-process":
        try
        {
            await using SurlServer server = await SurlServer.StartAsync(new SurlServerOptions { ListenUrls = { "mcp+stdio:" } });
            Console.WriteLine("started");
            return 0;
        }
        catch (Exception e)
        {
            Console.WriteLine($"StartAsync refused mcp+stdio: ({e.GetType().Name})");
            return 1;
        }
}

// The program is the MCP client: it starts surl with mcp+stdio: and speaks JSON-RPC, one message a line.
var start = new ProcessStartInfo("surl")
{
    RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
};
string[] arguments = which switch
{
    "no-login" => ["--directory", "site", "--user", "alice:secret", "mcp+stdio:"],
    "upload" => ["--directory", "site", "--allow-uploads", "mcp+stdio:"],
    _ => ["--directory", "site", "mcp+stdio:"],
};
foreach (string argument in arguments)
{
    start.ArgumentList.Add(argument);
}

using Process surl = Process.Start(start)!;
Task<string> log = surl.StandardError.ReadToEndAsync();
surl.StandardInput.AutoFlush = true;

int id = 0;
async Task<JsonNode> Ask(string method, string parameters)
{
    id++;
    await surl.StandardInput.WriteLineAsync($$"""{"jsonrpc":"2.0","id":{{id}},"method":"{{method}}","params":{{parameters}}}""");
    string line = await surl.StandardOutput.ReadLineAsync() ?? throw new InvalidOperationException("surl closed its output");
    return JsonNode.Parse(line)!;
}

JsonNode hello = await Ask("initialize", """{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}""");
Console.WriteLine($"server: {hello["result"]!["serverInfo"]!["name"]}");
await surl.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");

JsonNode answer = which switch
{
    "no-login" => await Ask("tools/list", "{}"),
    "write" or "upload" => await Ask("tools/call", Write),
    _ => await Ask("tools/call", Read),
};
if (answer["error"] is JsonNode error)
{
    Console.WriteLine($"error {error["code"]}: {error["message"]}");
}
else if (which == "no-login")
{
    Console.WriteLine("tools: " + string.Join(", ", answer["result"]!["tools"]!.AsArray().Select(t => t!["name"]!.GetValue<string>())));
}
else
{
    foreach (JsonNode? item in answer["result"]!["content"]!.AsArray())
    {
        // One line per content item, as curl writes them.
        Console.WriteLine(item!["text"]!.GetValue<string>().TrimEnd('\n'));
    }
}

// Closing surl's input ends the session; surl exits on its own.
surl.StandardInput.Close();
await surl.WaitForExitAsync();
Console.WriteLine($"surl exited {surl.ExitCode}");
Console.WriteLine($"surl's log: {(await log).Trim()}");
return answer["error"] is null ? 0 : 1;
