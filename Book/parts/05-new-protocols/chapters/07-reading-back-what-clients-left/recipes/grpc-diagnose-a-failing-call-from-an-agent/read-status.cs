// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent diagnoses a failing gRPC call from the server's record: surl-mcp runs the gRPC server,
// curl-mcp's curl tool makes the test's call, and the agent reads the exchange from get_exchanges,
// takes the HTTP/2 frames apart, decodes their HPACK-compressed headers and reports, call by call,
// the path the client asked for and the grpc-status and grpc-message the server sent.
// Run: dotnet run read-status.cs -- <case>
//   notfound  the call is answered status 5 with a message (the default)
//   percent   the message holds a percent sign
//   ok        the call succeeds
//   nologin   the server has no --allow-anonymous
#:package ModelContextProtocol.Core@2.4.0

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "notfound";

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

// gRPC on a free port; surl.test.Echo answers with the status the request asks for.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "grpc://127.0.0.1:0" },
    ["arguments"] = mode == "nologin" ? Array.Empty<string>() : new[] { "--allow-anonymous" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string grpc = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The test's call.
string request = mode switch
{
    "notfound" => """{"message":"order 1042","statusCode":5,"statusMessage":"no such order"}""",
    "percent" => """{"message":"order 1042","statusCode":8,"statusMessage":"disk 100% full"}""",
    _ => """{"message":"order 1042"}""",
};
CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = new[] { "-s", "-S", "-d", request, grpc + "surl.test.Echo/Echo" } });
JsonElement test = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
string said = test.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd();
Console.WriteLine($"test: curl exit {test.GetProperty("exitCode").GetInt32()}{(said == "" ? "" : ", " + said)}");

// The agent's part: wait for the exchange to end, then read it back.
await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "exchange_ended" }, ["timeout_ms"] = 10000 });
CallToolResult got = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["scheme"] = "grpc" });
JsonElement exchange = got.StructuredContent!.Value.GetProperty("exchanges")[0];

// Each direction's bytes, joined: the client's start with the 24-byte HTTP/2 preface.
byte[] Joined(string direction) => exchange.GetProperty("chunks").EnumerateArray()
    .Where(c => c.GetProperty("direction").GetString() == direction)
    .SelectMany(c => c.GetProperty("encoding").GetString() == "base64" ? Convert.FromBase64String(c.GetProperty("data").GetString()!) : Encoding.UTF8.GetBytes(c.GetProperty("data").GetString()!))
    .ToArray();
Dictionary<int, List<(string Name, string Value)>> asked = Headers(Joined("received")[24..]);
Dictionary<int, List<(string Name, string Value)>> answered = Headers(Joined("sent"));

string[] codes = ["OK", "CANCELLED", "UNKNOWN", "INVALID_ARGUMENT", "DEADLINE_EXCEEDED", "NOT_FOUND", "ALREADY_EXISTS", "PERMISSION_DENIED", "RESOURCE_EXHAUSTED", "FAILED_PRECONDITION", "ABORTED", "OUT_OF_RANGE", "UNIMPLEMENTED", "INTERNAL", "UNAVAILABLE", "DATA_LOSS", "UNAUTHENTICATED"];
bool failed = false;
foreach ((int stream, List<(string Name, string Value)> headers) in answered.OrderBy(a => a.Key))
{
    string path = asked[stream].First(h => h.Name == ":path").Value;
    string status = headers.LastOrDefault(h => h.Name == "grpc-status").Value ?? "(none)";
    string? message = headers.LastOrDefault(h => h.Name == "grpc-message").Value;
    string report = $"{path}: grpc-status {status}" + (int.TryParse(status, out int code) && code < codes.Length ? $" ({codes[code]})" : "");
    if (message is not null)
    {
        // grpc-message is percent-encoded on the wire.
        report += $", grpc-message \"{Uri.UnescapeDataString(message)}\"" + (message.Contains('%') ? $" (sent as \"{message}\")" : "");
    }

    Console.WriteLine($"agent: {report}");
    failed |= status != "0";
}

return failed ? 1 : 0;

// HTTP/2 frames: 3 bytes of length, a type, flags and 4 bytes of stream; HEADERS (1) and CONTINUATION (9)
// carry a header block, which is decoded in order with one HPACK table per direction (RFC 7541).
static Dictionary<int, List<(string, string)>> Headers(byte[] bytes)
{
    var streams = new Dictionary<int, List<(string, string)>>();
    var table = new List<(string, string)>();
    var block = new List<byte>();
    for (int at = 0; at + 9 <= bytes.Length;)
    {
        int length = bytes[at] << 16 | bytes[at + 1] << 8 | bytes[at + 2], type = bytes[at + 3], flags = bytes[at + 4];
        int stream = (bytes[at + 5] & 0x7F) << 24 | bytes[at + 6] << 16 | bytes[at + 7] << 8 | bytes[at + 8];
        byte[] payload = bytes[(at + 9)..(at + 9 + length)];
        at += 9 + length;
        if (type == 1)
        {
            int start = ((flags & 0x08) != 0 ? 1 : 0) + ((flags & 0x20) != 0 ? 5 : 0);
            block.AddRange(payload[start..(payload.Length - ((flags & 0x08) != 0 ? payload[0] : 0))]);
        }
        else if (type == 9)
        {
            block.AddRange(payload);
        }

        if ((type == 1 || type == 9) && (flags & 0x04) != 0)
        {
            if (!streams.TryGetValue(stream, out var list))
            {
                streams[stream] = list = [];
            }

            list.AddRange(Hpack(block.ToArray(), table));
            block.Clear();
        }
    }

    return streams;
}

// One HPACK header block: indexed fields, literals with or without indexing, and table size updates.
static List<(string, string)> Hpack(byte[] b, List<(string Name, string Value)> table)
{
    string[] names = ":authority :method :method :path :path :scheme :scheme :status :status :status :status :status :status :status accept-charset accept-encoding accept-language accept-ranges accept access-control-allow-origin age allow authorization cache-control content-disposition content-encoding content-language content-length content-location content-range content-type cookie date etag expect expires from host if-match if-modified-since if-none-match if-range if-unmodified-since last-modified link location max-forwards proxy-authenticate proxy-authorization range referer refresh retry-after server set-cookie strict-transport-security transfer-encoding user-agent vary via www-authenticate".Split(' ');
    (string Name, string Value) Entry(int index) => index <= 61 ? (names[index - 1], "") : table[index - 62];
    var fields = new List<(string, string)>();
    int at = 0;
    while (at < b.Length)
    {
        byte first = b[at];
        if ((first & 0x80) != 0)
        {
            fields.Add(Entry(Integer(7)));
        }
        else if ((first & 0xE0) == 0x20)
        {
            Integer(5);
        }
        else
        {
            bool indexing = (first & 0xC0) == 0x40;
            int nameIndex = Integer(indexing ? 6 : 4);
            string name = nameIndex == 0 ? Text() : Entry(nameIndex).Name;
            (string, string) field = (name, Text());
            fields.Add(field);
            if (indexing)
            {
                table.Insert(0, field);
            }
        }
    }

    return fields;

    int Integer(int prefix)
    {
        int max = (1 << prefix) - 1, value = b[at++] & max;
        for (int shift = 0; value >= max; shift += 7)
        {
            byte next = b[at++];
            value += (next & 0x7F) << shift;
            if ((next & 0x80) == 0)
            {
                break;
            }
        }

        return value;
    }

    string Text()
    {
        bool huffman = (b[at] & 0x80) != 0;
        int length = Integer(7);
        byte[] raw = b[at..(at + length)];
        at += length;
        return huffman ? Huffman(raw) : Encoding.ASCII.GetString(raw);
    }
}

// HPACK's Huffman code is canonical, so the code lengths alone rebuild it: one letter per symbol 0 to
// 256, A for 5 bits, B for 6 and so on (RFC 7541, appendix B).
static string Huffman(byte[] raw)
{
    const string lengths = "ISXXXXXXXTZXXZXXXXXXXXZXXXXXXXXXBFFHIBDGFFDGDBBBAAABBBBBBBCDKBHFIBCCCCCCCCCCCCCCCCCCCCCCDCDIOIJBKABABABBBACCBBBABCBAABCCCCCKGJIXPRPPRRRSRSSSSSTSTTRSTSSSSQRSRSSTRQPRRSSQSRRTQRSSQQRQSRSSPRRRSRRSVVPORSRUVVVWWVTUOQVWWVWTQQVVXWWWPTPQRQQSRRUUTTVSVWVVWWWWWXWWWWWVZ";
    var codes = new Dictionary<(int, int), int>();
    int code = -1, previous = 5;
    foreach (int symbol in Enumerable.Range(0, 257).OrderBy(s => lengths[s]).ThenBy(s => s))
    {
        int length = lengths[symbol] - 'A' + 5;
        code = (code + 1) << (length - previous);
        previous = length;
        codes[(length, code)] = symbol;
    }

    var text = new StringBuilder();
    int bits = 0, value = 0;
    foreach (byte b in raw)
    {
        for (int i = 7; i >= 0; i--)
        {
            value = value << 1 | (b >> i & 1);
            if (codes.TryGetValue((++bits, value), out int symbol))
            {
                text.Append((char)symbol);
                bits = value = 0;
            }
        }
    }

    return text.ToString();
}
