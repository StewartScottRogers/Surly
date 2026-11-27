// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Builds a nested folder of files in a running in-process server with Files.PutAsync, then looks
// at it as HTTP and FTP clients and as the store itself.
// Run: dotnet run program.cs [-- list|ftp-listing|ftp-file|http-folder|overwrite|relative|no-login|listings-off]
//   (no argument)  seed the tree, fetch every file over HTTP
//   list           walk the tree with Files.ListAsync
//   ftp-listing    list the root and a folder over FTP
//   ftp-file       fetch a nested file over FTP
//   http-folder    ask HTTP for a folder
//   overwrite      put one path twice and fetch it
//   relative       put a path with no leading slash
//   no-login       a server that accepts no anonymous login
//   listings-off   a server that answers no FTP listings
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "solution";

// One server, two schemes: HTTP and FTP see the same store. Port 0 takes a free port for each.
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0", "ftp://127.0.0.1:0" },
    AllowAnonymous = which != "no-login",
    ListDirectories = which != "listings-off",
};
await using SurlServer server = await SurlServer.StartAsync(options);
string http = server.BoundUrls[0].ToString(), ftp = server.BoundUrls[1].ToString();
await using CurlClient client = CurlClient.Create();

// The tree, built after the server is up. Paths start with a slash; folders come with their files.
var tree = new Dictionary<string, string>
{
    ["/readme.txt"] = "top level\n",
    ["/reports/index.txt"] = "reports\n",
    ["/reports/2026/q1.csv"] = "region,total\nnorth,10\n",
    ["/reports/2026/q2.csv"] = "region,total\nnorth,12\n",
    ["/reports/2027/plan.txt"] = "next year\n",
};
if (which != "relative")
{
    foreach ((string path, string text) in tree)
    {
        await server.Files.PutAsync(path, Encoding.UTF8.GetBytes(text));
    }
}

try
{
    switch (which)
    {
        case "solution":
            foreach (string path in tree.Keys.Order())
            {
                (int status, string body) = await Get(http + path[1..]);
                Console.WriteLine($"{path}: {status} {body.Length} bytes");
            }

            break;
        case "list":
            await Walk("/", "");
            break;
        case "ftp-listing":
            foreach (string folder in new[] { "", "reports/", "reports/2026/" })
            {
                // The long listing: the first character is the type, the last word the name.
                string listing = (await Get(ftp + folder)).Body;
                string names = string.Join(" ", listing.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim().Split(' ').Last() + (l[0] == 'd' ? "/" : "")));
                Console.WriteLine($"/{folder}: {names}");
            }

            break;
        case "ftp-file":
            Console.Write((await Get(ftp + "reports/2026/q1.csv")).Body);
            break;
        case "http-folder":
            Console.WriteLine($"/reports/: {(await Get(http + "reports/")).Status}");
            Console.WriteLine($"/reports/2026/: {(await Get(http + "reports/2026/")).Status}");
            break;
        case "overwrite":
            await server.Files.PutAsync("/reports/index.txt", "revised\n"u8.ToArray());
            Console.Write((await Get(http + "reports/index.txt")).Body);
            break;
        case "relative":
            await server.Files.PutAsync("reports/index.txt", "x\n"u8.ToArray());
            break;
        case "no-login":
            Console.WriteLine($"HTTP: {(await Get(http + "readme.txt")).Status}");
            await Get(ftp + "readme.txt");
            break;
        case "listings-off":
            await Walk("/reports", "");
            await Get(ftp + "reports/");
            break;
    }
}
catch (CurlException failure)
{
    // The numbers curl exits with: 67 for a refused login, 19 for a listing it cannot get.
    Console.WriteLine($"CurlException {(int)failure.Code} {failure.Code}");
}
catch (ArgumentException failure)
{
    Console.WriteLine($"ArgumentException: {failure.Message.Split('\n')[0]}");
}

return 0;

async Task<(int Status, string Body)> Get(string url)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url), CancellationToken.None);
    await using Stream body = response.Body;
    return (response.StatusCode, await new StreamReader(body).ReadToEndAsync());
}

// Files.ListAsync lists one level of the store, in name order, whatever the listing options say.
async Task Walk(string folder, string indent)
{
    SurlFolderPage page = await server.Files.ListAsync(folder);
    foreach (SurlFolderEntry entry in page.Entries)
    {
        Console.WriteLine(entry.IsFolder ? $"{indent}{entry.Name}/" : $"{indent}{entry.Name} ({entry.ByteCount} bytes)");
        if (entry.IsFolder) await Walk($"{folder.TrimEnd('/')}/{entry.Name}", indent + "  ");
    }
}
