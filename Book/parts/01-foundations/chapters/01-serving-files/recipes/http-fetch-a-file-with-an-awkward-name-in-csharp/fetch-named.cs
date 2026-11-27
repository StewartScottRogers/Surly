// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Fetches files with awkward names from an in-process server, building each URL one of three ways.
// Run: dotnet run fetch-named.cs -- <how> [name]
//   how    escape (the default): Uri.EscapeDataString on the name; raw: the name as typed;
//          uri: new Uri(baseUri, name)
//   name   one file name to ask for; with none, all three awkward names
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string how = args.Length > 0 ? args[0] : "escape";
string[] names = args.Length > 1 ? [args[1]] : ["my file.txt", "café.txt", "a#b.txt"];

// The server holds the real names, and a file called a, to see where a raw # leads.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["my file.txt"] = "spaced\n"u8.ToArray();
options.Files["café.txt"] = "accented\n"u8.ToArray();
options.Files["a#b.txt"] = "hashed\n"u8.ToArray();
options.Files["a"] = "just a\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

int failures = 0;
foreach (string name in names)
{
    string url = how switch
    {
        "raw" => server.BoundUrl + name,
        "uri" => new Uri(server.BoundUrl, name).AbsoluteUri,
        _ => server.BoundUrl + Uri.EscapeDataString(name),
    };
    string path = url[server.BoundUrl.ToString().Length..];
    try
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url), CancellationToken.None);
        await using Stream body = response.Body;
        string text = (await new StreamReader(body).ReadToEndAsync()).Trim();
        Console.WriteLine($"{path} -> {(int)response.StatusCode} {text}".TrimEnd());
        if ((int)response.StatusCode != 200 || text.Length == 0) { failures++; }
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{path} -> CurlException {(int)failure.Code} {failure.Code}");
        failures++;
    }
}

// Exit 0 when every name was served, 1 otherwise: the program's choice, as for curl -f.
return failures == 0 ? 0 : 1;
