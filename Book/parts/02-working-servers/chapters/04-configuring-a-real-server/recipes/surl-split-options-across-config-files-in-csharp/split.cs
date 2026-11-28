// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# file-based app: `dotnet run split.cs -- SCENARIO`. base.conf and test.conf are fixtures in recipe.yml,
// the same files the console recipe starts surl with.
//   both (the default), second-account, login-needed, later-file-wins, under-the-limit, ftp-limit, ftp-same-directory,
//   order-matters, command-line-after-wins, property-before-loses, command-line-directory, no-allow-after-the-files, typo
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string scenario = args.Length > 0 ? args[0] : "both";

// Surl.Api reads ConfigFiles in order as -K, then AdditionalArguments as the rest of the command line: so
// the last word on an option wins. The listen URLs are in code, on port 0; the options are in the files.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0", "ftp://127.0.0.1:0" } };
switch (scenario)
{
    case "order-matters": Add(options.ConfigFiles, ["test.conf", "base.conf"]); break;
    case "typo": Add(options.ConfigFiles, ["base.conf", "bad.conf"]); break;
    default: Add(options.ConfigFiles, ["base.conf", "test.conf"]); break;
}
if (scenario == "order-matters") Add(options.AdditionalArguments, ["--directory", "site-b"]);
if (scenario == "command-line-after-wins") Add(options.AdditionalArguments, ["--directory", "site-c", "--max-filesize", "5"]);
if (scenario == "property-before-loses") { options.MaxFileSize = 5; Add(options.AdditionalArguments, ["--directory", "site-d"]); }
if (scenario == "command-line-directory") Add(options.AdditionalArguments, ["--directory", "site-b"]);
if (scenario == "no-allow-after-the-files") Add(options.AdditionalArguments, ["--directory", "site-e", "--no-allow-uploads"]);

void Add(IList<string> list, string[] words) { foreach (string word in words) list.Add(word); }

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException refused)
{
    Console.WriteLine($"refused: {refused.ExitCode}, {refused.Lines[0]}");
    return 0;
}

await using (server)
await using (CurlClient client = CurlClient.Create())
{
    string http = server.BoundUrls[0].ToString(), ftp = server.BoundUrls[1].ToString();
    var tester = new CurlAuthenticationOptions { UserName = "tester", Password = "secret" };
    byte[] twentyOne = Encoding.ASCII.GetBytes("0123456789abcdefghij\n"), eight = Encoding.ASCII.GetBytes("1234567\n");

    // A GET: the body, or the status when it is not 2xx.
    async Task<string> Get(CurlAuthenticationOptions? login)
    {
        var request = new CurlRequest(HttpMethod.Get, http + "hello.txt") { Authentication = login };
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        string text = await new StreamReader(body).ReadToEndAsync();
        return response.StatusCode == 200 ? text.TrimEnd() : $"status {response.StatusCode}";
    }

    // A PUT over HTTP: the status the server answered.
    async Task<int> Put(byte[] bytes)
    {
        var request = new CurlRequest(HttpMethod.Put, http + scenario + ".txt") { Authentication = tester, Body = bytes };
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await response.Body.DisposeAsync();
        return response.StatusCode;
    }

    switch (scenario)
    {
        case "both": Console.WriteLine($"tester: {await Get(tester)}"); break;
        case "second-account": Console.WriteLine($"second: {await Get(new CurlAuthenticationOptions { UserName = "second", Password = "pw" })}"); break;
        case "login-needed": Console.WriteLine($"no login: {await Get(null)}"); break;
        case "later-file-wins": Console.WriteLine($"21 bytes: {await Put(twentyOne)}"); break;
        case "under-the-limit": Console.WriteLine($"8 bytes: {await Put(eight)}"); break;
        case "order-matters": Console.WriteLine($"21 bytes: {await Put(twentyOne)}"); break;
        case "command-line-after-wins" or "property-before-loses": Console.WriteLine($"8 bytes: {await Put(eight)}"); break;
        case "command-line-directory": Console.WriteLine($"hello.txt: {await Get(tester)}"); break;
        case "no-allow-after-the-files": Console.WriteLine($"8 bytes: {await Put(eight)}"); break;
        case "ftp-limit" or "ftp-same-directory":
            byte[] bytes = scenario == "ftp-limit" ? twentyOne : eight;
            try
            {
                var upload = new CurlRequest(HttpMethod.Put, ftp + scenario + ".txt") { Authentication = tester, Body = bytes };
                CurlResponse sent = await client.SendAsync(upload, CancellationToken.None);
                await sent.Body.DisposeAsync();
                Console.WriteLine($"ftp: ok, the directory holds {Encoding.ASCII.GetString((await server.Files.ReadAsync("/" + scenario + ".txt")).Bytes.Span).TrimEnd()}");
            }
            catch (CurlException e)
            {
                Console.WriteLine($"ftp: {e.Code} ({(int)e.Code})");
            }
            break;
    }
}
return 0;
