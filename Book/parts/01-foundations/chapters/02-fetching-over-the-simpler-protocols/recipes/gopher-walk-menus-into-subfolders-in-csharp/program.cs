// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# file-based app: `dotnet run program.cs -- SCENARIO`. Package versions are the book's pins in
// Book/build/packages.yml; build/pin.py rewrites these lines, and lint checks they match.
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using Curl.Api;
using Surl.Api;

// Two servers in this process, on free ports: one lists directories, one does not.
// Files holds the same tree for both: a root file, a notes folder and a folder inside it.
SurlServerOptions Make(bool list)
{
    var options = new SurlServerOptions { ListenUrls = { "gopher://127.0.0.1:0" }, ListDirectories = list };
    options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
    options.Files["notes/today.txt"] = "nothing happened\n"u8.ToArray();
    options.Files["notes/archive/2025.txt"] = "old news\n"u8.ToArray();
    return options;
}
await using var listing = await SurlServer.StartAsync(Make(true));
await using var bare = await SurlServer.StartAsync(Make(false));
await using CurlClient client = CurlClient.Create();

// One selector in, one answer out. In a Gopher URL the first character after the slash is the
// item type; it is not sent, and the rest is the selector.
async Task<string> Get(SurlServer server, string path)
{
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
    await using Stream body = response.Body;
    return await new StreamReader(body).ReadToEndAsync();
}

// A menu is lines of type+name, selector, host, port, tab-separated, ended by a line holding a dot.
// Print them without the port, which changes on every run.
void Show(string menu)
{
    foreach (string line in menu.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        Console.WriteLine(line.Replace($"\t{listing.BoundUrl.Port}", "\tPORT").Replace($"\t{bare.BoundUrl.Port}", "\tPORT"));
}

switch (args[0])
{
    case "walk":
        // From the root menu, follow the first directory entry of each menu, then fetch a file.
        string selector = "", menu;
        while (true)
        {
            menu = await Get(listing, $"1{selector}");
            string? next = menu.Split("\r\n").Where(l => l.StartsWith('1')).Select(l => l.Split('\t')[1]).FirstOrDefault();
            if (next is null) break;
            Console.WriteLine($"menu {(selector == "" ? "/" : selector)} -> {next}");
            selector = next;
        }
        string file = menu.Split("\r\n").Where(l => l.StartsWith('0')).Select(l => l.Split('\t')[1]).First();
        Console.WriteLine($"file {file}");
        Console.Write(await Get(listing, $"0{file}"));
        break;
    case "folder-menu": Show(await Get(listing, "1/notes")); break;
    case "deepest-menu": Show(await Get(listing, "1/notes/archive")); break;
    case "file-by-selector": Console.Write(await Get(listing, "0/notes/archive/2025.txt")); break;
    case "trailing-slash": Console.WriteLine(await Get(listing, "1/notes/") == await Get(listing, "1/notes")); break;
    case "type-is-not-checked":
        Console.Write(await Get(listing, "1/hello.txt"));
        Show(await Get(listing, "0/notes/archive"));
        break;
    case "missing-folder": Show(await Get(listing, "1/notes/nope")); break;
    case "missing-file": Show(await Get(listing, "0/notes/archive/2026.txt")); break;
    case "listing-off-no-walk":
        Show(await Get(bare, "1"));
        Show(await Get(bare, "1/notes"));
        break;
    case "listing-off-file-served": Console.Write(await Get(bare, "0/hello.txt")); break;
}
return 0;
