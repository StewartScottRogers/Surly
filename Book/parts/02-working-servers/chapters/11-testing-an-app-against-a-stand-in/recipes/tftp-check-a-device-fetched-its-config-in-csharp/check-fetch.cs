// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves a device's config over TFTP from an in-process Surl.Api server, lets the device fetch it, and checks with
// FindExchanges that the server recorded the read, whole, and the block size it agreed.
// Run: dotnet run check-fetch.cs -- <case>
//   solution        the device fetches r1.cfg asking for 1,024-byte blocks; the check
//   got-the-file    the same fetch; are the device's bytes the served ones?
//   default         the device asks for no block size of its own; the check
//   no-options      the device sends no options at all; the check
//   missing         the device asks for r9.cfg, which is not served; the check
//   device-missing  the same fetch, from the device's side
//   never           no device fetches r5.cfg; the check
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Buffers.Binary;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args[0];

// The stand-in TFTP server, in memory, with four configs of 27 bytes. RecordExchanges keeps every packet.
var options = new SurlServerOptions { ListenUrls = { "tftp://127.0.0.1:0" }, RecordExchanges = true };
foreach (string device in new[] { "r1", "r2", "r3", "r5" })
{
    options.Files[$"{device}.cfg"] = Encoding.ASCII.GetBytes($"hostname {device}\ninterface eth0\n");
}

await using SurlServer server = await SurlServer.StartAsync(options);
string tftp = server.BoundUrl.ToString();

switch (which)
{
    case "solution":
        await Fetch("r1.cfg", new CurlTftpOptions { BlockSize = 1024 });
        return await Check("r1.cfg");
    case "got-the-file":
        byte[] got = await Fetch("r1.cfg", new CurlTftpOptions { BlockSize = 1024 });
        Console.WriteLine(got.AsSpan().SequenceEqual(options.Files["r1.cfg"]) ? "identical" : "different");
        return 0;
    case "default":
        await Fetch("r2.cfg", null);
        return await Check("r2.cfg");
    case "no-options":
        await Fetch("r3.cfg", new CurlTftpOptions { NoOptions = true });
        return await Check("r3.cfg");
    case "missing":
        try
        {
            await Fetch("r9.cfg", null);
        }
        catch (CurlException)
        {
        }

        return await Check("r9.cfg");
    case "device-missing":
        try
        {
            await Fetch("r9.cfg", null);
            return 0;
        }
        catch (CurlException failure)
        {
            Console.WriteLine($"device: curl code {(int)failure.Code} {failure.Code}");
            return (int)failure.Code;
        }

    default:
        return await Check("r5.cfg");
}

// The device: reads one file over TFTP, as its boot loader would, at the block size it asks for.
async Task<byte[]> Fetch(string name, CurlTftpOptions? tftpOptions)
{
    await using CurlClient client = CurlClient.Create();
    using var ceiling = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, tftp + name) { Tftp = tftpOptions }, ceiling.Token);
    await using Stream body = response.Body;
    using var copy = new MemoryStream();
    await body.CopyToAsync(copy, ceiling.Token);
    return copy.ToArray();
}

// The check: waits, at most ten seconds, for a finished TFTP exchange whose read request names the file, then reads
// the packets the server recorded for it.
async Task<int> Check(string name)
{
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); waited.Elapsed < TimeSpan.FromSeconds(10); await Task.Delay(200))
    {
        var ended = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, Scheme = "tftp" })
            .Select(e => e.ExchangeId).ToHashSet();
        SurlExchangeRecord? read = server.FindExchanges(new SurlEventQuery { Scheme = "tftp" })
            .LastOrDefault(x => ended.Contains(x.ExchangeId) && RequestedName(x) == name);
        if (read is null)
        {
            continue;
        }

        // Each chunk is one datagram: a two-byte opcode, then its fields (RFC 1350, RFC 2347).
        var sent = read.Chunks.Where(c => c.Direction == SurlExchangeDirection.Sent).Select(c => c.Bytes.ToArray()).ToList();
        var acked = read.Chunks.Where(c => c.Direction == SurlExchangeDirection.Received).Select(c => c.Bytes.ToArray())
            .Where(p => Opcode(p) == 4).Select(p => BinaryPrimitives.ReadUInt16BigEndian(p.AsSpan(2))).ToList();
        if (sent.Any(p => Opcode(p) == 5))
        {
            Console.WriteLine($"{name}: asked for, not on the server (error {BinaryPrimitives.ReadUInt16BigEndian(sent.First(p => Opcode(p) == 5).AsSpan(2))})");
            return 1;
        }

        var data = sent.Where(p => Opcode(p) == 3).ToList();
        int lastBlock = data.Count == 0 ? -1 : BinaryPrimitives.ReadUInt16BigEndian(data[^1].AsSpan(2));
        if (!acked.Contains((ushort)lastBlock))
        {
            continue;
        }

        Console.WriteLine($"{name}: read, {data.Sum(p => p.Length - 4)} bytes in {data.Count} data block{(data.Count == 1 ? "" : "s")}, all acknowledged");
        byte[]? oack = sent.FirstOrDefault(p => Opcode(p) == 6);
        string[] fields = oack is null ? [] : Encoding.ASCII.GetString(oack, 2, oack.Length - 2).Split('\0');
        int at = Array.IndexOf(fields, "blksize");
        Console.WriteLine(at >= 0 ? $"agreed: blksize {fields[at + 1]}" : "agreed: no options, 512 by default");
        return 0;
    }

    Console.WriteLine($"{name}: never read");
    return 1;
}

static int Opcode(byte[] packet) => packet.Length >= 2 ? BinaryPrimitives.ReadUInt16BigEndian(packet) : -1;

// A read request is opcode 1, then the file name and the mode, each ending in a zero byte.
static string? RequestedName(SurlExchangeRecord exchange)
{
    byte[]? request = exchange.Chunks.Where(c => c.Direction == SurlExchangeDirection.Received)
        .Select(c => c.Bytes.ToArray()).FirstOrDefault(p => Opcode(p) == 1);
    return request is null ? null : Encoding.ASCII.GetString(request, 2, Array.IndexOf(request, (byte)0, 2) - 2);
}
