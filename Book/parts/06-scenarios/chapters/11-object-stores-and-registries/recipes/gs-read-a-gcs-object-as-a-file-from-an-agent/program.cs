// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent checks that objects written through Cloud Storage can be read as files: surl-mcp serves one store
// as gs:// and https://, curl-mcp writes through gs:// and reads over https://, and read_file shows the stored
// file. server.pem, server.key, note.txt, report.txt and tail.txt are in the folder.
// Run: dotnet run program.cs [-- <case>]
//   write-and-read (default)  put an object through gs://, read it over https://, read the stored file
//   no-trailing-newline       an object whose last byte is not a newline
//   nested-name               an object name with slashes
//   file-is-an-object         an object that was already in the store, read through gs://
//   put-with-the-token        a put with the OAuth token, read with the HMAC key pair over https://
//   content-type              the Content-Type of an object read over https://
//   https-needs-a-login       a read over https:// with no login
//   wrong-secret              a put signed with the wrong secret
//   refused-is-not-written    the same refused put, then a read of the name
//   missing-object            a get of a name that holds nothing
//   uploads-off               a put to a server without --allow-uploads
//   uploads-off-reads         reads from that server
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "write-and-read";

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

// surl-mcp: one store in memory served as Cloud Storage and as HTTPS, with an HMAC key pair and an OAuth token.
var surlArguments = new List<string>
{
    "--cert", "server.pem", "--key", "server.key", "--user", "GOOGTESTKEY:s3cret", "--user", ":ya29.t0ken",
};
if (!which.StartsWith("uploads-off")) surlArguments.Add("--allow-uploads");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "gs://127.0.0.1:0", "https://127.0.0.1:0" },
    ["arguments"] = surlArguments,
    ["files"] = new Dictionary<string, string> { ["photos/seed.txt"] = "already here\n" },
});
JsonElement server = started.StructuredContent!.Value;
string serverId = server.GetProperty("server_id").GetString()!;
// bound_urls has one address per listen URL, in order: gs:// first, https:// second.
int gsPort = new Uri(server.GetProperty("bound_urls")[0].GetString()!).Port;
string https = $"https://127.0.0.1:{new Uri(server.GetProperty("bound_urls")[1].GetString()!).Port}";

// The two ways to log in, and where gs:// goes: Google's host name, routed to the server.
string[] hmac = ["-u", "GOOGTESTKEY:s3cret"];
string[] token = ["--oauth2-bearer", "ya29.t0ken"];
string[] viaServer = ["--cacert", "server.pem", "--connect-to", $"::127.0.0.1:{gsPort}"];
string[] trust = ["--cacert", "server.pem"];

switch (which)
{
    case "no-trailing-newline":
        // -o keeps the bytes in a file, so what curl wrote can be compared; -w reports how many arrived.
        await Curl(["-sS", .. viaServer, .. hmac, "-T", "tail.txt", "gs://photos/tail.txt"]);
        var (_, _, size) = await Curl(["-sS", .. trust, .. token, "-o", "got.txt", "-w", "%{size_download}", $"{https}/photos/tail.txt"]);
        Console.WriteLine($"read {size} bytes, identical to the upload: {File.ReadAllBytes("got.txt").AsSpan().SequenceEqual(File.ReadAllBytes("tail.txt"))}");
        break;

    case "nested-name":
        await Curl(["-sS", .. viaServer, .. hmac, "-T", "report.txt", "gs://photos/2026/q1/report.txt"]);
        Console.WriteLine($"read: {(await Curl(["-sS", .. trust, .. token, $"{https}/photos/2026/q1/report.txt"])).Text}");
        Console.WriteLine($"stored at /photos/2026/q1/report.txt: {await Stored("/photos/2026/q1/report.txt")}");
        break;

    case "file-is-an-object":
        Console.WriteLine($"get: {Line(await Curl(["-sS", .. viaServer, .. hmac, "gs://photos/seed.txt"]))}");
        break;

    case "put-with-the-token":
        Console.WriteLine($"put: {Line(await Curl(["-sS", .. viaServer, .. token, "-T", "note.txt", "gs://photos/by-token.txt"]))}");
        Console.WriteLine($"read: {Line(await Curl(["-sS", .. trust, .. hmac, $"{https}/photos/by-token.txt"]))}");
        break;

    case "content-type":
        Console.WriteLine($"Content-Type: {(await Curl(["-sS", .. trust, .. token, "-o", "seed.out", "-w", "%{content_type}", $"{https}/photos/seed.txt"])).Text}");
        break;

    case "https-needs-a-login":
        Console.WriteLine($"read with no login: {(await Curl(["-sS", .. trust, "-o", "none.out", "-w", "%{http_code}", $"{https}/photos/seed.txt"])).Text}");
        break;

    case "wrong-secret":
        Console.WriteLine($"put: {Line(await Curl(["-sS", .. viaServer, "-u", "GOOGTESTKEY:wrong", "-T", "note.txt", "gs://photos/refused.txt"]))}");
        break;

    case "refused-is-not-written":
        Console.WriteLine($"put: {Line(await Curl(["-sS", .. viaServer, "-u", "GOOGTESTKEY:wrong", "-T", "note.txt", "gs://photos/refused.txt"]))}");
        Console.WriteLine($"read over https: {(await Curl(["-sS", .. trust, .. token, "-o", "none.out", "-w", "%{http_code}", $"{https}/photos/refused.txt"])).Text}");
        Console.WriteLine($"store: {await Stored("/photos/refused.txt")}");
        break;

    case "missing-object":
        Console.WriteLine($"get: {Line(await Curl(["-sS", .. viaServer, .. hmac, "gs://photos/missing.txt"]))}");
        break;

    case "uploads-off":
        Console.WriteLine($"put: {Line(await Curl(["-sS", .. viaServer, .. hmac, "-T", "note.txt", "gs://photos/note.txt"]))}");
        break;

    case "uploads-off-reads":
        Console.WriteLine($"gs get: {Line(await Curl(["-sS", .. viaServer, .. hmac, "gs://photos/seed.txt"]))}");
        Console.WriteLine($"https get: {Line(await Curl(["-sS", .. trust, .. hmac, $"{https}/photos/seed.txt"]))}");
        break;

    default:
        // One curl-mcp call with two transfers: the write through gs:// with the HMAC key pair, the read over
        // https:// with the token. --next gives the second its own options.
        var both = await Curl(["-sS", .. viaServer, .. hmac, "-T", "note.txt", "gs://photos/note.txt", "--next", "-sS", .. trust, .. token, $"{https}/photos/note.txt"]);
        Console.WriteLine($"put and read: exit {both.Exit}");
        Console.WriteLine($"read: {both.Text}");
        Console.WriteLine($"stored: {await Stored("/photos/note.txt")}");
        break;
}

return 0;

// One curl-mcp call: curl's exit code, whether the tool called it an error, and what it wrote. On success the
// text is standard output; a refusal is a tool error whose text is curl's own message.
async Task<(int Exit, bool IsError, string Text)> Curl(IEnumerable<string> argv)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = argv.ToList() });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string Stream(string name) => (answer.RootElement.GetProperty(name).GetProperty("content").GetString() ?? "").Replace("\r", "").Trim();
    int exit = answer.RootElement.GetProperty("exitCode").GetInt32();
    return (exit, result.IsError == true, exit == 0 ? Stream("standardOutput") : Stream("standardError"));
}

// A line for a call: the body, or for a refusal the exit code and the status and error code from curl's message.
string Line((int Exit, bool IsError, string Text) call)
{
    if (call.Exit == 0) return call.Text.Length > 0 ? call.Text : "done";
    Match named = Regex.Match(call.Text, @"\(\d+\) Cloud Storage returned error (\d+): (\w+)");
    return $"exit {call.Exit}, tool error: {(call.IsError ? "yes" : "no")}, {named.Groups[1]} {named.Groups[2]}";
}

// What surl-mcp's read_file says of a stored object: its text, or that it is not there.
async Task<string> Stored(string path)
{
    CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = path });
    return read.IsError == true ? "no such object" : read.StructuredContent!.Value.GetProperty("text").GetString()!.TrimEnd();
}
