// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent waits for a file an app uploaded to one surl-mcp server and hands it to a second one,
// so the next stage of a test can fetch it. Run: dotnet run program.cs -- <case>
//   solution   a text report: upload, wait, read_file, put_file, then fetch over HTTP and FTP
//   binary     a 300-byte file that is not text, handed over as base64
//   events     the second server's record of the hand-over
//   missing    nothing was uploaded: the wait times out and read_file is a tool error
//   wait-first the wait is sent before the upload
//   put-twice  a second put_file over the same path
//   put-both   put_file given both text and base64
#:package ModelContextProtocol.Core@2.4.0

using System.Security.Cryptography;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "solution";

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

// The app's file: a text report, or bytes that are not text.
string name = which == "binary" ? "blob.bin" : "report.txt";
if (which == "binary")
{
    File.WriteAllBytes(Path.Combine(here, name), Enumerable.Range(0, 300).Select(i => (byte)(i * 7 + 200)).ToArray());
}

// Server one takes the app's upload. Server two is where the next stage looks, over HTTP and FTP.
// It has an in folder to write into.
JsonElement one = await Start(["http://127.0.0.1:0"], new() { ["in/README.txt"] = "Uploads go here.\n" }, "--allow-anonymous", "--allow-uploads");
JsonElement two = await Start(["http://127.0.0.1:0", "ftp://127.0.0.1:0"], [], "--allow-anonymous");
string oneId = one.GetProperty("server_id").GetString()!, twoId = two.GetProperty("server_id").GetString()!;
string oneUrl = one.GetProperty("bound_urls")[0].GetString()!;
string twoHttp = two.GetProperty("bound_urls")[0].GetString()!, twoFtp = two.GetProperty("bound_urls")[1].GetString()!;

// In wait-first the wait is sent before the upload and answers when the upload happens.
Task<JsonElement>? early = which == "wait-first" ? WaitForUpload(5000) : null;
if (which != "missing")
{
    // The app: curl uploads the file to server one.
    JsonElement ran = await Call(curl, "curl", new()
    {
        ["args"] = new[] { "-s", "-T", name, "-o", "reply.txt", "-w", "%{http_code}", oneUrl + "in/" + name },
    });
    Console.WriteLine($"app upload: {ran.GetProperty("standardOutput").GetProperty("content").GetString()}");
}

// The agent waits for the file to arrive, with a bound, instead of reading the store and hoping.
JsonElement waited = early is not null ? await early : await WaitForUpload(which == "missing" ? 300 : 5000);
if (waited.TryGetProperty("event", out JsonElement arrived))
{
    Console.WriteLine($"wait_for_event: {arrived.GetProperty("change").GetString()} {arrived.GetProperty("name").GetString()} {arrived.GetProperty("byte_count")} bytes");
}
else
{
    Console.WriteLine($"wait_for_event: timed_out {waited.GetProperty("timed_out")}");
    CallToolResult gone = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = oneId, ["path"] = "/in/" + name });
    Console.WriteLine($"read_file: tool error {(gone.IsError == true ? "yes" : "no")}, {gone.Content.OfType<TextContentBlock>().Single().Text}");
    return 0;
}

// Read it from server one, and put it into server two exactly as read: text as text, anything else as base64.
JsonElement file = await Call(surl, "read_file", new() { ["server_id"] = oneId, ["path"] = "/in/" + name });
Console.WriteLine($"read_file: {(file.TryGetProperty("text", out _) ? "text" : "base64")}, {file.GetProperty("byte_count")} bytes");
Console.WriteLine($"before put_file, server two: {(await Fetch(twoHttp + "handed/" + name)).Status}");
var put = new Dictionary<string, object?> { ["server_id"] = twoId, ["path"] = "/handed/" + name };
put[file.TryGetProperty("text", out JsonElement text) ? "text" : "base64"] = file.TryGetProperty("text", out _) ? text.GetString() : file.GetProperty("base64").GetString();
await Call(surl, "put_file", put);

// The next stage fetches it from server two.
(int status, byte[] bytes) = await Fetch(twoHttp + "handed/" + name);
Console.WriteLine($"fetch over http: {status}, {bytes.Length} bytes, same as the upload: {SHA256.HashData(bytes).SequenceEqual(SHA256.HashData(File.ReadAllBytes(Path.Combine(here, name))))}");
(int ftpStatus, byte[] ftpBytes) = await Fetch(twoFtp + "handed/" + name);
Console.WriteLine($"fetch over ftp: {ftpStatus}, {ftpBytes.Length} bytes, same as the upload: {ftpBytes.SequenceEqual(bytes)}");

if (which == "events")
{
    JsonElement events = await Call(surl, "get_events", new() { ["server_id"] = twoId, ["kinds"] = new[] { "store_changed" } });
    foreach (JsonElement e in events.GetProperty("events").EnumerateArray())
    {
        Console.WriteLine($"server two store_changed: {e.GetProperty("change").GetString()} {e.GetProperty("name").GetString()}");
    }

    JsonElement oneEvents = await Call(surl, "get_events", new() { ["server_id"] = oneId, ["kinds"] = new[] { "store_changed" } });
    Console.WriteLine($"server one store_changed: {oneEvents.GetProperty("events").GetArrayLength()}");
}

if (which == "put-twice")
{
    // put_file over a path that is there replaces it.
    await Call(surl, "put_file", new() { ["server_id"] = twoId, ["path"] = "/handed/" + name, ["text"] = "second version\n" });
    (int again, byte[] second) = await Fetch(twoHttp + "handed/" + name);
    Console.WriteLine($"fetch after a second put_file: {again}, {System.Text.Encoding.UTF8.GetString(second).TrimEnd()}");
}

if (which == "put-both")
{
    // put_file takes text or base64, not both.
    CallToolResult both = await surl.CallToolAsync("put_file", new Dictionary<string, object?>
    {
        ["server_id"] = twoId, ["path"] = "/handed/both.txt", ["text"] = "a", ["base64"] = "YQ==",
    });
    Console.WriteLine($"put_file with both: tool error {(both.IsError == true ? "yes" : "no")}, {both.Content.OfType<TextContentBlock>().Single().Text}");
}

return 0;

// The wait for the upload: the first change under /in, or timed_out true after timeout_ms.
async Task<JsonElement> WaitForUpload(int timeout) => await Call(surl, "wait_for_event", new()
{
    ["server_id"] = oneId, ["store"] = "content", ["name"] = "/in", ["timeout_ms"] = timeout,
});

// start_server answers its structured content: server_id and bound_urls.
async Task<JsonElement> Start(string[] listen, Dictionary<string, string> files, params string[] options) =>
    await Call(surl, "start_server", new() { ["listen_urls"] = listen, ["arguments"] = options, ["files"] = files });

// A tool call answered as structured content, or as text JSON for curl-mcp.
async Task<JsonElement> Call(McpClient client, string tool, Dictionary<string, object?> arguments)
{
    CallToolResult result = await client.CallToolAsync(tool, arguments);
    return result.StructuredContent ?? JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}

// transfer answers the status and the body as text or, for bytes that are not text, base64.
async Task<(int Status, byte[] Body)> Fetch(string url)
{
    JsonElement answer = await Call(curl, "transfer", new() { ["url"] = url, ["timeoutSeconds"] = 20 });
    JsonElement body = answer.GetProperty("body");
    int status = answer.GetProperty("statusCode").GetInt32();
    if (body.ValueKind != JsonValueKind.Object) return (status, []);
    string content = body.GetProperty("content").GetString()!;
    return (status, body.GetProperty("isBase64").GetBoolean() ? Convert.FromBase64String(content) : System.Text.Encoding.UTF8.GetBytes(content));
}
