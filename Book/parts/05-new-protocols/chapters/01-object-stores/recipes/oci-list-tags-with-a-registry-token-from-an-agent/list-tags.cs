// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent lists a repository's tags: surl-mcp is the registry, holding demo/app under three
// tags, and curl-mcp's curl tool logs in at the token endpoint with Basic for a token scoped to
// pull the repository, then asks for the tag list with the token as a Bearer credential.
// Run: dotnet run list-tags.cs -- <case>
//   tags       a token, then the tag list (the default)
//   no-token   the tag list with no token
//   password   the tag list with the password sent as Basic
//   wrong      a wrong password at the token endpoint
//   scope      a token scoped to another repository
//   pages      the tag list two at a time, following the Link header
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "tags";

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

// The registry: TLS with the certificate from setup, one account, and demo/app as an OCI image
// layout in memory, one manifest under three tags.
const string Digest = "sha256:267a1a95fbd5b9ea80c16d403cfb6c46dfa46b3abc615dafa13acb32e452c275";
string index = "{\"schemaVersion\":2,\"mediaType\":\"application/vnd.oci.image.index.v1+json\",\"manifests\":["
    + string.Join(",", new[] { "latest", "1.0", "1.1" }.Select(tag =>
        $"{{\"mediaType\":\"application/vnd.oci.image.manifest.v1+json\",\"digest\":\"{Digest}\",\"size\":359,\"annotations\":{{\"org.opencontainers.image.ref.name\":\"{tag}\"}}}}"))
    + "]}";
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "oci://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key", "--user", "alice:s3cret" },
    ["files"] = new Dictionary<string, string>
    {
        ["demo/app/oci-layout"] = "{\"imageLayoutVersion\":\"1.0.0\"}",
        [$"demo/app/blobs/sha256/{Digest[7..]}"] = File.ReadAllText(Path.Combine(here, "manifest.json")),
        ["demo/app/index.json"] = index,
    },
});
string registry = "https" + started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!["oci".Length..].TrimEnd('/');

switch (which)
{
    case "no-token":
        Report(await Get("/v2/demo/app/tags/list"), "WWW-Authenticate");
        return 0;
    case "password":
        Report(await Get("/v2/demo/app/tags/list", "-u", "alice:s3cret"));
        return 0;
}

// The token: a Basic login at the token endpoint, for a token scoped to pull the repository.
string scope = which == "scope" ? "repository:other/app:pull" : "repository:demo/app:pull";
(int issued, _, string answer) = await Get($"/token?service=surl&scope={scope}", "-u", which == "wrong" ? "alice:wrong" : "alice:s3cret");
if (issued != 200)
{
    Console.WriteLine($"token endpoint: {issued}, no token");
    return 1;
}

using JsonDocument json = JsonDocument.Parse(answer);
string token = json.RootElement.GetProperty("token").GetString()!;
Console.WriteLine($"token endpoint: {issued}, a token good for {json.RootElement.GetProperty("expires_in")} s");

// The tag list with the token; with pages, two at a time, following each Link header.
string? next = which == "pages" ? "/v2/demo/app/tags/list?n=2" : "/v2/demo/app/tags/list";
int status = 0;
while (next is not null)
{
    (int code, Dictionary<string, string> headers, string body) = await Get(next, "--oauth2-bearer", token);
    Report((code, headers, body), "Link");
    status = code;
    next = headers.TryGetValue("link", out string? link) ? link.Split('<', '>')[1] : null;
}

return status == 200 ? 0 : 1;

// One GET through the curl tool, with -D - so the status line and headers come before the body
// in standardOutput; answers the status, the headers (names in lower case) and the body.
async Task<(int Code, Dictionary<string, string> Headers, string Body)> Get(string path, params string[] login)
{
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "--cacert", "server.pem", "-D", "-" }.Concat(login).Append(registry + path).ToArray(),
    });
    using JsonDocument result = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text);
    string output = result.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!;
    int split = output.IndexOf("\r\n\r\n", StringComparison.Ordinal);
    string[] head = output[..split].Split("\r\n");
    var headers = head.Skip(1).Select(h => h.Split(':', 2)).ToDictionary(h => h[0].ToLowerInvariant(), h => h[1].Trim());
    return (int.Parse(head[0].Split(' ')[1]), headers, output[(split + 4)..]);
}

void Report((int Code, Dictionary<string, string> Headers, string Body) got, string? header = null)
{
    Console.WriteLine(header is "WWW-Authenticate" ? $"{got.Code} {got.Headers["www-authenticate"]}" : header is null ? $"{got.Code}" : $"{got.Code} {got.Body}");
    if (header == "Link" && got.Headers.TryGetValue("link", out string? link))
    {
        Console.WriteLine($"Link: {link}");
    }
}
