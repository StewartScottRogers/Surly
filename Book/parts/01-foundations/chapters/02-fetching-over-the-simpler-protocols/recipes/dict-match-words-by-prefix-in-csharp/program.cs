// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves a three-word glossary over DICT with Surl.Api, lists the words that match with Curl.Api, and
// defines the first. Run: dotnet run program.cs -- m:ap:*:prefix
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text.RegularExpressions;
using Curl.Api;
using Surl.Api;

string question = args[0];

// The server, in-process, on a free port. Each file is a headword: its name the word, its bytes the definition.
var options = new SurlServerOptions { ListenUrls = { "dict://127.0.0.1:0" } };
options.Files["apple"] = "a round fruit\n"u8.ToArray();
options.Files["apricot"] = "a small orange fruit\n"u8.ToArray();
options.Files["banana"] = "a long yellow fruit\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);

// The client. A DICT URL is an ordinary GET: the path says what to ask, and the body is the whole reply.
await using CurlClient client = CurlClient.Create();

// The question first: the list of matches, or the server's refusal.
string reply = await Ask(question);
Console.Write(reply);

// Then the pick: the first word of the list, if there is one, is defined.
Match first = Regex.Match(reply, "^surl \"(.+)\"$", RegexOptions.Multiline);
if (first.Success)
{
    Console.WriteLine($"picked: {first.Groups[1].Value}");
    Console.Write(await Ask($"d:{first.Groups[1].Value}"));
}
else
{
    Console.WriteLine("nothing to pick");
}
return 0;

// DICT ends its lines with CR LF; they are printed as plain new lines.
async Task<string> Ask(string path)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, server.BoundUrl + path), CancellationToken.None);
    await using Stream body = response.Body;
    return (await new StreamReader(body).ReadToEndAsync()).Replace("\r\n", "\n");
}
