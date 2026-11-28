// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves a two-word glossary over DICT with Surl.Api and asks it questions with Curl.Api. Each argument is
// the path of one DICT URL, and each reply is printed whole.
// Run: dotnet run look-up.cs -- d:surly m:surl show:db
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text.RegularExpressions;
using Curl.Api;
using Surl.Api;

// The server, in-process, on a free port. Each file is a headword: its name the word, its bytes the definition.
var options = new SurlServerOptions { ListenUrls = { "dict://127.0.0.1:0" } };
options.Files["surly"] = "bad-tempered and unfriendly\n"u8.ToArray();
options.Files["surl"] = "the server that answers curl\n"u8.ToArray();
options.Files[".secret"] = "not for you\n"u8.ToArray();
options.Files["more/word"] = "inside a folder\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);

// The client. A DICT URL is an ordinary GET: the path says what to ask, and the body is the whole reply.
await using CurlClient client = CurlClient.Create();
foreach (string path in args)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, server.BoundUrl + path), CancellationToken.None);
    await using Stream body = response.Body;
    string reply = await new StreamReader(body).ReadToEndAsync();

    // DICT ends its lines with CR LF, printed here as plain new lines, and the greeting
    // counts up per connection, so its number is blanked.
    Console.WriteLine($"== {path} (status {response.StatusCode})");
    Console.Write(Regex.Replace(reply.Replace("\r\n", "\n"), @"<\d+@surl>", "<n@surl>"));
}

return 0;
