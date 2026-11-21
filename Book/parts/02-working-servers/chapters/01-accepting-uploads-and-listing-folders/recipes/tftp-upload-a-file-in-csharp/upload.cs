// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# file-based app: `dotnet run upload.cs -- SCENARIO`.
//   stored (the default), read-back, overwrite, several-blocks, several-blocks-read-back, no-subfolders,
//   no-dot-files, uploads-off, uploads-off-error-two, uploads-off-nothing-kept, over-the-limit, over-the-limit-kept-nothing
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string scenario = args.Length > 0 ? args[0] : "stored";

// One TFTP server in-process. Uploads are on, except for the uploads-off scenarios; the over-the-limit ones add a size limit.
var options = new SurlServerOptions
{
    ListenUrls = { "tftp://127.0.0.1:0" },
    AllowUploads = !scenario.StartsWith("uploads-off"),
    MaxFileSize = scenario.StartsWith("over-the-limit") ? 1000 : null,
    RecordExchanges = true,
};
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

// curl -T: a PUT with a body to a tftp:// URL is a TFTP write. The answer is the code that refused it, or "ok".
async Task<string> Put(string name, byte[] body)
{
    try
    {
        await client.SendAsync(new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}{name}") { Body = body }, CancellationToken.None);
        return "ok";
    }
    catch (CurlException e)
    {
        return $"{e.Code} ({(int)e.Code})";
    }
}

// A read: the file's bytes, or the code that refused it.
async Task<byte[]?> Get(string name)
{
    try
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{name}"), CancellationToken.None);
        await using Stream body = response.Body;
        using var buffer = new MemoryStream();
        await body.CopyToAsync(buffer);
        return buffer.ToArray();
    }
    catch (CurlException) { return null; }
}

// What the server holds under a name, read from its store.
async Task<string> Stored(string name)
{
    try { return Encoding.ASCII.GetString((await server.Files.ReadAsync("/" + name)).Bytes.Span); }
    catch { return "(nothing)"; }
}

byte[] config = "hostname r1\n"u8.ToArray();
byte[] big = Enumerable.Repeat((byte)'a', 1300).ToArray();

switch (scenario)
{
    case "stored":
        Console.WriteLine($"put: {await Put("router.cfg", config)}");
        Console.Write($"stored: {await Stored("router.cfg")}");
        break;
    case "read-back":
        await Put("router.cfg", config);
        Console.Write($"read: {Encoding.ASCII.GetString((await Get("router.cfg"))!)}");
        break;
    case "overwrite":
        await Put("router.cfg", config);
        await Put("router.cfg", "hostname r2\n"u8.ToArray());
        Console.Write($"stored: {await Stored("router.cfg")}");
        break;
    case "several-blocks":
        Console.WriteLine($"put: {await Put("big.bin", big)}");
        Console.WriteLine($"stored: {(await server.Files.ReadAsync("/big.bin")).Bytes.Length} bytes");
        break;
    case "several-blocks-read-back":
        await Put("big.bin", big);
        Console.WriteLine($"read: {(await Get("big.bin"))!.Length} bytes");
        break;
    case "no-subfolders":
        Console.WriteLine($"put: {await Put("configs/r1.cfg", config)}");
        break;
    case "no-dot-files":
        Console.WriteLine($"put: {await Put(".hidden", config)}");
        break;
    case "uploads-off":
        Console.WriteLine($"put: {await Put("router.cfg", config)}");
        break;
    case "uploads-off-error-two":
        await Put("router.cfg", config);
        // The datagrams the server sent: an error packet is opcode 5, then a two-byte error code.
        byte[] error = server.Exchanges.Single().Chunks
            .Where(chunk => chunk.Direction == SurlExchangeDirection.Sent).Select(chunk => chunk.Bytes.ToArray())
            .Single(datagram => datagram[1] == 5);
        Console.WriteLine($"server sent: opcode {error[1]}, error {error[2] * 256 + error[3]}");
        break;
    case "uploads-off-nothing-kept":
        await Put("router.cfg", config);
        Console.WriteLine($"read: {(await Get("router.cfg")) is null}");
        break;
    case "over-the-limit":
        Console.WriteLine($"put: {await Put("big.bin", big)}");
        break;
    case "over-the-limit-kept-nothing":
        await Put("big.bin", big);
        Console.WriteLine($"stored: {await Stored("big.bin")}");
        break;
}
return 0;
