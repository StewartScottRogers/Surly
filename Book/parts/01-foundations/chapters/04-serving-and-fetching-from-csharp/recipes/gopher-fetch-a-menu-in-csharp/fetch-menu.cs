// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves a small folder over Gopher with Surl.Api, fetches menus and files with Curl.Api, and parses each
// menu into its entries.
// Run: dotnet run fetch-menu.cs [-- off] <path>...
//   <path>   a Gopher URL path: "" for the root menu, 0/hello.txt for a file, 1/notes for a sub-menu
//   off      first argument: the server is started without ListDirectories
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using Curl.Api;
using Surl.Api;

bool listings = args.FirstOrDefault() != "off";
string[] paths = args.Where(a => a != "off").DefaultIfEmpty("").ToArray();

// The server, in-process, on a free port, with a file, a folder and a dot file.
var options = new SurlServerOptions { ListenUrls = { "gopher://127.0.0.1:0" }, ListDirectories = listings, RecordExchanges = true };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
options.Files["notes/today.txt"] = "nothing happened\n"u8.ToArray();
options.Files[".secret"] = "not for you\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);

await using CurlClient client = CurlClient.Create();
foreach (string path in paths)
{
    string reply = await Fetch(path);

    // What the server received: the selector alone, without the item type the URL carried.
    string selector = System.Text.Encoding.ASCII.GetString(server.Exchanges[^1].Received).Trim();
    Console.WriteLine($"== /{path} (server received '{selector}')");

    // A menu is lines of "<type><display>TAB<selector>TAB<host>TAB<port>", each ended by CR LF, and a
    // line holding a dot. Anything else is the bytes of a file.
    if (path is "" or ['1', '/', ..] || reply.StartsWith('3'))
    {
        foreach (string line in reply.Split("\r\n").TakeWhile(l => l != "."))
        {
            string[] field = line[1..].Split('\t');

            // The port in an entry is the one the server listens on, so it is shown by name.
            string port = field[3] == server.BoundUrl.Port.ToString() ? "<bound>" : field[3];
            Console.WriteLine($"type {line[0]}: {field[0]} -> '{field[1]}' on {field[2]}:{port}");
        }
    }
    else
    {
        Console.Write(reply);
    }
}

return 0;

async Task<string> Fetch(string path)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, server.BoundUrl + path), CancellationToken.None);
    await using Stream body = response.Body;
    return await new StreamReader(body).ReadToEndAsync();
}
