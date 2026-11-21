// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a surl-mcp SFTP server with a narrowed cipher and MAC list, fetches a file through
// curl-mcp and reads the server's offer back from get_exchanges. The keys come from setup: in
// recipe.yml: host_key and client_key, each with a .pub half.
// Run: dotnet run offer.cs [-- narrowed|file|other-cipher|other-mac|default-offer|default-macs|two|unknown-cipher|unknown-mac|weak-cipher|weak-allowed]
#:package ModelContextProtocol.Core@2.2.0

using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string scenario = args.Length > 0 ? args[0] : "narrowed";
string[] extra = scenario switch
{
    "narrowed" or "file" or "other-cipher" or "other-mac" => ["--ssh-ciphers", "aes256-ctr", "--ssh-macs", "hmac-sha2-256"],
    "two" => ["--ssh-ciphers", "aes256-ctr,aes128-ctr", "--ssh-macs", "hmac-sha2-256"],
    "unknown-cipher" => ["--ssh-ciphers", "bogus"],
    "unknown-mac" => ["--ssh-macs", "bogus"],
    "weak-cipher" => ["--ssh-ciphers", "aes128-cbc"],
    "weak-allowed" => ["--allow-weak-ssh-algorithms", "--ssh-ciphers", "aes128-cbc"],
    _ => [],
};

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it, and the key files lie in it.
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

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "sftp://127.0.0.1:0" },
    ["arguments"] = new[] { "--hostkey", "host_key", "--authorized-keys", "alice:client_key.pub" }.Concat(extra).ToArray(),
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});

// A refused option is a tool error carrying surl's own line.
if (started.IsError == true)
{
    string message = started.Content.OfType<TextContentBlock>().First().Text;
    Console.WriteLine("start_server refused: " + message.Split('\n').First(l => l.Contains("surl:") || l.Contains("--ssh")).Trim());
    return 0;
}
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "hello.txt";

// curl-mcp's curl tool logs in with the client key; -k skips the host key check.
CallToolResult fetched = await curl.CallToolAsync("curl", new Dictionary<string, object?>
{
    ["args"] = new[] { "-s", "-S", "-k", "-u", "alice:", "--key", "client_key", "--pubkey", "client_key.pub", url },
});
using JsonDocument answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text);
string text = answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!;

// get_exchanges: the server's key exchange message follows its version line, in the first sent chunks.
CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = server });
var sent = new List<byte>();
foreach (JsonElement chunk in recorded.StructuredContent!.Value.GetProperty("exchanges")[0].GetProperty("chunks").EnumerateArray())
{
    if (chunk.GetProperty("direction").GetString() != "sent") continue;
    string data = chunk.GetProperty("data").GetString()!;
    sent.AddRange(chunk.GetProperty("encoding").GetString() == "base64" ? Convert.FromBase64String(data) : Encoding.Latin1.GetBytes(data));
}
byte[] bytes = sent.ToArray();
int at = Array.IndexOf(bytes, (byte)'\n') + 1 + 4 + 1 + 1 + 16;
var lists = new List<string[]>();
for (int i = 0; i < 6; i++)
{
    int length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(at, 4));
    lists.Add(Encoding.ASCII.GetString(bytes, at + 4, length).Split(','));
    at += 4 + length;
}
string[] ciphers = lists[2], macs = lists[4];

switch (scenario)
{
    case "narrowed" or "two":
        Console.WriteLine($"ciphers: {string.Join(", ", ciphers)}");
        Console.WriteLine($"MACs: {string.Join(", ", macs)}");
        break;
    case "file": Console.Write(text); break;
    case "other-cipher": Console.WriteLine($"offers aes128-ctr: {ciphers.Contains("aes128-ctr")}"); break;
    case "other-mac": Console.WriteLine($"offers hmac-sha2-512: {macs.Contains("hmac-sha2-512")}"); break;
    case "default-macs": Console.WriteLine($"first MAC: {macs[0]}"); break;
    case "default-offer":
        Console.WriteLine($"first cipher: {ciphers[0]}");
        Console.WriteLine($"offers aes256-ctr as well: {ciphers.Contains("aes256-ctr")}");
        Console.WriteLine($"offers more than one MAC: {macs.Length > 1}");
        Console.WriteLine($"offers a CBC cipher: {ciphers.Any(c => c.EndsWith("-cbc"))}");
        break;
    default: Console.WriteLine($"ciphers: {string.Join(", ", ciphers)}"); break;
}
return 0;
