// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent maps a bucket onto a folder with surl-mcp, and reads, lists and writes it with curl-mcp, over the
// official MCP SDK. Run: dotnet run program.cs
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. server.pem and
// server.key (a certificate and key for 127.0.0.1 and every bucket host name), up.txt and a folder spare
// holding spare/gallery/sunset.txt are in it.
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

// surl-mcp: one server, an s3 and an https listener on one store held in memory. The folder gallery is the
// bucket pics, and other is a folder nobody mapped.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "s3://127.0.0.1:0", "https://127.0.0.1:0" },
    ["arguments"] = new[]
    {
        "--cert", "server.pem", "--key", "server.key", "--user", "AKIAIOSFODNN7EXAMPLE:s3cret",
        "--bucket", "pics=/gallery", "--allow-uploads", "--list-directories",
    },
    ["files"] = new Dictionary<string, string>
    {
        ["gallery/sunset.txt"] = "sunset\n",
        ["gallery/2026/jan.txt"] = "january\n",
        ["other/o.txt"] = "other\n",
    },
});
JsonElement answer = started.StructuredContent!.Value;
string id = answer.GetProperty("server_id").GetString()!;
int s3Port = new Uri(answer.GetProperty("bound_urls")[0].GetString()!).Port;
string httpsUrl = answer.GetProperty("bound_urls")[1].GetString()!;

const string Login = "AKIAIOSFODNN7EXAMPLE:s3cret";
string toServer = $"::127.0.0.1:{s3Port}";
await Fetch("pics get", "--connect-to", toServer, "-u", Login, "s3://pics/sunset.txt");
await Fetch("pics list", "--connect-to", toServer, "-u", Login, "s3://pics/");
await Fetch("gallery get", "--connect-to", toServer, "-u", Login, "s3://gallery/sunset.txt");
await Fetch("other get", "--connect-to", toServer, "-u", Login, "s3://other/o.txt");
await Fetch("no bucket", "--connect-to", toServer, "-u", Login, "s3://videos/sunset.txt");
await Fetch("https gallery", "-u", Login, httpsUrl + "gallery/sunset.txt");
await Fetch("https pics", "-f", "-u", Login, httpsUrl + "pics/sunset.txt");
await Fetch("put", "--connect-to", toServer, "-u", Login, "-T", "up.txt", "s3://pics/up.txt");

// surl-mcp reads the store back by the folder's path, and the put is in gallery, not in a folder pics.
CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = "/gallery/up.txt" });
Console.WriteLine($"read_file /gallery/up.txt: {read.StructuredContent!.Value.GetProperty("text").GetString()!.TrimEnd()}");
CallToolResult root = await surl.CallToolAsync("list_files", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = "/" });
Console.WriteLine($"list_files /: {string.Join(", ", root.StructuredContent!.Value.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("name").GetString()))}");

// A bucket mapped onto a folder that is not on disk stops the start: a tool error with surl's exit code.
CallToolResult broken = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "s3://127.0.0.1:0" },
    ["arguments"] = new[] { "--directory", "spare", "--bucket", "pics=/nowhere", "--cert", "server.pem", "--key", "server.key", "--user", Login },
});
Console.WriteLine($"missing folder: tool error {broken.IsError == true}, exit_code {broken.StructuredContent?.GetProperty("exit_code")}");
Console.WriteLine(broken.Content.OfType<TextContentBlock>().Single().Text.Split('\n')[0]);
return 0;

// curl-mcp's curl tool runs curl's own arguments and answers in text, as JSON. --connect-to sends the
// connection to the server whatever bucket host the URL names; --cacert trusts its certificate. The program
// prints the exit code and the lines of standard output, or the first line of standard error.
async Task Fetch(string label, params string[] more)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-sS", "--cacert", "server.pem" }.Concat(more).ToArray(),
    });
    using JsonDocument reply = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string output = reply.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r", "");
    string error = reply.RootElement.GetProperty("standardError").GetProperty("content").GetString()!.Replace("\r", "").Split('\n')[0];
    string lines = string.Join(", ", output.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    Console.WriteLine($"{label}: exit {reply.RootElement.GetProperty("exitCode")} [{lines}] {error.Replace("curl: (22) ", "")}".TrimEnd());
}
