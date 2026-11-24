// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent draws up who may read and who may write a share: surl-mcp serves the share team with a
// grant for each of two groups, and the agent tries a read and a write over FTP as each account,
// and as no account, through curl-mcp's curl tool, then prints the table.
// Run: dotnet run who-may-write.cs -- <case>
//   table      every account and no account, a read and a write each (the default)
//   http       the same table over HTTP
//   anonymous  a server with --allow-anonymous: no account, then Ed
//   bare       a grant that names the group without its @
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "table";

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

// The share: the folder team under the root, editors may write it, readers may read it. The
// anonymous case shares it read-only on a server that takes every caller unchecked.
var arguments = new List<string>
{
    "--user", "ed:edpass", "--user", "erin:erinpass", "--user", "rita:ritapass", "--user", "gus:guspass",
    "--group", "editors=ed,erin", "--group", "readers=rita",
    "--share", which == "anonymous" ? "team=team;readonly" : "team=team",
    "--share-access", which == "bare" ? "team=editors:rw" : "team=@editors:rw,@readers:r",
    "--allow-uploads", "--allow-plaintext-auth",
};
if (which == "anonymous")
{
    arguments.Add("--allow-anonymous");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0", "http://127.0.0.1:0" },
    ["arguments"] = arguments,
});
if (started.IsError == true)
{
    Console.WriteLine($"start_server: tool error: {started.Content.OfType<TextContentBlock>().First().Text.Split('\n')[0]}");
    return 1;
}

JsonElement bound = started.StructuredContent!.Value.GetProperty("bound_urls");
string ftp = bound[0].GetString()!;
string http = bound[1].GetString()!;

string[] callers = which == "anonymous" ? ["(none)", "ed"] : ["ed", "erin", "rita", "gus", "(none)"];
string root = which == "http" ? http : ftp;
Console.WriteLine($"{"account",-8} {"read",-12} write");
foreach (string caller in callers)
{
    string read = await Try(caller, root + "team/plan.txt", upload: false);
    string write = await Try(caller, root + $"team/from-{caller.Trim('(', ')')}.txt", upload: true);
    Console.WriteLine($"{caller,-8} {read,-12} {write}");
}

return 0;

// One try through the curl tool: yes when curl exits 0 (and, over HTTP, the status is 2xx), else
// no with what refused it.
async Task<string> Try(string caller, string url, bool upload)
{
    var curlArgs = new List<string> { "-s", "-S", "-o", "out.txt", "-w", "%{response_code}" };
    if (caller != "(none)")
    {
        curlArgs.AddRange(["-u", $"{caller}:{caller}pass"]);
    }

    if (upload)
    {
        curlArgs.AddRange(["-T", "probe.txt"]);
    }

    curlArgs.Add(url);
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
    using JsonDocument answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text);
    int exit = answer.RootElement.GetProperty("exitCode").GetInt32();
    string code = answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!;
    if (exit != 0)
    {
        return $"no (exit {exit})";
    }

    return url.StartsWith("http") && !code.StartsWith('2') ? $"no ({code})" : "yes";
}
