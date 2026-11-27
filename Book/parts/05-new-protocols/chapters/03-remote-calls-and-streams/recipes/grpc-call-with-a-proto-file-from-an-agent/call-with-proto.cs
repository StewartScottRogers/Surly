// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent calls a gRPC method with a .proto file and by reflection: surl-mcp starts the server and reports
// what it saw, curl-mcp makes the calls. Run: dotnet run call-with-proto.cs -- <case>
//   solution        a call with echo.proto
//   reflection      the same call with no file
//   streaming       a server-streaming method with the file
//   names           renamed.proto, whose field names differ from the server's
//   method          a method the file does not declare
//   file-problems   a file that is missing and one that is not a proto file
//   wrong-service   a file that names a service the server does not serve
//   bytes           what the server sent for the call with the file and for the call by reflection
#:package ModelContextProtocol.Core@2.4.0

using System.Text.RegularExpressions;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "solution";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. The .proto files
// the recipe lists as fixtures are in it.
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

// A gRPC server on a free port. It serves surl.test.Echo and reflection, and takes anonymous callers.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "grpc://127.0.0.1:0" },
    ["arguments"] = new[] { "--allow-anonymous" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string echo = url + "surl.test.Echo/Echo";

switch (which)
{
    case "solution":
        await Call("--grpc-proto", "echo.proto", "-d", """{"message":"hello"}""", echo);
        break;
    case "reflection":
        await Call("-d", """{"message":"hello"}""", echo);
        break;
    case "streaming":
        await Call("--grpc-proto", "echo.proto", "-d", """{"message":"hi","count":2}""", url + "surl.test.Echo/Expand");
        break;
    case "names":
        await Call("--grpc-proto", "renamed.proto", "-d", """{"text":"hello"}""", echo);
        await Call("--grpc-proto", "renamed.proto", "-d", """{"message":"hello"}""", echo);
        await Call("-d", """{"text":"hello"}""", echo);
        break;
    case "method":
        await Call("--grpc-proto", "renamed.proto", "-d", "{}", url + "surl.test.Echo/Chat");
        break;
    case "file-problems":
        await Call("--grpc-proto", "missing.proto", "-d", "{}", echo);
        await Call("--grpc-proto", "bad.proto", "-d", "{}", echo);
        break;
    case "wrong-service":
        await Call("--grpc-proto", "other.proto", "-d", """{"text":"x"}""", url + "shop.Echo/Echo");
        break;
    case "bytes":
        await Call("--grpc-proto", "echo.proto", "-d", """{"message":"hello"}""", echo);
        await Call("-d", """{"message":"hello"}""", echo);
        await Bytes();
        break;
    default:
        Console.Error.WriteLine($"unknown case {which}");
        return 2;
}

return 0;

// The curl tool runs curl's own arguments, as the console would. -sS keeps the progress meter out and the
// error message in. The reply is JSON, printed on one line; an error is curl's first line on standard error.
async Task Call(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = arguments.Prepend("-sS").ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement root = answer.RootElement;
    int exit = root.GetProperty("exitCode").GetInt32();
    string output = Regex.Replace(root.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim(), @"\s+", " ");
    string error = root.GetProperty("standardError").GetProperty("content").GetString()!.Trim();
    Console.WriteLine($"curl exit {exit}, tool error {(result.IsError == true ? "yes" : "no")}: {(exit == 0 ? output : error)}");
}

// get_events lists what the server saw as events; each exchange ends with the bytes it sent. A call
// by reflection makes the server send the service's descriptor too, so it sends more. The second
// exchange can end a moment after curl returns, so ask again until both have, for up to 10 s.
async Task Bytes()
{
    long[] sent = [];
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); sent.Length < 2 && waited.Elapsed.TotalSeconds < 10; await Task.Delay(100))
    {
        CallToolResult result = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = server });
        sent = result.StructuredContent!.Value.GetProperty("events").EnumerateArray()
            .Where(e => e.GetProperty("kind").GetString() == "exchange_ended")
            .Select(e => e.GetProperty("sent_byte_count").GetInt64()).ToArray();
    }
    if (sent.Length < 2)
    {
        Console.WriteLine($"exchanges: {sent.Length} after 10 s, expected 2");
        return;
    }
    Console.WriteLine($"exchanges: {sent.Length}; the call by reflection made the server send more than the call with the file: {sent[1] > sent[0]}");
}
