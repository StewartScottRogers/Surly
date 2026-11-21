// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Fetches a 3,000-byte file from an in-process TFTP server with different block sizes, and reads from
// the server's recorded datagrams what the client asked for and what the server agreed to.
// Run: dotnet run block-size.cs [-- default|max|range|none|missing]
//   (no argument) block size 1024
//   default       no CurlTftpOptions at all
//   max           the largest block size, 65464
//   range         block sizes of 70000, 4 and -1, out of range three ways
//   none          NoOptions: no options in the request
//   missing       a file the server does not have
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Security.Cryptography;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "1024";

if (which == "range")
{
    // As curl --tftp-blksize does: too large is clamped, too small falls back to 512, negative throws.
    Console.WriteLine($"70000 -> {new CurlTftpOptions { BlockSize = 70000 }.BlockSize}");
    Console.WriteLine($"4 -> {new CurlTftpOptions { BlockSize = 4 }.BlockSize}");
    try
    {
        _ = new CurlTftpOptions { BlockSize = -1 };
    }
    catch (ArgumentOutOfRangeException)
    {
        Console.WriteLine("-1 -> ArgumentOutOfRangeException");
    }

    return 0;
}

byte[] image = RandomNumberGenerator.GetBytes(3000);
var options = new SurlServerOptions { ListenUrls = { "tftp://127.0.0.1:0" }, RecordExchanges = true };
options.Files["big.bin"] = image;
await using SurlServer server = await SurlServer.StartAsync(options);

CurlTftpOptions? tftp = which switch
{
    "1024" => new CurlTftpOptions { BlockSize = 1024 },
    "max" => new CurlTftpOptions { BlockSize = 65464 },
    "none" => new CurlTftpOptions { NoOptions = true },
    _ => null,
};

await using CurlClient client = CurlClient.Create();
try
{
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{(which == "missing" ? "nope.bin" : "big.bin")}") { Tftp = tftp },
        CancellationToken.None);
    await using Stream body = response.Body;
    using var bytes = new MemoryStream();
    await body.CopyToAsync(bytes);
    Console.WriteLine(bytes.ToArray().AsSpan().SequenceEqual(image) ? $"{bytes.Length} bytes, identical" : "different");
}
catch (CurlException failure)
{
    Console.WriteLine($"{failure.Code} ({(int)failure.Code})");
}

// The datagrams of the one exchange: each chunk is one datagram, and its first two bytes are the opcode.
SurlExchangeRecord exchange = server.Exchanges.Single();
var datagrams = exchange.Chunks
    .Select(chunk => (From: chunk.Direction, Bytes: chunk.Bytes.ToArray()))
    .ToList();
int opcode(byte[] datagram) => datagram[1];

var request = datagrams[0].Bytes;
Console.WriteLine($"request: opcode {opcode(request)}, {Describe(request)}");
var reply = datagrams[1].Bytes;
Console.WriteLine($"reply: opcode {opcode(reply)}, {Describe(reply)}");
Console.WriteLine($"data blocks: {datagrams.Count(d => d.From == SurlExchangeDirection.Sent && opcode(d.Bytes) == 3)}");
return 0;

// A request's file name, mode and options, or an option acknowledgement's options, or a data block's number.
static string Describe(byte[] datagram) => datagram[1] switch
{
    3 => $"block {datagram[2] * 256 + datagram[3]}",
    5 => $"error {datagram[2] * 256 + datagram[3]}",
    _ => string.Join(" ", Encoding.ASCII.GetString(datagram[2..]).Split('\0', StringSplitOptions.RemoveEmptyEntries)),
};
