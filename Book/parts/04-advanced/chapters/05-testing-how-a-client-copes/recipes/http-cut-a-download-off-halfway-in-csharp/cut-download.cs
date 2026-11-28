// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# test of a downloader that must resume, not restart, when a transfer is cut off: Surl.Api serves
// a large file and drops every connection after 4,000,000 bytes, and Curl.Api downloads it to a file.
// Run: dotnet run cut-download.cs -- <case>
//   resume   download, get cut off, resume with a Range request (the default)
//   restart  download three times from the start, as a client that does not resume would
//   short    a short file through the same rule
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "resume";

// big.txt: 5,000,000 random hexadecimal digits and a line end, too long for one connection.
byte[] big = System.Text.Encoding.ASCII.GetBytes(Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(2_500_000)) + "\n");

// The server drops every connection once it has sent 4,000,000 bytes on it, the head included.
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    Faults = { SurlFault.DropConnection() with { AfterBytes = 4_000_000 } },
    RecordExchanges = true,
};
options.Files["big.txt"] = big;
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);

await using CurlClient client = CurlClient.Create(new CurlClientOptions { Timeout = TimeSpan.FromSeconds(30) });
string path = Path.Combine("dl", "big.txt");
Directory.CreateDirectory("dl");
File.Delete(path);

if (which == "short")
{
    CurlResponse small = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt"), CancellationToken.None);
    await using Stream body = small.Body;
    Console.WriteLine($"{small.StatusCode}, {(await new StreamReader(body).ReadToEndAsync()).TrimEnd()}");
    return 0;
}

// One try: ask for the bytes after what the file already holds, append what comes, and say how it ended.
async Task<bool> Fetch(bool resume)
{
    long have = resume && File.Exists(path) ? new FileInfo(path).Length : 0;
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}big.txt")
    {
        Headers = have > 0 ? [new("Range", $"bytes={have}-")] : [],
    };
    long got = 0;
    int status = 0;
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        status = response.StatusCode;
        await using Stream body = response.Body;
        await using FileStream file = new(path, have > 0 ? FileMode.Append : FileMode.Create);
        byte[] buffer = new byte[65536];
        for (int n; (n = await body.ReadAsync(buffer)) > 0; got += n)
            await file.WriteAsync(buffer.AsMemory(0, n));
        Console.WriteLine($"{(have > 0 ? "resumed" : "from the start")}: {status}, {got} bytes, complete");
        return true;
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{(have > 0 ? "resumed" : "from the start")}: {status}, {got} bytes, then CurlException {(int)failure.Code} {failure.Code}");
        return false;
    }
}

if (which == "restart")
{
    int cut = 0;
    for (int i = 0; i < 3; i++)
        if (!await Fetch(resume: false)) cut++;
    Console.WriteLine($"restarted 3 times: {cut} cut off");
    return 0;
}

// A downloader that resumes until the file is whole: each cut connection is followed by a Range request
// for the bytes after what is on disk. Five resumes are far more than a 5 MB file cut at 4 MB needs.
int cuts = 0;
if (!await Fetch(resume: false))
{
    cuts++;
    Console.WriteLine($"partial file: {new FileInfo(path).Length} bytes");
    for (int resumes = 0; resumes < 5; resumes++)
    {
        if (await Fetch(resume: true)) break;
        cuts++;
    }
}
Console.WriteLine($"whole file: {(File.ReadAllBytes(path).AsSpan().SequenceEqual(big) ? "yes" : "no")}");
// The server's own account: one fault_applied event, a drop, for each cut connection.
var drops = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).OfType<FaultAppliedEvent>().ToList();
Console.WriteLine($"fault events: {drops.Count} for {cuts} cut connections, all Drop: {drops.All(d => d.Kind == FaultKind.Drop)}");
return 0;
