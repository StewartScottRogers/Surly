// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Starts surl as an MCP server program over standard input and output, from .NET.
// Run: dotnet run start-over-stdio.cs -- <case>
//   call         Curl.Api starts surl and calls read_file (the default)
//   list         the same URL with no Body: the tool list
//   no-login     list, with an account given to surl and no --allow-anonymous
//   write        call write_file without --allow-uploads
//   upload       call write_file with --allow-uploads
//   missing      an mcp+stdio URL naming a program that does not exist
//   by-hand      the program starts surl itself and speaks JSON-RPC on its pipes
//   in-process   ask Surl.Api to host mcp+stdio:
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Diagnostics;
using System.Text.Json.Nodes;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "call";
const string Read = """{"name":"read_file","arguments":{"path":"hello.txt"}}""";
const string Write = """{"name":"write_file","arguments":{"path":"notes.txt","text":"written over stdio\n"}}""";

if (which == "in-process")
{
    try
    {
        await using SurlServer server = await SurlServer.StartAsync(new SurlServerOptions { ListenUrls = { "mcp+stdio:" } });
        return 0;
    }
    catch (SurlServerStartException e)
    {
        Console.WriteLine($"start refused: {e.ExitCode} ({(int)e.ExitCode})");
        Console.WriteLine(e.Message);
        return (int)e.ExitCode;
    }
}

if (which == "by-hand")
{
    return await ByHand();
}

// The URL names the program and, after ?, its arguments split at &. The Body, if any, is the tools/call.
string url = which switch
{
    "no-login" => "mcp+stdio://localhost/surl?--directory&site&--user&alice:secret&mcp+stdio:",
    "upload" => "mcp+stdio://localhost/surl?--directory&site&--allow-uploads&mcp+stdio:",
    "missing" => "mcp+stdio://localhost/no-such-server?mcp+stdio:",
    _ => "mcp+stdio://localhost/surl?--directory&site&mcp+stdio:",
};
CurlRequest request = which switch
{
    "call" => new CurlRequest(HttpMethod.Post, url) { Body = System.Text.Encoding.UTF8.GetBytes(Read) },
    "write" or "upload" => new CurlRequest(HttpMethod.Post, url) { Body = System.Text.Encoding.UTF8.GetBytes(Write) },
    _ => new CurlRequest(HttpMethod.Get, url),
};

await using CurlClient client = CurlClient.Create();
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
try
{
    CurlResponse response = await client.SendAsync(request, timeout.Token);
    await using Stream body = response.Body;
    Console.Write(await new StreamReader(body).ReadToEndAsync());
    return 0;
}
catch (CurlException e)
{
    Console.WriteLine($"refused: {e.Code} ({(int)e.Code}): {e.Message}");
    return (int)e.Code;
}

// The program as the MCP client: it starts surl with mcp+stdio: and writes one JSON-RPC message a line.
static async Task<int> ByHand()
{
    var start = new ProcessStartInfo("surl")
    {
        ArgumentList = { "--directory", "site", "mcp+stdio:" },
        RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
    };
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
    JsonNode pong = await Ask("ping", "{}");
    Console.WriteLine($"ping: {pong["result"]!.ToJsonString()}");

    // Closing surl's input ends the session; surl exits on its own.
    surl.StandardInput.Close();
    await surl.WaitForExitAsync();
    Console.WriteLine($"surl exited {surl.ExitCode}");
    Console.WriteLine($"surl's log: {(await log).Trim()}");
    return 0;
}
