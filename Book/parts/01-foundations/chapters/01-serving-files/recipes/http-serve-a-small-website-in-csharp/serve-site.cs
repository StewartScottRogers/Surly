// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves a small site from memory with Surl.Api and checks each file's Content-Type with Curl.Api.
// Run: dotnet run serve-site.cs [-- path ...]   (default: every file of the site)
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Surl.Api;

// The site: each path with its bytes and the Content-Type a browser expects for it.
byte[] png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
var site = new (string Path, byte[] Bytes, string Type)[]
{
    ("index.html", Encoding.UTF8.GetBytes("<!doctype html><link rel=stylesheet href=css/site.css><img src=img/logo.png>\n"), "text/html"),
    ("css/site.css", Encoding.UTF8.GetBytes("body { margin: 0; }\n"), "text/css"),
    ("js/app.js", Encoding.UTF8.GetBytes("console.log('loaded');\n"), "text/javascript"),
    ("img/logo.png", png, "image/png"),
    ("img/icon.svg", Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'/>\n"), "image/svg+xml"),
    ("data/menu.json", Encoding.UTF8.GetBytes("{\"items\": []}\n"), "application/json"),
    ("downloads/notes.xyz", Encoding.UTF8.GetBytes("an extension surl does not know\n"), "application/octet-stream"),
};

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
foreach (var file in site)
{
    options.Files[file.Path] = file.Bytes;
}

await using var server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

// Fetch each path as a browser would and compare what came back with what was expected.
int wrong = 0;
foreach (string path in args.Length > 0 ? args : site.Select(f => f.Path))
{
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
    var body = new MemoryStream();
    await using (Stream stream = response.Body)
    {
        await stream.CopyToAsync(body);
    }

    // Headers is every header as received, in order; header names are case-insensitive.
    string type = response.Headers.FirstOrDefault(h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value ?? "(none)";
    var expected = site.FirstOrDefault(f => f.Path == path);
    bool ok = expected.Path is not null && response.StatusCode == 200 && type == expected.Type && body.ToArray().SequenceEqual(expected.Bytes);
    Console.WriteLine($"/{path} {response.StatusCode} {type} {body.Length} bytes {(ok ? "ok" : "WRONG")}");
    wrong += ok ? 0 : 1;
}

return wrong == 0 ? 0 : 1;
