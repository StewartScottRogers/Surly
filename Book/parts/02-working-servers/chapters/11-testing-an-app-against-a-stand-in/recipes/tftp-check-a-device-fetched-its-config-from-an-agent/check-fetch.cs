// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent serves a device's config over TFTP with surl-mcp, lets the device fetch it with curl-mcp, and confirms
// from get_exchanges that the device read the right file whole, and at what block size, over the official MCP SDK.
// Run: dotnet run check-fetch.cs -- <case>
//   solution        the device fetches r1.cfg asking for 1,024-byte blocks; the check
//   got-the-file    the same fetch; are the device's bytes the served ones?
//   default         the device asks for no block size of its own; the check
//   no-options      the device sends no options at all; the check
//   missing         the device asks for r9.cfg, which is not served; the check
//   device-missing  the same fetch, from the device's side
//   never           no device fetches r5.cfg; the check
#:package ModelContextProtocol.Core@*

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args[0];

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

// surl-mcp: the stand-in TFTP server on a free port, holding four configs of 27 bytes in memory.
var configs = new[] { "r1", "r2", "r3", "r5" }.ToDictionary(d => $"{d}.cfg", d => $"hostname {d}\ninterface eth0\n");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "tftp://127.0.0.1:0" },
    ["files"] = configs,
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string tftp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "solution":
        await Fetch("r1.cfg", "--tftp-blksize", "1024");
        return await Check("r1.cfg");
    case "got-the-file":
        await Fetch("r1.cfg", "--tftp-blksize", "1024");
        Console.WriteLine(File.ReadAllText("got.cfg") == configs["r1.cfg"] ? "identical" : "different");
        return 0;
    case "default":
        await Fetch("r2.cfg");
        return await Check("r2.cfg");
    case "no-options":
        await Fetch("r3.cfg", "--tftp-no-options");
        return await Check("r3.cfg");
    case "missing":
        await Fetch("r9.cfg");
        return await Check("r9.cfg");
    case "device-missing":
        JsonElement ran = await Fetch("r9.cfg");
        Console.WriteLine($"device: exit code {ran.GetProperty("exitCode")}, {ran.GetProperty("standardError").GetProperty("content").GetString()!.Trim()}");
        return 0;
    default:
        return await Check("r5.cfg");
}

// The device: curl-mcp's curl tool reads one file over TFTP into got.cfg, as the device's boot loader would.
async Task<JsonElement> Fetch(string name, params string[] options)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "-m", "10", "-o", "got.cfg" }.Concat(options).Append(tftp + name).ToArray(),
    });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}

// The check: waits, ten seconds at most in all, for each exchange to end, and reads the packets of each with
// get_exchanges until one is a read request for the file.
async Task<int> Check(string name)
{
    long after = 0;
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); waited.Elapsed < TimeSpan.FromSeconds(10);)
    {
        CallToolResult result = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
        {
            ["server_id"] = serverId,
            ["kinds"] = new[] { "exchange_ended" },
            ["scheme"] = "tftp",
            ["after_sequence"] = after,
            ["timeout_ms"] = Math.Max(1, 10000 - (int)waited.ElapsedMilliseconds),
        });
        JsonElement answer = result.StructuredContent!.Value;
        if (answer.TryGetProperty("timed_out", out JsonElement timedOut) && timedOut.GetBoolean())
        {
            break;
        }

        JsonElement ended = answer.GetProperty("event");
        after = ended.GetProperty("sequence").GetInt64();
        long id = ended.GetProperty("exchange_id").GetInt64();

        // Every TFTP exchange is binary, so each chunk is a base64 datagram: a two-byte opcode, then its fields.
        CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?>
        {
            ["server_id"] = serverId, ["after_exchange_id"] = id - 1, ["max_exchanges"] = 1,
        });
        var packets = recorded.StructuredContent!.Value.GetProperty("exchanges")[0].GetProperty("chunks").EnumerateArray()
            .Select(c => (Sent: c.GetProperty("direction").GetString() == "sent", Bytes: Convert.FromBase64String(c.GetProperty("data").GetString()!)))
            .ToList();
        byte[]? request = packets.Where(p => !p.Sent).Select(p => p.Bytes).FirstOrDefault(b => b[1] == 1);
        if (request is null || Encoding.ASCII.GetString(request, 2, Array.IndexOf(request, (byte)0, 2) - 2) != name)
        {
            continue;
        }

        var sent = packets.Where(p => p.Sent).Select(p => p.Bytes).ToList();
        if (sent.FirstOrDefault(b => b[1] == 5) is byte[] error)
        {
            Console.WriteLine($"{name}: asked for, not on the server (error {error[2] * 256 + error[3]})");
            return 1;
        }

        var data = sent.Where(b => b[1] == 3).ToList();
        int lastBlock = data[^1][2] * 256 + data[^1][3];
        bool acknowledged = packets.Any(p => !p.Sent && p.Bytes[1] == 4 && p.Bytes[2] * 256 + p.Bytes[3] == lastBlock);
        Console.WriteLine($"{name}: read, {data.Sum(b => b.Length - 4)} bytes in {data.Count} data block{(data.Count == 1 ? "" : "s")}{(acknowledged ? ", all acknowledged" : ", not all acknowledged")}");
        byte[]? oack = sent.FirstOrDefault(b => b[1] == 6);
        string[] fields = oack is null ? [] : Encoding.ASCII.GetString(oack, 2, oack.Length - 2).Split('\0');
        int at = Array.IndexOf(fields, "blksize");
        Console.WriteLine(at >= 0 ? $"agreed: blksize {fields[at + 1]}" : "agreed: no options, 512 by default");
        return acknowledged ? 0 : 1;
    }

    Console.WriteLine($"{name}: never read");
    return 1;
}
