// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent moves a client from FTP to SFTP: surl-mcp serves both from one store, curl-mcp fetches the
// same report over each, and the copies and the server's recorded logins are compared, over the
// official MCP SDK.
// Run: dotnet run move.cs [-- listing|missing|password|pin|wrong-pin|cutover]
//   (no argument) the report over both protocols, compared, and the logins the server recorded
//   listing       the folder's names over both
//   missing       a file that is not there, over both
//   password      a wrong password, over both
//   pin           SFTP pinned to the server's host key
//   wrong-pin     SFTP pinned to a fingerprint that is not the server's
//   cutover       a server that refuses plaintext passwords: FTP is refused, SFTP is served
// host_key and host_key.pub come from setup: in recipe.yml.
#:package ModelContextProtocol.Core@2.2.0

using System.Security.Cryptography;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "move";

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

// The report: 3,000 characters of numbered lines, text so that it travels as a string.
string report = string.Concat(Enumerable.Range(0, 300).Select(i => $"row {i:D5}\n"))[..3000];

// surl-mcp: both listeners, one store, one account, in surl's own words. The old client's password
// crosses the wire in clear, so FTP needs --allow-plaintext-auth, which "cutover" leaves out.
var surlArguments = new List<string> { "--hostkey", "host_key", "--user", "alice:s3cret", "--list-directories" };
if (which != "cutover")
{
    surlArguments.Add("--allow-plaintext-auth");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0", "sftp://127.0.0.1:0" },
    ["arguments"] = surlArguments,
    ["files"] = new Dictionary<string, string> { ["report.bin"] = report, ["notes.txt"] = "release notes\n" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string[] urls = started.StructuredContent!.Value.GetProperty("bound_urls").EnumerateArray().Select(u => u.GetString()!).ToArray();
string ftp = urls[0];
string sftp = urls[1];

// The pin: the SHA-256 of the host key's blob, in base64 without padding, as ssh-keygen -l writes it.
byte[] blob = Convert.FromBase64String(File.ReadAllText("host_key.pub").Split(' ')[1]);
string pin = Convert.ToBase64String(SHA256.HashData(blob)).TrimEnd('=');

switch (which)
{
    case "move":
        string overFtp = (await Fetch("over ftp", ftp + "report.bin", "s3cret", null))!;
        string overSftp = (await Fetch("over sftp", sftp + "report.bin", "s3cret", null))!;
        Console.WriteLine(overFtp == overSftp && overFtp == report
            ? "the two copies are identical to each other and to the server's"
            : "the copies DIFFER");
        Console.WriteLine($"ftp logins accepted for alice: {await Logins("ftp")}");
        Console.WriteLine($"sftp logins accepted for alice: {await Logins("sftp")}");
        break;
    case "listing":
        Console.WriteLine($"ftp: {await Names(ftp)}");
        Console.WriteLine($"sftp: {await Names(sftp)}");
        break;
    case "missing":
        await Fetch("ftp", ftp + "nope.bin", "s3cret", null);
        await Fetch("sftp", sftp + "nope.bin", "s3cret", null);
        break;
    case "password":
        await Fetch("ftp", ftp + "report.bin", "wrong", null);
        await Fetch("sftp", sftp + "report.bin", "wrong", null);
        break;
    case "pin":
        string? pinned = await Fetch("pinned sftp", sftp + "report.bin", "s3cret", pin);
        Console.WriteLine(pinned == report ? "identical to the server's" : "DIFFERENT from the server's");
        break;
    case "wrong-pin":
        await Fetch("pinned sftp", sftp + "report.bin", "s3cret", new string('A', 43));
        break;
    case "cutover":
        await Fetch("ftp", ftp + "report.bin", "s3cret", null);
        string? kept = await Fetch("sftp", sftp + "report.bin", "s3cret", null);
        Console.WriteLine(kept == report ? "identical to the server's" : "DIFFERENT from the server's");
        break;
}

return 0;

// One curl-mcp call: the exit code, what curl wrote to standard output. curl-mcp answers in text, as JSON.
async Task<(int Code, string Out)> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    return (answer.GetProperty("exitCode").GetInt32(), answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r", ""));
}

// One fetch: the text, or null with the exit code that refused it. A hostPin of null skips the host key
// check (curl -k), which is for a key you made yourself; a pin checks it.
async Task<string?> Fetch(string label, string url, string password, string? hostPin)
{
    string[] check = url.StartsWith("sftp:") ? (hostPin is null ? ["-k"] : ["--hostpubsha256", hostPin]) : [];
    (int code, string output) = await Curl(["-s", .. check, "-u", $"alice:{password}", url]);
    Console.WriteLine(code == 0 ? $"{label}: {output.Length} bytes" : $"{label}: exit {code}");
    return code == 0 ? output : null;
}

// The names of the root folder: curl -l, one a line, in order.
async Task<string> Names(string url)
{
    string[] check = url.StartsWith("sftp:") ? ["-k"] : [];
    (int _, string output) = await Curl(["-s", "-l", .. check, "-u", "alice:s3cret", url]);
    return string.Join(" ", output.Split('\n', StringSplitOptions.RemoveEmptyEntries));
}

// The logins surl-mcp judged for one scheme, from its login_judged events: how many were accepted for alice.
async Task<int> Logins(string scheme)
{
    CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
    {
        ["server_id"] = serverId,
        ["kinds"] = new[] { "login_judged" },
        ["scheme"] = scheme,
    });
    return events.StructuredContent!.Value.GetProperty("events").EnumerateArray()
        .Count(e => e.GetProperty("account").GetString() == "alice" && e.GetProperty("is_accepted").GetBoolean());
}
