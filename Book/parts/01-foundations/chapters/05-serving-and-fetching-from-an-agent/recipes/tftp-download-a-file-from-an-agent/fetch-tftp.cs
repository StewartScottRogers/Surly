// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent serves a file over TFTP with surl-mcp and fetches it with curl-mcp, over the official MCP SDK.
// Run: dotnet run fetch-tftp.cs [-- <case>]
//   fetch         the curl tool downloads hello.txt, and surl-mcp's record shows the read request (the default)
//   transfer      the transfer tool downloads the same file
//   save          the curl tool saves the file with -o into the folder it is confined to
//   missing       a file the server does not have
//   dot-file      a file whose name begins with a dot, which the server holds but does not serve
//   upload        curl -T to a server that was not told to accept uploads
//   upload-on     curl -T to a server started with --allow-uploads, then read the file back from its store
#:package ModelContextProtocol.Core@2.4.0

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "fetch";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it.
string here = Directory.GetCurrentDirectory();
File.WriteAllText("upload.txt", "sent by the agent\n");
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

// surl-mcp: a TFTP server on a free UDP port, with two files in memory. Uploads are off unless the
// run is "upload-on", which passes surl's option of that name as surl's own words.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "tftp://127.0.0.1:0" },
    ["arguments"] = which == "upload-on" ? new[] { "--allow-uploads" } : Array.Empty<string>(),
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n", [".secret"] = "not for you\n" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string root = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "fetch":
        JsonElement fetched = await Curl(["-s", root + "hello.txt"]);
        Console.WriteLine($"exit: {fetched.GetProperty("exitCode")}");
        Console.Write(fetched.GetProperty("standardOutput").GetProperty("content").GetString());

        // The first packet the server received, a TFTP read request: opcode 1, the file name, the mode.
        JsonElement first = (await RecordedAsync(answer => answer.GetProperty("exchanges").GetArrayLength() > 0)).GetProperty("exchanges")[0].GetProperty("chunks")[0];
        byte[] packet = Convert.FromBase64String(first.GetProperty("data").GetString()!);
        string[] fields = Encoding.ASCII.GetString(packet[2..]).Split('\0');
        Console.WriteLine($"server received: opcode {packet[1]}, {fields[0]}, {fields[1]}");
        break;
    case "transfer":
        // transfer describes the request by fields; a TFTP answer has no HTTP status, so statusCode is 0.
        CallToolResult transferred = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = root + "hello.txt" });
        JsonElement answer = JsonDocument.Parse(transferred.Content.OfType<TextContentBlock>().Single().Text).RootElement;
        Console.WriteLine($"statusCode: {answer.GetProperty("statusCode")}");
        Console.Write(answer.GetProperty("body").GetProperty("content").GetString());
        break;
    case "save":
        JsonElement saved = await Curl(["-s", "-o", "copy.txt", root + "hello.txt"]);
        Console.WriteLine($"exit: {saved.GetProperty("exitCode")}");
        Console.Write(File.ReadAllText("copy.txt"));
        break;
    case "missing" or "dot-file":
        JsonElement refused = await Curl(["-s", root + (which == "missing" ? "missing.txt" : ".secret")]);
        Console.WriteLine($"exit: {refused.GetProperty("exitCode")}, {refused.GetProperty("failure").GetProperty("text")}");
        break;
    case "upload" or "upload-on":
        JsonElement uploaded = await Curl(["-s", "-T", "upload.txt", root + "upload.txt"]);
        Console.WriteLine($"exit: {uploaded.GetProperty("exitCode")}");
        if (which == "upload")
        {
            Console.WriteLine(uploaded.GetProperty("failure").GetProperty("text"));
        }
        else
        {
            // The file the server now holds, read through surl-mcp's own tool. The store can trail curl by
            // a moment, so ask again until the file is there, for up to 10 s.
            CallToolResult stored = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = "/upload.txt" });
            for (var waited = System.Diagnostics.Stopwatch.StartNew(); stored.IsError == true && waited.Elapsed.TotalSeconds < 10;)
            {
                await Task.Delay(100);
                stored = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = "/upload.txt" });
            }

            Console.Write(stored.StructuredContent!.Value.GetProperty("text").GetString());
        }

        break;
}

return 0;

// One curl-mcp call: curl with these arguments. The answer is text, JSON, and a failed run is still an answer.
async Task<JsonElement> Curl(string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}

// get_exchanges, asked again until the record holds what ready looks for and two answers 100 ms apart
// agree: the server's record can trail curl by a moment. At most 10 s, then the last answer.
async Task<JsonElement> RecordedAsync(Func<JsonElement, bool> ready, int after = 0)
{
    JsonElement last = default;
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); waited.Elapsed.TotalSeconds < 10; await Task.Delay(100))
    {
        CallToolResult result = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId, ["after_exchange_id"] = after });
        JsonElement answer = result.StructuredContent!.Value.Clone();
        if (ready(answer) && last.ValueKind != JsonValueKind.Undefined && answer.GetRawText() == last.GetRawText())
        {
            return answer;
        }

        last = answer;
    }

    return last;
}
