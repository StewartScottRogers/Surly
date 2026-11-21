// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// One Surl.Api server with six listen URLs over one store; Curl.Api fetches the same file from each
// and compares the bytes with the ones served.
// Run: dotnet run program.cs [-- mode]
//   (none)        fetch hello.txt six ways
//   no-anonymous  the same server without AllowAnonymous
//   replaced      replace hello.txt in the store after the start, then fetch it six ways
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "";
byte[] served = "hello from surl\n"u8.ToArray();

// One store, six listeners, each on a free port. AllowAnonymous lets curl's anonymous FTP login in.
var options = new SurlServerOptions
{
    ListenUrls =
    {
        "http://127.0.0.1:0", "ftp://127.0.0.1:0", "tftp://127.0.0.1:0",
        "gopher://127.0.0.1:0", "dict://127.0.0.1:0", "ws://127.0.0.1:0",
    },
    AllowAnonymous = mode != "no-anonymous",
};
options.Files["hello.txt"] = served;
await using var server = await SurlServer.StartAsync(options);
if (mode == "replaced")
{
    // The one store all six listeners read, changed while they serve.
    served = "replaced while running\n"u8.ToArray();
    await server.Files.PutAsync("/hello.txt", served, CancellationToken.None);
}

// DICT sends the definition with its lines ended in CR LF, then a line holding a dot.
string definition = "\r\n" + Encoding.UTF8.GetString(served).Replace("\n", "\r\n") + ".\r\n";

// Where the file is on each: Gopher puts item type 0 before the path; DICT looks up the headword.
string[] paths = ["hello.txt", "hello.txt", "hello.txt", "0/hello.txt", "d:hello.txt", "hello.txt"];

await using CurlClient client = CurlClient.Create();
for (int i = 0; i < paths.Length; i++)
{
    // Joined as text: as a relative Uri, d:hello.txt would read as a Windows drive.
    string url = $"{server.BoundUrls[i]}{paths[i]}";
    string scheme = server.BoundUrls[i].Scheme;
    try
    {
        byte[] got = await Fetch(url);
        string verdict = got.AsSpan().SequenceEqual(served) ? "the served bytes"
            : Encoding.UTF8.GetString(got).Contains(definition) ? "a DICT reply holding them"
            : "something else";
        Console.WriteLine($"{scheme}: {got.Length} bytes, {verdict}");
    }
    catch (CurlException e)
    {
        Console.WriteLine($"{scheme}: CurlException {e.Code} ({(int)e.Code})");
    }
}

return 0;

// A WebSocket file arrives as one message; the other five are one SendAsync each.
async Task<byte[]> Fetch(string url)
{
    if (url.StartsWith("ws:"))
    {
        await using CurlWebSocket socket = await client.ConnectWebSocketAsync(url, CancellationToken.None);
        CurlWebSocketMessage message = await socket.ReceiveAsync(CancellationToken.None);
        return message.Data.ToArray();
    }

    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url), CancellationToken.None);
    await using Stream body = response.Body;
    var copy = new MemoryStream();
    await body.CopyToAsync(copy);
    return copy.ToArray();
}
