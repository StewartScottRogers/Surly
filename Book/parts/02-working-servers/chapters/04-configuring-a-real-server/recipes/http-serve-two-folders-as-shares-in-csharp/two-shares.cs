// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves two real folders as shares, /docs readonly and /data writable, reads and uploads with Curl.Api, and starts
// a second server on the same folders. Run: dotnet run two-shares.cs -- MODE
//   serve            read a file from each share
//   readonly         upload into the readonly share, then look for the file
//   writable         upload into the other share, then read the folder on disk
//   second-readonly  a second server on the readonly folder, read from it
//   second-writable  a second server on the writable folder
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using Curl.Api;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "serve";

// The folders behind the shares.
string root = Path.Combine(Path.GetTempPath(), "two-shares-" + Guid.NewGuid().ToString("N"));
string docs = Path.Combine(root, "docs"), data = Path.Combine(root, "data");
Directory.CreateDirectory(docs);
Directory.CreateDirectory(data);
File.WriteAllText(Path.Combine(docs, "guide.txt"), "Read me first.\n");
File.WriteAllText(Path.Combine(data, "a.txt"), "first\n");

try
{
    await using SurlServer first = await SurlServer.StartAsync(Options(docs, data));
    await using CurlClient client = CurlClient.Create();
    string url = first.BoundUrl.ToString();

    switch (which)
    {
        case "serve":
            Console.WriteLine($"docs: {await Get(url + "docs/guide.txt")}");
            Console.WriteLine($"data: {await Get(url + "data/a.txt")}");
            break;
        case "readonly":
            Console.WriteLine($"upload: {await Put(url + "docs/up.txt")}");
            Console.WriteLine($"on disk: {File.Exists(Path.Combine(docs, "up.txt"))}");
            break;
        case "writable":
            Console.WriteLine($"upload: {await Put(url + "data/up.txt")}");
            Console.WriteLine($"on disk: {File.ReadAllText(Path.Combine(data, "up.txt")).TrimEnd()}");
            break;
        case "second-readonly":
        {
            // The same readonly folder, on a second server in the same process.
            await using SurlServer second = await SurlServer.StartAsync(new SurlServerOptions
            {
                ListenUrls = { "http://127.0.0.1:0" },
                Shares = { "docs=" + docs + ";readonly" },
            });
            Console.WriteLine($"second docs: {await Get(second.BoundUrl + "docs/guide.txt")}");
            break;
        }
        case "second-writable":
            await using (await SurlServer.StartAsync(new SurlServerOptions
            {
                ListenUrls = { "http://127.0.0.1:0" },
                Shares = { "data=" + data },
            })) { }
            Console.WriteLine("second: started");
            break;
        default:
            throw new ArgumentException($"unknown mode {which}");
    }
    return 0;

    // A GET: the body for a 200, the status for anything else.
    async Task<string> Get(string address)
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, address), CancellationToken.None);
        await using Stream body = response.Body;
        string text = await new StreamReader(body).ReadToEndAsync();
        return (int)response.StatusCode == 200 ? text.TrimEnd() : ((int)response.StatusCode).ToString();
    }

    // A PUT of a small file: the status code.
    async Task<string> Put(string address)
    {
        CurlResponse response = await client.SendAsync(
            new CurlRequest(HttpMethod.Put, address) { Body = "uploaded\n"u8.ToArray() }, CancellationToken.None);
        await using Stream body = response.Body;
        return ((int)response.StatusCode).ToString();
    }
}
catch (SurlServerStartException refused)
{
    // The first line surl would have written after "surl: ".
    Console.WriteLine($"second refused: {refused.Lines[0]}");
    return 0;
}

static SurlServerOptions Options(string docs, string data) => new()
{
    ListenUrls = { "http://127.0.0.1:0" },
    AllowAnonymous = true,
    AllowUploads = true,
    Shares = { "docs=" + docs + ";readonly", "data=" + data },
};
