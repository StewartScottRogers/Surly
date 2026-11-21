// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Downloads files from an in-process server into a folder tree the program builds.
// Run: dotnet run download.cs [-- folder [--no-create] [path ...]]
//   folder        where the files land (default: downloads)
//   --no-create   skip creating the folders first
//   path ...      what to fetch (default: docs/guide.txt reports/2026/summary.txt)
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using Curl.Api;
using Surl.Api;

string folder = args.Length > 0 ? args[0] : "downloads";
bool create = !args.Contains("--no-create");
string[] paths = args.Skip(1).Where(a => a != "--no-create").ToArray();
if (paths.Length == 0)
{
    paths = ["docs/guide.txt", "reports/2026/summary.txt"];
}

// The server: two files in memory, on a free port.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["docs/guide.txt"] = "the guide\n"u8.ToArray();
options.Files["reports/2026/summary.txt"] = "the summary\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);

int exit = 0;
await using CurlClient client = CurlClient.Create();
foreach (string path in paths)
{
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
    await using Stream body = response.Body;

    // Only a 200 is a download; anything else leaves no file behind.
    if (response.StatusCode != 200)
    {
        Console.WriteLine($"{path}: {response.StatusCode} {response.ReasonPhrase}, not saved; {folder}/{path} exists: {File.Exists(Path.Combine(folder, path))}");
        exit = 22;
        continue;
    }

    // The URL's path, under the chosen folder, with the folders made first (curl --create-dirs).
    string target = Path.Combine(folder, path);
    if (create)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    }

    try
    {
        await using FileStream file = File.Create(target);
        await body.CopyToAsync(file);
        Console.WriteLine($"{path}: saved {file.Length} bytes to {folder}/{path}");
    }
    catch (DirectoryNotFoundException)
    {
        Console.WriteLine($"{path}: no folder for {folder}/{path}");
        exit = 23;
    }
}

return exit;
