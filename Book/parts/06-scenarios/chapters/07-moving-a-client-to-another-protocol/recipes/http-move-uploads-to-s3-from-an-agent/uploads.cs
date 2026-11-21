// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent moves an app's uploads from HTTP PUT to S3: surl-mcp serves one store over HTTPS and S3,
// curl-mcp uploads once each way, and the agent reads the store and the server's record back, over the
// official MCP SDK.
// Run: dotnet run uploads.cs [-- across|codes|refused|cutover]
//   (no argument) one upload each way, the bucket listed, the store and the record read back
//   across        each upload read back over the other protocol
//   codes         the status of a first upload and of a replacing one, over each protocol
//   refused       an HTTP PUT with no login, an unsigned S3 put, and one signed with the wrong secret
//   cutover       a server that takes signed requests only
// server.pem and server.key come from setup: in recipe.yml; report.csv and summary.csv are fixtures.
#:package ModelContextProtocol.Core@2.2.0

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "move";
const string Login = "AKIAIOSFODNN7EXAMPLE:s3cret";

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

// surl-mcp: one store, an s3:// listener for the new client and an https:// one for the old, one key pair
// for both, uploads on, in surl's own words. "cutover" adds --auth aws-sigv4: signed requests only.
var surlArguments = new List<string>
{
    "--cert", "server.pem", "--key", "server.key", "--user", Login, "--allow-uploads", "--list-directories",
};
if (which == "cutover")
{
    surlArguments.AddRange(["--auth", "aws-sigv4"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "s3://127.0.0.1:0", "https://127.0.0.1:0" },
    ["arguments"] = surlArguments,
    ["files"] = new Dictionary<string, string> { ["uploads/readme.txt"] = "uploads land here\n" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string[] urls = started.StructuredContent!.Value.GetProperty("bound_urls").EnumerateArray().Select(u => u.GetString()!).ToArray();
string s3Port = new Uri(urls[0]).Port.ToString();
string https = urls[1];

switch (which)
{
    case "move":
        Console.WriteLine($"old client, HTTP PUT report.csv: {(await Https("-T", "report.csv", "-w", "%{http_code}", https + "uploads/report.csv")).Out}");
        Console.WriteLine($"new client, S3 put summary.csv: {(await S3("-T", "summary.csv", "-w", "%{http_code}", "s3://uploads/summary.csv")).Out}");
        Console.Write((await S3("s3://uploads/")).Out);
        Console.WriteLine($"store: {string.Join(", ", await Names())}");
        Console.WriteLine($"store: report.csv {await Stored("report.csv", "report.csv")}, summary.csv {await Stored("summary.csv", "summary.csv")}");
        foreach (JsonElement e in (await Call(surl, "get_events", new() { ["server_id"] = serverId, ["kinds"] = new[] { "login_judged" } })).GetProperty("events").EnumerateArray())
        {
            Console.WriteLine($"login judged: {e.GetProperty("scheme").GetString()} {e.GetProperty("mechanism").GetString()} {e.GetProperty("account").GetString()}");
        }

        break;
    case "across":
        await Https("-T", "report.csv", https + "uploads/report.csv");
        await S3("-T", "summary.csv", "s3://uploads/summary.csv");
        string overS3 = (await S3("s3://uploads/report.csv")).Out;
        string overHttps = (await Https(https + "uploads/summary.csv")).Out;
        Console.WriteLine($"HTTP upload read over S3: {(overS3 == File.ReadAllText("report.csv") ? "identical" : "DIFFERENT")}");
        Console.WriteLine($"S3 upload read over HTTPS: {(overHttps == File.ReadAllText("summary.csv") ? "identical" : "DIFFERENT")}");
        break;
    case "codes":
        Console.WriteLine($"HTTP PUT, new: {(await Https("-T", "report.csv", "-w", "%{http_code}", https + "uploads/again.csv")).Out}");
        Console.WriteLine($"HTTP PUT, replacing: {(await Https("-T", "report.csv", "-w", "%{http_code}", https + "uploads/again.csv")).Out}");
        Console.WriteLine($"S3 put, new: {(await S3("-T", "summary.csv", "-w", "%{http_code}", "s3://uploads/twice.csv")).Out}");
        Console.WriteLine($"S3 put, replacing: {(await S3("-T", "summary.csv", "-w", "%{http_code}", "s3://uploads/twice.csv")).Out}");
        break;
    case "refused":
        Console.WriteLine($"HTTP PUT, no login: {(await Curl("-s", "--cacert", "server.pem", "-T", "report.csv", "-w", "%{http_code}", https + "uploads/nobody.csv")).Out}");
        var unsigned = await Curl("-sS", "--cacert", "server.pem", "--connect-to", $"::127.0.0.1:{s3Port}", "-T", "summary.csv", "s3://uploads/nobody.csv");
        Console.WriteLine($"S3 put, unsigned: exit {unsigned.Code}, {unsigned.Err.Trim()}");
        var wrong = await Curl("-sS", "--cacert", "server.pem", "--connect-to", $"::127.0.0.1:{s3Port}", "-u", "AKIAIOSFODNN7EXAMPLE:wrong", "-T", "summary.csv", "s3://uploads/nobody.csv");
        Console.WriteLine($"S3 put, wrong secret: exit {wrong.Code}, {wrong.Err.Trim()}");
        Console.WriteLine($"store: nobody.csv {await Stored("nobody.csv", "report.csv")}");
        break;
    case "cutover":
        Console.WriteLine($"old client, HTTP PUT report.csv: {(await Https("-T", "report.csv", "-w", "%{http_code}", https + "uploads/report.csv")).Out}");
        Console.WriteLine($"store: report.csv {await Stored("report.csv", "report.csv")}");
        Console.WriteLine($"new client, S3 put report.csv: {(await S3("-T", "report.csv", "-w", "%{http_code}", "s3://uploads/report.csv")).Out}");
        Console.WriteLine($"store: report.csv {await Stored("report.csv", "report.csv")}");
        break;
}

return 0;

// One tool call; the structured answer, or the text answer parsed as JSON (curl-mcp answers in text).
async Task<JsonElement> Call(McpClient host, string tool, Dictionary<string, object?> arguments)
{
    CallToolResult result = await host.CallToolAsync(tool, arguments);
    return result.StructuredContent ?? JsonDocument.Parse(result.Content.OfType<TextContentBlock>().First().Text).RootElement;
}

// One curl-mcp call: the exit code, standard output and standard error.
async Task<(int Code, string Out, string Err)> Curl(params string[] curlArguments)
{
    JsonElement answer = await Call(curl, "curl", new() { ["args"] = curlArguments });
    return (answer.GetProperty("exitCode").GetInt32(),
        answer.GetProperty("standardOutput").GetProperty("content").GetString()!,
        answer.GetProperty("standardError").GetProperty("content").GetString()!);
}

// The old client: HTTPS with the key pair as user and password, Basic over TLS.
Task<(int Code, string Out, string Err)> Https(params string[] rest) =>
    Curl(["-s", "--cacert", "server.pem", "-u", Login, .. rest]);

// The new client: an s3:// URL signed with the key pair, sent to the server's s3 port.
Task<(int Code, string Out, string Err)> S3(params string[] rest) =>
    Curl(["-s", "--cacert", "server.pem", "--connect-to", $"::127.0.0.1:{s3Port}", "-u", Login, .. rest]);

// The names surl-mcp's list_files finds in the bucket's folder.
async Task<IEnumerable<string>> Names() =>
    (await Call(surl, "list_files", new() { ["server_id"] = serverId, ["path"] = "/uploads" }))
        .GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("name").GetString()!);

// What the store holds under /uploads, read with read_file, against a local file: the same, other, or nothing.
async Task<string> Stored(string name, string local)
{
    CallToolResult result = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = $"/uploads/{name}" });
    if (result.IsError == true)
    {
        return "absent";
    }

    return result.StructuredContent!.Value.GetProperty("text").GetString() == File.ReadAllText(local) ? "identical" : "DIFFERENT";
}
