// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of moving a client from FTP to SFTP: one in-process server serves both from one store, each
// client fetches the same report, and the copies and the server's log are compared.
// Run: dotnet run move.cs [-- listing|missing|password|pin|wrong-pin|cutover]
//   (no argument) the report over both protocols, compared, and the logins the server recorded
//   listing       the folder's names over both
//   missing       a file that is not there, over both
//   password      a wrong password, over both
//   pin           SFTP pinned to the server's host key
//   wrong-pin     SFTP pinned to a fingerprint that is not the server's
//   cutover       a server that refuses plaintext passwords: FTP is refused, SFTP is served
// host_key and host_key.pub come from setup: in recipe.yml.
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Security.Cryptography;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Network;
using Surl.Api;
using Surl.Output;

string which = args.Length > 0 ? args[0] : "move";

// The report: 3,000 bytes that repeat from run to run, so a copy that differs by one byte is caught.
byte[] report = new byte[3000];
new Random(7).NextBytes(report);

// The server: both listeners, one store, one account. The old client's password crosses the wire in
// clear, so FTP needs AllowPlaintextAuthentication, which "cutover" leaves out.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0", "sftp://127.0.0.1:0" },
    Accounts = { new SurlAccount("alice", "s3cret") },
    AllowPlaintextAuthentication = which != "cutover",
    ListDirectories = true,
    Log = log,
    LogLevel = LogLevel.Verbose,
};
options.HostKeyFiles.Add("host_key");
options.Files["report.bin"] = report;
options.Files["notes.txt"] = "release notes\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
string ftp = server.BoundUrls[0].ToString();
string sftp = server.BoundUrls[1].ToString();
await using CurlClient client = CurlClient.Create();

// The pin: the SHA-256 of the host key's blob, in base64 without padding, as ssh-keygen -l writes it.
byte[] blob = Convert.FromBase64String(File.ReadAllText("host_key.pub").Split(' ')[1]);
string pin = Convert.ToBase64String(SHA256.HashData(blob)).TrimEnd('=');

switch (which)
{
    case "move":
        byte[]? overFtp = await Fetch("over ftp", ftp + "report.bin", "s3cret", null);
        byte[]? overSftp = await Fetch("over sftp", sftp + "report.bin", "s3cret", null);
        Console.WriteLine(overFtp!.AsSpan().SequenceEqual(overSftp) && overFtp.AsSpan().SequenceEqual(report)
            ? "the two copies are identical to each other and to the server's"
            : "the copies DIFFER");
        string[] lines = log.ToString().Split('\n');
        Console.WriteLine($"ftp logins recorded: {lines.Count(l => l.Contains("Login accepted: ftp alice"))}");
        Console.WriteLine($"sftp logins recorded: {lines.Count(l => l.Contains("Login accepted: password alice"))}");
        break;
    case "listing":
        Console.WriteLine($"ftp: {await Names(ftp)}");
        Console.WriteLine($"sftp: {await Names(sftp)}");
        break;
    case "missing":
        await Fetch("ftp", ftp + "nope.bin", "s3cret", null);
        await Fetch("sftp", sftp + "nope.bin", "s3cret", null);
        break;
    case "password":
        await Fetch("ftp", ftp + "report.bin", "wrong", null);
        await Fetch("sftp", sftp + "report.bin", "wrong", null);
        break;
    case "pin":
        byte[]? pinned = await Fetch("pinned sftp", sftp + "report.bin", "s3cret", pin);
        Console.WriteLine(pinned!.AsSpan().SequenceEqual(report) ? "identical to the server's" : "DIFFERENT from the server's");
        break;
    case "wrong-pin":
        await Fetch("pinned sftp", sftp + "report.bin", "s3cret", new string('A', 43));
        break;
    case "cutover":
        await Fetch("ftp", ftp + "report.bin", "s3cret", null);
        byte[]? kept = await Fetch("sftp", sftp + "report.bin", "s3cret", null);
        Console.WriteLine(kept!.AsSpan().SequenceEqual(report) ? "identical to the server's" : "DIFFERENT from the server's");
        break;
}

return 0;

// One fetch: the bytes, or null with the code that refused it. A hostPin of null skips the host key check
// (curl -k), which is for a key you made yourself; a pin checks it.
async Task<byte[]?> Fetch(string label, string url, string password, string? hostPin)
{
    try
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url)
        {
            Authentication = new CurlAuthenticationOptions { UserName = "alice", Password = password },
            Tls = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { VerifyPeer = false } },
            Ssh = hostPin is null ? null : new CurlSshOptions { HostPublicKeySha256 = hostPin },
        }, CancellationToken.None);
        await using Stream body = response.Body;
        using var copy = new MemoryStream();
        await body.CopyToAsync(copy);
        Console.WriteLine($"{label}: {copy.Length} bytes");
        return copy.ToArray();
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{label}: {(int)failure.Code} {failure.Code}");
        return null;
    }
}

// The names of the root folder: the long listing's last word on each line, in order.
async Task<string> Names(string url)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url)
    {
        Authentication = new CurlAuthenticationOptions { UserName = "alice", Password = "s3cret" },
        Tls = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { VerifyPeer = false } },
    }, CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();
    return string.Join(" ", text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.TrimEnd('\r')).Select(line => line[(line.LastIndexOf(' ') + 1)..]));
}
