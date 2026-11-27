// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A fixture calls ResetAsync between tests. When the server serves a real folder (DataDirectory), the
// reset is refused with InvalidOperationException and the folder is left as it is; served from memory
// (Files), the same call puts the files back as they were at start.
// Run: dotnet run reset-guard.cs [-- case]
//   (no argument)  serve the data folder, upload a file, reset
//   in-memory      serve the same file from memory, upload a file, reset
//   snapshot       serve the data folder and take a snapshot
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "directory";

// The server: anonymous uploads allowed, its files either the data folder on disk or the same file in memory.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, AllowUploads = true, AllowAnonymous = true };
if (which == "in-memory")
{
    options.Files["report.txt"] = "quarterly figures\n"u8.ToArray();
}
else
{
    options.DataDirectory = Path.GetFullPath("data");
}

await using var server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

if (which == "snapshot")
{
    try
    {
        await server.SnapshotAsync();
        Console.WriteLine("snapshot taken");
        return 0;
    }
    catch (InvalidOperationException refused)
    {
        Console.WriteLine($"SnapshotAsync refused: {refused.Message}");
        return 1;
    }
}

// A test uploads a draft.
CurlResponse put = await client.SendAsync(new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}draft.txt")
{
    Body = Encoding.ASCII.GetBytes("draft\n"),
}, CancellationToken.None);
await put.Body.DisposeAsync();
Console.WriteLine($"upload: {put.StatusCode}");

// The fixture's reset between tests.
int exit = 0;
try
{
    await server.ResetAsync();
    Console.WriteLine("ResetAsync: done");
}
catch (InvalidOperationException refused)
{
    Console.WriteLine($"ResetAsync refused: {refused.Message}");
    exit = 1;
}

// What the next test would find, by HTTP, and on disk for the folder.
Console.WriteLine($"GET report.txt: {await Status("report.txt")}, GET draft.txt: {await Status("draft.txt")}");
if (which != "in-memory")
{
    Console.WriteLine($"data folder: {string.Join(" ", Directory.GetFiles("data").Select(Path.GetFileName).Order())}");
}

return exit;

async Task<int> Status(string path)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
    await response.Body.DisposeAsync();
    return response.StatusCode;
}
