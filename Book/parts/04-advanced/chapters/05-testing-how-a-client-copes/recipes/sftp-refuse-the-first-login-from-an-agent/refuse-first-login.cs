// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts an SFTP server through surl-mcp that refuses the first login attempts, uploads
// to it through curl-mcp's curl tool, and reads back the file, the logins judged and the rule.
// Run: dotnet run refuse-first-login.cs -- <case>
//   solution        refuse two attempts; curl --retry 1 --retry-all-errors
//   retry-alone     refuse two attempts; curl --retry 1 without --retry-all-errors
//   one-attempt     refuse one attempt; curl with no retry
//   wrong-password  refuse two attempts; curl --retry 1 --retry-all-errors with a wrong password
#:package ModelContextProtocol.Core@*

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

// surl --throwaway-hostkey --user alice:s3cret --allow-uploads --fault "refuse-login;times=N" sftp://127.0.0.1:0
string times = which == "one-attempt" ? "1" : "2";
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "sftp://127.0.0.1:0" },
    ["arguments"] = new[] { "--throwaway-hostkey", "--user", "alice:s3cret", "--allow-uploads", "--fault", "refuse-login;times=" + times },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The deploy step: curl's own arguments; release.txt is a file in this folder.
string[] retry = which switch
{
    "solution" or "wrong-password" => ["--retry", "1", "--retry-all-errors", "--retry-delay", "1"],
    "retry-alone" => ["--retry", "1", "--retry-delay", "1"],
    _ => [],
};
string password = which == "wrong-password" ? "wrong" : "s3cret";
CallToolResult uploaded = await curl.CallToolAsync("curl", new Dictionary<string, object?>
{
    ["args"] = new[] { "-sS", "-k", "-u", "alice:" + password }.Concat(retry)
        .Concat(new[] { "-w", "%{num_retries}", "-T", "release.txt", url + "release.txt" }).ToArray(),
});
using (JsonDocument answer = JsonDocument.Parse(uploaded.Content.OfType<TextContentBlock>().Single().Text))
{
    JsonElement root = answer.RootElement;
    string Content(string stream) => root.GetProperty(stream).ValueKind == JsonValueKind.Null ? "" : root.GetProperty(stream).GetProperty("content").GetString()!.Trim();
    Console.WriteLine($"curl: exitCode {root.GetProperty("exitCode")}, retries {Content("standardOutput")}, tool error {(uploaded.IsError == true ? "yes" : "no")}");
    Console.WriteLine($"curl stderr: {Content("standardError").Split('\n')[0].Trim()}");
}

// read_file: what the server stored.
CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = server, ["path"] = "/release.txt" });
Console.WriteLine(read.IsError == true
    ? $"read_file: {read.Content.OfType<TextContentBlock>().Single().Text}"
    : $"read_file: {read.StructuredContent!.Value.GetProperty("text").GetString()!.TrimEnd('\n')}");

// get_events: every login the server judged, in order.
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = server,
    ["kinds"] = new[] { "login_judged" },
});
foreach (JsonElement e in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    string verdict = e.GetProperty("is_accepted").GetBoolean() ? "accepted" : e.GetProperty("is_refused_by_fault").GetBoolean() ? "refused by the rule" : "refused";
    Console.WriteLine($"login_judged: exchange {e.GetProperty("exchange_id")}, {e.GetProperty("mechanism").GetString()} {e.GetProperty("account").GetString()}, {verdict}");
}

// list_faults: what is left of the rule.
CallToolResult faults = await surl.CallToolAsync("list_faults", new Dictionary<string, object?> { ["server_id"] = server });
JsonElement rule = faults.StructuredContent!.Value.GetProperty("faults")[0];
Console.WriteLine($"list_faults: {rule.GetProperty("item").GetString()} remaining {rule.GetProperty("remaining").GetRawText()}");
return 0;
