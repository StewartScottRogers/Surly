// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a Redis server with one account using surl-mcp, seeds a key, and logs in with
// curl-mcp's curl tool.
// Run: dotnet run program.cs -- <scheme> <login> [plaintext] [untrusted]
//   rediss alice:s3cret   TLS and the right password
//   rediss alice:wrong    TLS and a wrong password
//   rediss none           TLS and no login
//   redis alice:s3cret    no TLS, the right password; add plaintext to loosen the server
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string scheme = args[0];
string login = args[1];
bool plaintext = args.Contains("plaintext");
bool untrusted = args.Contains("untrusted");

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. It holds
// server.pem and server.key, which openssl wrote.
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

// surl-mcp: one account; --allow-plaintext-auth only when asked for.
var serverArguments = new List<string> { "--user", "alice:s3cret" };
if (scheme == "rediss") serverArguments.AddRange(["--cert", "server.pem", "--key", "server.key"]);
if (plaintext) serverArguments.Add("--allow-plaintext-auth");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { $"{scheme}://127.0.0.1:0" },
    ["arguments"] = serverArguments,
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// surl-mcp: seed a key. This needs no login: the tool writes into the server's memory.
await surl.CallToolAsync("set_key", new Dictionary<string, object?>
{
    ["server_id"] = server, ["key"] = "greeting", ["value"] = "hello",
});

// curl-mcp's curl tool takes curl's arguments, one list item each: -u is the login, --cacert the
// certificate to trust. -sS hides the progress meter and keeps the error text.
var curlArgs = new List<string> { "-sS" };
if (!untrusted) curlArgs.AddRange(["--cacert", "server.pem"]);
if (login != "none") curlArgs.AddRange(["-u", login]);
curlArgs.Add(url + "GET/greeting");
CallToolResult read = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
JsonElement answer = JsonDocument.Parse(read.Content.OfType<TextContentBlock>().Single().Text).RootElement;
Console.WriteLine($"curl exit {answer.GetProperty("exitCode").GetInt32()}");
Console.WriteLine($"out: {Text(answer.GetProperty("standardOutput"))}");
Console.WriteLine($"err: {Text(answer.GetProperty("standardError"))}");
return 0;

// A stream is null when empty, else an object with content.
static string Text(JsonElement stream) =>
    stream.ValueKind == JsonValueKind.Null ? "" : stream.GetProperty("content").GetString()!.Trim();
