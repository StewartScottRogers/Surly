// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves a firmware image over TFTP and HTTP from one in-memory store, fetches it both ways with
// Curl.Api and compares the bytes.
// Run: dotnet run firmware-over-http.cs [-- case]
//   same      fetch a 1300-byte image over both and compare (the default)
//   exact     the same with an image of exactly 1024 bytes, two full blocks
//   blksize   fetch over TFTP with a block size of 1024
//   missing   ask both for an image that is not there
//   write     send a file over TFTP and over HTTP
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Security.Cryptography;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "same";

// One store, two listeners: the same bytes are served at the same path over both protocols.
byte[] image = RandomNumberGenerator.GetBytes(which == "exact" ? 1024 : 1300);
var options = new SurlServerOptions { ListenUrls = { "tftp://127.0.0.1:0", "http://127.0.0.1:0" } };
options.Files["firmware.bin"] = image;
await using var server = await SurlServer.StartAsync(options);
string tftp = server.BoundUrls[0].ToString();
string http = server.BoundUrls[1].ToString();

await using CurlClient client = CurlClient.Create();

// The whole body, as bytes. TFTP has no status line, so StatusCode is 0 there and the bytes are the answer.
async Task<byte[]> Fetch(CurlRequest request)
{
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    using var bytes = new MemoryStream();
    await body.CopyToAsync(bytes);
    return bytes.ToArray();
}

switch (which)
{
    case "missing":
        try
        {
            await Fetch(new CurlRequest(HttpMethod.Get, tftp + "nope.bin"));
        }
        catch (CurlException e)
        {
            Console.WriteLine($"tftp: {e.Code} ({(int)e.Code})");
        }

        CurlResponse missing = await client.SendAsync(new CurlRequest(HttpMethod.Get, http + "nope.bin"), CancellationToken.None);
        await missing.Body.DisposeAsync();
        Console.WriteLine($"http: {missing.StatusCode} {missing.ReasonPhrase}");
        return 0;

    case "write":
        // Uploads are off unless surl is started with --allow-uploads. A body to a tftp:// URL is a TFTP write,
        // which the server refuses; an HTTP client with no login is told so.
        try
        {
            await Fetch(new CurlRequest(HttpMethod.Put, tftp + "up.bin") { Body = new byte[] { 1, 2, 3 } });
        }
        catch (CurlException e)
        {
            Console.WriteLine($"tftp: {e.Code} ({(int)e.Code})");
        }

        CurlResponse put = await client.SendAsync(new CurlRequest(HttpMethod.Put, http + "up.bin") { Body = new byte[] { 1, 2, 3 } }, CancellationToken.None);
        await put.Body.DisposeAsync();
        Console.WriteLine($"http: {put.StatusCode} {put.ReasonPhrase}");
        return 0;
}

CurlRequest overTftp = new(HttpMethod.Get, tftp + "firmware.bin");
if (which == "blksize")
{
    overTftp = new CurlRequest(HttpMethod.Get, tftp + "firmware.bin") { Tftp = new CurlTftpOptions { BlockSize = 1024 } };
}

byte[] viaTftp = await Fetch(overTftp);
byte[] viaHttp = await Fetch(new CurlRequest(HttpMethod.Get, http + "firmware.bin"));
Console.WriteLine($"tftp: {viaTftp.Length} bytes");
Console.WriteLine($"http: {viaHttp.Length} bytes");
Console.WriteLine(viaTftp.AsSpan().SequenceEqual(viaHttp) && viaTftp.AsSpan().SequenceEqual(image) ? "identical" : "different");
return 0;
