// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent explains an S3 SignatureDoesNotMatch: surl-mcp runs the S3 server, curl-mcp's curl tool sends
// the client's request, and the agent reads the signed request from get_exchanges, signs it again with
// the account's secret under a few readings, and names the part the client signed differently.
// server.pem and server.key are in the folder.
// Run: dotnet run explain-signature.cs -- <case>
//   path     an app's own signer puts the bucket in the path it signs (virtual-hosted request)
//   secret   curl signs with a wrong secret
//   right    curl signs with the right secret
#:package ModelContextProtocol.Core@*

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "path";
const string Key = "AKIAIOSFODNN7EXAMPLE", Secret = "s3cret", Host = "photos.s3.us-east-1.amazonaws.com";

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

// S3 over TLS, one key pair, and a bucket holding one object.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "s3://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key", "--user", $"{Key}:{Secret}" },
    ["files"] = new Dictionary<string, string> { ["photos/hello.txt"] = "hello\n" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
int port = new Uri(started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!).Port;

// The client's request for photos/hello.txt.
string[] reach = ["-s", "-S", "--cacert", "server.pem", "--connect-to", $"::127.0.0.1:{port}", "-w", "\n%{http_code}"];
string[] request = which switch
{
    // An app's own signer, which signs /photos/hello.txt but sends the virtual-hosted /hello.txt.
    "path" => [.. reach, .. AppSigner("/photos/hello.txt"), $"https://{Host}/hello.txt"],
    "secret" => [.. reach, "-u", $"{Key}:not-the-secret", "--aws-sigv4", "aws:amz:us-east-1:s3", $"https://{Host}/hello.txt"],
    _ => [.. reach, "-u", $"{Key}:{Secret}", "--aws-sigv4", "aws:amz:us-east-1:s3", $"https://{Host}/hello.txt"],
};
CallToolResult sent = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = request });
using JsonDocument answer = JsonDocument.Parse(sent.Content.OfType<TextContentBlock>().Single().Text);
string[] output = answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.Split('\n');
string body = string.Join("\n", output[..^1]);
string code = body.Contains("<Code>") ? body.Split("<Code>")[1].Split("</Code>")[0] : "";
Console.WriteLine($"client: {output[^1]} {code}".TrimEnd());

// The agent: wait for the exchange to end, then read the request as the server received it.
CallToolResult ended = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "exchange_ended" }, ["timeout_ms"] = 10000,
});
long exchange = ended.StructuredContent!.Value.GetProperty("event").GetProperty("exchange_id").GetInt64();
CallToolResult got = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?>
{
    ["server_id"] = id, ["after_exchange_id"] = exchange - 1, ["max_exchanges"] = 1,
});
string received = string.Concat(got.StructuredContent!.Value.GetProperty("exchanges")[0].GetProperty("chunks").EnumerateArray()
    .Where(c => c.GetProperty("direction").GetString() == "received").Select(c => c.GetProperty("data").GetString()));
string[] head = received[..received.IndexOf("\r\n\r\n")].Split("\r\n");
string[] line = head[0].Split(' ');
string method = line[0], path = line[1].Split('?')[0], query = line[1].Contains('?') ? line[1].Split('?')[1] : "";
Dictionary<string, string> headers = head.Skip(1).Select(h => h.Split(':', 2))
    .ToDictionary(h => h[0].Trim().ToLowerInvariant(), h => h[1].Trim());

// The Authorization header names what the client signed: the scope, the signed headers and the signature.
string auth = headers["authorization"];
string scope = auth.Split("Credential=")[1].Split(',')[0].Split('/', 2)[1];
string[] signedHeaders = auth.Split("SignedHeaders=")[1].Split(',')[0].Split(';');
string signature = auth.Split("Signature=")[1].Trim();
Console.WriteLine($"signed: {method} {path}, scope {scope.Split('/', 2)[1]}, headers {string.Join(";", signedHeaders)}");

// Sign the request again with the account's secret: as it arrived, then under readings a client might have used.
var readings = new (string Name, string Path, string Payload)[]
{
    ("as received", path, headers["x-amz-content-sha256"]),
    ("with the bucket in the path", "/photos" + path, headers["x-amz-content-sha256"]),
    ("with UNSIGNED-PAYLOAD as the payload hash", path, "UNSIGNED-PAYLOAD"),
};
string? matched = null;
foreach ((string name, string signedPath, string payload) in readings)
{
    bool same = Sign(method, signedPath, query, signedHeaders.Select(h => (h, headers[h])), payload, headers["x-amz-date"], scope, Secret) == signature;
    Console.WriteLine($"{name}: {(same ? "matches" : "differs")}");
    matched ??= same ? name : null;
}

Console.WriteLine(matched switch
{
    "as received" => "diagnosis: the signature is right for the request as it arrived",
    null => "diagnosis: no reading reproduces the client's signature, which leaves the signing key: check the secret",
    _ => $"diagnosis: the client signed the request {matched}: the canonical path it signed is not {path}, the path it sent",
});
return 0;

// The client side of the "path" case: an app's own SigV4 signer that signs the path-style path.
string[] AppSigner(string signedPath)
{
    string date = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
    string payload = Convert.ToHexStringLower(SHA256.HashData([]));
    string scope = $"{date[..8]}/us-east-1/s3/aws4_request";
    string sig = Sign("GET", signedPath, "", [("host", Host), ("x-amz-content-sha256", payload), ("x-amz-date", date)], payload, date, scope, Secret);
    return ["-H", $"x-amz-date: {date}", "-H", $"x-amz-content-sha256: {payload}",
        "-H", $"Authorization: AWS4-HMAC-SHA256 Credential={Key}/{scope}, SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature={sig}"];
}

// Signature Version 4: the canonical request, the string to sign, and the key derived from the secret.
static string Sign(string method, string path, string query, IEnumerable<(string Name, string Value)> headers, string payload, string date, string scope, string secret)
{
    var signed = headers.ToList();
    string canonical = string.Join("\n", method, path, query,
        string.Concat(signed.Select(h => $"{h.Name}:{h.Value}\n")), string.Join(";", signed.Select(h => h.Name)), payload);
    string toSign = string.Join("\n", "AWS4-HMAC-SHA256", date, scope, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))));
    byte[] key = Encoding.UTF8.GetBytes("AWS4" + secret);
    foreach (string part in scope.Split('/'))
    {
        key = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(part));
    }

    return Convert.ToHexStringLower(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(toSign)));
}
