// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves a folder on disk with Surl.Api and fetches from it with Curl.Api, in one process.
// Run: dotnet run serve-folder.cs -- <case>
//   fetch        serve site/ and fetch hello.txt (the default)
//   state        show the .surl folder the server keeps in site/, and try to fetch from it
//   new-file     write a file into site/ while the server runs, then fetch it
//   second       start a second server on the same folder
//   both         give DataDirectory and Files together
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using Curl.Api;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "fetch";

// The server: the folder site/ on disk, on a free port of the loopback address.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, DataDirectory = "site" };
if (which == "both")
{
    options.Files["hello.txt"] = "hello from memory\n"u8.ToArray();
    try
    {
        await SurlServer.StartAsync(options);
    }
    catch (ArgumentException misused)
    {
        Console.WriteLine($"ArgumentException: {misused.Message}");
        return 2;
    }
}

await using var server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

switch (which)
{
    case "state":
        // The server's own state, kept in the folder it serves, and never served.
        Console.WriteLine($"site/.surl/lock exists: {File.Exists(Path.Combine("site", ".surl", "lock"))}");
        await Fetch(".surl/lock");
        await Fetch(".surl/");
        break;

    case "new-file":
        await File.WriteAllTextAsync(Path.Combine("site", "later.txt"), "written while serving\n");
        await Fetch("later.txt");
        break;

    case "second":
        try
        {
            await using var second = await SurlServer.StartAsync(
                new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, DataDirectory = "site" });
        }
        catch (SurlServerStartException refused)
        {
            Console.WriteLine($"second server refused: {(int)refused.ExitCode} ({refused.ExitCode})");
            return (int)refused.ExitCode;
        }

        break;

    default:
        await Fetch("hello.txt");
        break;
}

return 0;

async Task Fetch(string path)
{
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();
    Console.WriteLine($"/{path}: {response.StatusCode} {response.ReasonPhrase}");
    if (response.StatusCode == 200)
    {
        Console.Write(text);
    }
}
