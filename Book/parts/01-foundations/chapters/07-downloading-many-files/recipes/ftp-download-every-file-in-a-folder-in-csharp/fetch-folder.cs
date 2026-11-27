// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Downloads every file of an FTP folder with Curl.Api: list it, skip the subfolders, fetch the rest, and
// compare each local copy with what Surl.Api serves.
// Run: dotnet run fetch-folder.cs [-- refused|folder|vanished|sub]
//   (no argument) the folder /pub/, three files, a subfolder and a dot file
//   refused       the same server without ListDirectories
//   folder        a subfolder asked for as if it were a file
//   vanished      a name that is listed no more
//   hidden        the dot file, asked for by name
//   sub           the subfolder, fetched by the same routine
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "pub";

// The server: anonymous FTP that lists folders, holding a folder of three files, a subfolder and a dot file.
var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0" },
    AllowAnonymous = true,
    ListDirectories = which != "refused",
};
options.Files["pub/a.txt"] = "alpha\n"u8.ToArray();
options.Files["pub/b.txt"] = "bravo\n"u8.ToArray();
options.Files["pub/c.txt"] = "charlie\n"u8.ToArray();
options.Files["pub/.hidden"] = "not served\n"u8.ToArray();
options.Files["pub/archive/old.txt"] = "from last year\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

switch (which)
{
    case "pub":
        return await Download("pub");
    case "sub":
        return await Download("pub/archive");
    case "refused":
        try
        {
            await List("pub");
        }
        catch (CurlException failure)
        {
            Console.WriteLine($"listing: {(int)failure.Code} {failure.Code}");
        }

        return 0;
    case "folder":
        try
        {
            await Fetch("pub/archive");
        }
        catch (CurlException failure)
        {
            Console.WriteLine($"archive as a file: {(int)failure.Code} {failure.Code}");
        }

        return 0;
    default:
        string name = which == "hidden" ? ".hidden" : "gone.txt";
        try
        {
            await Fetch($"pub/{name}");
        }
        catch (CurlException failure)
        {
            Console.WriteLine($"{name}: {(int)failure.Code} {failure.Code}");
        }

        return 0;
}

// The long listing of a folder: GET on its URL, ending in a slash. Curl.Api has no names-only switch,
// so each line is read as the first character for the type and the last word for the name.
async Task<List<(bool IsFolder, string Name)>> List(string folder)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{folder}/"), CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();
    return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.TrimEnd('\r'))
        .Select(line => (line[0] == 'd', line[(line.LastIndexOf(' ') + 1)..]))
        .ToList();
}

async Task<byte[]> Fetch(string path)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
    await using Stream body = response.Body;
    using var copy = new MemoryStream();
    await body.CopyToAsync(copy);
    return copy.ToArray();
}

// List the folder, skip the subfolders, fetch each file into out/, and compare it with the server's bytes.
async Task<int> Download(string folder)
{
    List<(bool IsFolder, string Name)> entries = await List(folder);
    Console.WriteLine($"listing: {string.Join(" ", entries.Select(e => e.Name + (e.IsFolder ? "/" : "")))}");
    string target = Path.Combine("out", folder);
    Directory.CreateDirectory(target);
    bool allSame = true;
    foreach ((bool isFolder, string name) in entries)
    {
        if (isFolder)
        {
            Console.WriteLine($"skip {name}/");
            continue;
        }

        byte[] got = await Fetch($"{folder}/{name}");
        File.WriteAllBytes(Path.Combine(target, name), got);
        var served = await server.Files.ReadAsync($"/{folder}/{name}");
        bool same = got.AsSpan().SequenceEqual(served.Bytes.Span);
        allSame &= same;
        Console.WriteLine($"get {name}: {got.Length} bytes, {(same ? "identical to the server's" : "DIFFERENT from the server's")}");
    }

    Console.WriteLine($"in out/{folder}: {string.Join(" ", Directory.GetFiles(target).Select(Path.GetFileName).Order())}");
    return allSame ? 0 : 1;
}
