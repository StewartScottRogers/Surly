// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent finishes a half-uploaded file on an FTP server it starts with surl-mcp: it reads the size of the file
// the server holds, resumes the upload with curl -C - -T through curl-mcp, and compares what the server then
// holds with the local file, over the official MCP SDK.
// Run: dotnet run resume-upload.cs -- MODE
//   resume       a server holding the first 65,536 bytes of 200,000, resumed
//   size         the size the server reports for that partial file, before the resume
//   commands     the SIZE, APPE and STOR the server received for that resume
//   fresh        a server holding nothing
//   complete     a server holding the whole file
//   restart      the upload with no -C, over the partial file
//   offset       -C 100000, so no size is asked for
//   zeros        a partial file of the right length with other bytes in it
//   longer       a server file longer than the local one
//   uploads-off  a server started without --allow-uploads
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "resume";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it.
string here = Directory.GetCurrentDirectory();

// The local file, 200,000 letters the same on every run, where curl -T reads it from.
var random = new Random(2026);
string local = new string(Enumerable.Range(0, 200_000).Select(_ => (char)('a' + random.Next(26))).ToArray());
File.WriteAllText("update.iso", local);

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

// What the interrupted upload left on the server: the first 65,536 letters unless the mode says otherwise.
string partial = mode switch
{
    "fresh" => "",
    "complete" => local,
    "zeros" => new string('0', 65_536),
    "longer" => new string('0', 300_000),
    _ => local[..65_536],
};
var files = new Dictionary<string, string>();
if (partial.Length > 0) files["update.iso"] = partial;

// surl-mcp: an FTP server on a free port holding that partial file in memory. --allow-uploads permits APPE as it
// permits STOR, and --allow-anonymous lets an anonymous login in and checks nothing: both for a test on your own
// machine only. The refusal run leaves --allow-uploads out.
var surlArguments = new List<string> { "--allow-anonymous" };
if (mode != "uploads-off") surlArguments.Add("--allow-uploads");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = surlArguments,
    ["files"] = files,
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "update.iso";

if (mode == "size")
{
    // curl -I asks for the size and the date and prints them as headers.
    (_, string headers) = await Curl("-s", "-I", url);
    string length = headers.Split("\r\n").Single(line => line.StartsWith("Content-Length:"));
    Console.WriteLine($"size: {length["Content-Length:".Length..].Trim()}");
    return 0;
}

// The resume: curl -C - makes curl ask for the size itself; -C 100000 gives the offset; no -C sends it all.
string offset = mode switch { "restart" => "", "offset" => "100000", _ => "-" };
var resume = new List<string> { "-s", "-w", "%{size_upload}" };
if (offset != "") resume.AddRange(["-C", offset]);
resume.AddRange(["-T", "update.iso", url]);
(int exit, string sent) = await Curl(resume.ToArray());

string held = await ReadFile();
switch (mode)
{
    case "commands" or "offset":
        Console.WriteLine($"{mode}: sent {sent}; {string.Join(", ", await Commands())}");
        break;
    case "uploads-off":
        Console.WriteLine($"uploads-off: curl exit {exit}, server holds {held.Length}");
        break;
    case "longer":
        Console.WriteLine($"longer: sent {sent}, server holds {held.Length}");
        break;
    case "complete":
        Console.WriteLine($"complete: sent {sent}");
        break;
    default:
        Console.WriteLine($"{mode}: curl exit {exit}, sent {sent}, same as local: {held == local}");
        break;
}

return 0;

// One curl-mcp call: the answer is JSON text with the exit code and what curl printed.
async Task<(int Exit, string Output)> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return (answer.RootElement.GetProperty("exitCode").GetInt32(),
        answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!);
}

// surl-mcp read_file: what the server holds now.
async Task<string> ReadFile()
{
    CallToolResult result = await surl.CallToolAsync("read_file", new Dictionary<string, object?>
    {
        ["server_id"] = serverId, ["path"] = "/update.iso",
    });
    return result.StructuredContent!.Value.GetProperty("text").GetString()!;
}

// surl-mcp get_exchanges: the commands that ask for a size or store bytes, in order, with the names they carried.
// The record can trail curl by a moment, so ask again until it holds the upload's APPE or STOR, for up to 10 s.
async Task<List<string>> Commands()
{
    DateTime deadline = DateTime.UtcNow.AddSeconds(10);
    List<string> commands = await Recorded();
    while (!commands.Any(c => c.StartsWith("APPE ") || c.StartsWith("STOR ")) && DateTime.UtcNow < deadline)
    {
        await Task.Delay(100);
        commands = await Recorded();
    }

    return commands;
}

async Task<List<string>> Recorded()
{
    CallToolResult result = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId });
    return result.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray()
        .SelectMany(exchange => exchange.GetProperty("chunks").EnumerateArray())
        .Where(chunk => chunk.GetProperty("direction").GetString() == "received")
        .Select(chunk => chunk.GetProperty("data").GetString()!.Split("\r\n")[0])
        .Where(line => line.StartsWith("SIZE ") || line.StartsWith("APPE ") || line.StartsWith("STOR ") || line.StartsWith("REST "))
        .ToList();
}
