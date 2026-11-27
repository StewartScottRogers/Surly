// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A nightly FTP download, ported from FtpWebRequest to Curl.Api, and the test that proves the port: both
// jobs run against one in-process stand-in, and what each wrote is compared with the server's files.
// Run: dotnet run nightly.cs [-- listing|missing|password|commands|plaintext]
//   (no argument) both jobs list /out and download every file; the copies are compared
//   listing       the names each job found
//   missing       each job asked for a file that is not there
//   password      each job with a wrong password
//   commands      what each job sent the server, from the recorded exchanges
//   plaintext     a stand-in left at its default, which refuses a password in clear
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Net;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

#pragma warning disable SYSLIB0014 // FtpWebRequest is obsolete: it is the code being ported away from.

string which = args.Length > 0 ? args[0] : "jobs";

// The stand-in: the night's three files under /out, one account. The job's password crosses the wire
// in clear, as it does against the real server, so AllowPlaintextAuthentication is on but for "plaintext".
byte[] blob = new byte[5000];
new Random(11).NextBytes(blob);
var files = new Dictionary<string, byte[]>
{
    ["orders.csv"] = "id,total\r\n1,40\r\n2,75\r\n"u8.ToArray(),
    ["refunds.csv"] = "id,total\r\n7,-15\r\n"u8.ToArray(),
    ["scan.bin"] = blob,
};
var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0" },
    Accounts = { new SurlAccount("nightly", "s3cret") },
    AllowPlaintextAuthentication = which != "plaintext",
    ListDirectories = true,
    RecordExchanges = true,
};
foreach (var (name, bytes) in files)
{
    options.Files[$"out/{name}"] = bytes;
}

await using SurlServer server = await SurlServer.StartAsync(options);
string folder = $"{server.BoundUrl}out/";
await using CurlClient client = CurlClient.Create();

switch (which)
{
    case "jobs":
        Report("old job", await OldJob("old", "s3cret"));
        Report("new job", await NewJob("new", "s3cret"));
        break;
    case "listing":
        Console.WriteLine($"old job lists: {string.Join(" ", await OldList("s3cret"))}");
        Console.WriteLine($"new job lists: {string.Join(" ", await NewList("s3cret"))}");
        break;
    case "missing":
        await Try("old job", () => OldGet("missing.csv", "s3cret"));
        await Try("new job", () => NewGet("missing.csv", "s3cret"));
        break;
    case "password":
    case "plaintext":
        string password = which == "password" ? "wrong" : "s3cret";
        await Try("old job", () => OldList(password));
        await Try("new job", () => NewList(password));
        break;
    case "commands":
        await OldJob("old", "s3cret");
        int split = server.Exchanges.Count;
        await NewJob("new", "s3cret");
        Commands("old job", server.Exchanges.Take(split));
        Commands("new job", server.Exchanges.Skip(split));
        break;
}

return 0;

// The old job: list the folder, then download each file, all with FtpWebRequest.
async Task<string> OldJob(string into, string password)
{
    Directory.CreateDirectory(into);
    foreach (string name in await OldList(password))
    {
        await File.WriteAllBytesAsync(Path.Combine(into, name), await OldGet(name, password));
    }

    return into;
}

async Task<List<string>> OldList(string password)
{
    using var response = (FtpWebResponse)await Old(folder, WebRequestMethods.Ftp.ListDirectory, password).GetResponseAsync();
    string text = await new StreamReader(response.GetResponseStream()).ReadToEndAsync();
    return text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).ToList();
}

async Task<byte[]> OldGet(string name, string password)
{
    using var response = (FtpWebResponse)await Old(folder + name, WebRequestMethods.Ftp.DownloadFile, password).GetResponseAsync();
    using var copy = new MemoryStream();
    await response.GetResponseStream().CopyToAsync(copy);
    return copy.ToArray();
}

FtpWebRequest Old(string url, string method, string password)
{
    var request = (FtpWebRequest)WebRequest.Create(url);
    request.Method = method;
    request.Credentials = new NetworkCredential("nightly", password);
    request.UseBinary = true;
    request.UsePassive = true;
    return request;
}

// The new job: the same steps with Curl.Api, through one CurlClient, so one control connection.
async Task<string> NewJob(string into, string password)
{
    Directory.CreateDirectory(into);
    foreach (string name in await NewList(password))
    {
        await File.WriteAllBytesAsync(Path.Combine(into, name), await NewGet(name, password));
    }

    return into;
}

async Task<List<string>> NewList(string password)
{
    // A GET on a folder URL is the long listing; the name is the last word of each line.
    string text = Encoding.UTF8.GetString(await NewGet("", password));
    return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.TrimEnd('\r')).Select(line => line[(line.LastIndexOf(' ') + 1)..]).ToList();
}

async Task<byte[]> NewGet(string name, string password)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, folder + name)
    {
        Authentication = new CurlAuthenticationOptions { UserName = "nightly", Password = password },
    }, CancellationToken.None);
    await using Stream body = response.Body;
    using var copy = new MemoryStream();
    await body.CopyToAsync(copy);
    return copy.ToArray();
}

// The test: every file the job wrote against the server's bytes.
void Report(string job, string into)
{
    string[] names = Directory.GetFiles(into).Select(Path.GetFileName).Order().ToArray()!;
    bool same = names.SequenceEqual(files.Keys.Order()) && names.All(n => File.ReadAllBytes(Path.Combine(into, n)).AsSpan().SequenceEqual(files[n]));
    Console.WriteLine($"{job}: {names.Length} files, {(same ? "identical to the server's" : "DIFFERENT from the server's")}");
}

// A step that should fail: how each API says so.
async Task Try(string job, Func<Task> step)
{
    try
    {
        await step();
        Console.WriteLine($"{job}: no failure");
    }
    catch (WebException failure) when (failure.Response is FtpWebResponse ftp)
    {
        Console.WriteLine($"{job}: WebException, {(int)ftp.StatusCode} {ftp.StatusCode}");
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{job}: CurlException, {(int)failure.Code} {failure.Code}");
    }
}

// The commands one job sent, from the control connections the server recorded: how many connections,
// and the distinct command words in the order first sent.
void Commands(string job, IEnumerable<SurlExchangeRecord> exchanges)
{
    var list = exchanges.ToList();
    var words = list.SelectMany(x => Encoding.ASCII.GetString(x.Received).Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        .Select(line => line.Split(' ')[0]).Where(word => word.All(char.IsUpper)).Distinct();
    Console.WriteLine($"{job}: {list.Count} connection(s), {string.Join(" ", words)}");
}
