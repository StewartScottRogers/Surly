// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts one server that offers a folder as a share to FTP only, with surl-mcp, then tries the share
// over FTP, HTTP and FTPS with curl-mcp and reports which schemes reach it, over the official MCP SDK.
// Run: dotnet run ftp-only-share.cs -- MODE
//   survey         try drop/report.txt over each scheme and report which reach it
//   http           the same path over HTTP, with the status
//   open-http, open-ftp  public/readme.txt, a share with no limit, over each
//   ftps           the FTP-only share over FTPS
//   root           the names an FTP listing of the root holds
//   http-folder    the FTP-only share's folder over HTTP
//   unknown-scheme a protocols= value that names no scheme
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "survey";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. The shares are folders in it.
string here = Directory.GetCurrentDirectory();
Directory.CreateDirectory("drop");
Directory.CreateDirectory("public");
File.WriteAllText("drop/report.txt", "totals\n");
File.WriteAllText("public/readme.txt", "open to all\n");

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

// surl-mcp: FTP, HTTP and FTPS listeners on free ports over one served root. --share is surl's own option, in
// surl's own words; --self-signed gives FTPS a throwaway certificate, for a test on your own machine only.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0", "http://127.0.0.1:0", "ftps://127.0.0.1:0" },
    ["arguments"] = new[]
    {
        "--allow-anonymous", "--list-directories", "--self-signed",
        "--share", "drop=drop;protocols=" + (which == "unknown-scheme" ? "nope" : "ftp"),
        "--share", "public=public",
    },
});
if (started.IsError == true)
{
    Console.WriteLine($"start refused: {started.Content.OfType<TextContentBlock>().Single().Text.Split('\n')[0]}");
    return 0;
}

JsonElement bound = started.StructuredContent!.Value.GetProperty("bound_urls");
string ftp = bound[0].GetString()!, http = bound[1].GetString()!, ftps = bound[2].GetString()!;

switch (which)
{
    case "survey":
        // An agent finds out by trying: a path is reached when curl exits 0, and -f makes HTTP's 404 an exit 22.
        var notReached = new List<string>();
        foreach ((string scheme, string url) in new[] { ("ftp", ftp), ("http", http), ("ftps", ftps) })
        {
            (int exit, string text) = await Fetch(url + "drop/report.txt");
            Console.WriteLine($"{scheme}: {(exit == 0 ? "reached, " + text : "exit " + exit)}");
            if (exit != 0) notReached.Add(scheme);
        }

        Console.WriteLine($"not reached: {string.Join(", ", notReached)}");
        break;
    case "http":
        Console.WriteLine($"http: {await Status(http + "drop/report.txt")}");
        break;
    case "open-http":
        Console.WriteLine($"open-http: {(await Fetch(http + "public/readme.txt")).Text}");
        break;
    case "open-ftp":
        Console.WriteLine($"open-ftp: {(await Fetch(ftp + "public/readme.txt")).Text}");
        break;
    case "ftps":
        Console.WriteLine($"ftps: {(await Fetch(ftps + "drop/report.txt")).Text}");
        break;
    case "root":
        Console.WriteLine($"root: {(await Fetch(ftp, "-l")).Text.Replace("\r", "").Replace("\n", " ")}");
        break;
    case "http-folder":
        Console.WriteLine($"http-folder: {await Status(http + "drop/")}");
        break;
}

return 0;

// curl-mcp curl: the command line as args. -s keeps the progress meter out, -k accepts the throwaway FTPS
// certificate, -f makes a refusal an exit code. Returns curl's exit code and what it printed.
async Task<(int Exit, string Text)> Fetch(string url, string extra = "-s")
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-k", "-f", extra, url },
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return (answer.RootElement.GetProperty("exitCode").GetInt32(),
        answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.TrimEnd());
}

// The HTTP status of a fetch: -o to a file and -w for the code, without -f.
async Task<string> Status(string url)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-o", "got.txt", "-w", "%{http_code}", url },
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!;
}
