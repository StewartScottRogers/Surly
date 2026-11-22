// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that a pinned SFTP host key survives restarts, and that a changed key is refused.
// Run: dotnet run program.cs -- <scenario>
//   kept       three starts with host_key; fetch with the pin after each
//   rekeyed    one start with other_key; fetch with host_key's pin
//   throwaway  two starts with a throwaway key; compare the keys served
//   nokey      a start with no host key at all
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Protocols;
using Surl.Api;
using Surl.Output;

string scenario = args[0];

// The pin, made once from host_key.pub before any server runs: the SHA-256 of the key's blob,
// in base64 without padding, as ssh-keygen -l writes it (curl --hostpubsha256).
byte[] blob = Convert.FromBase64String(File.ReadAllText("host_key.pub").Split(' ')[1]);
string pin = Convert.ToBase64String(SHA256.HashData(blob)).TrimEnd('=');
await using CurlClient client = CurlClient.Create();

// One start of the server, with its verbose log kept, and one fetch with the pin.
async Task<string> StartAndFetch(string? keyFile, StringWriter log)
{
    var options = new SurlServerOptions
    {
        ListenUrls = { "sftp://127.0.0.1:0" },
        Accounts = { new SurlAccount("alice", "s3cret") },
        ThrowawayHostKey = keyFile is null,
        Log = TextWriter.Synchronized(log),
        LogLevel = LogLevel.Verbose,
    };
    if (keyFile is not null)
    {
        options.HostKeyFiles.Add(keyFile);
    }

    options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
    await using SurlServer server = await SurlServer.StartAsync(options);
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt")
    {
        Authentication = new CurlAuthenticationOptions { UserName = "alice", Password = "s3cret" },
        Ssh = new CurlSshOptions { HostPublicKeySha256 = pin },
    };
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        return (await new StreamReader(body).ReadToEndAsync()).TrimEnd();
    }
    catch (CurlException e)
    {
        return $"refused: {e.Code}";
    }
}

// The fingerprint a start's log says it served.
string Served(StringWriter log) => Regex.Match(log.ToString(), @"Serving SSH host key \S+ --hostpubsha256 (\S+)").Groups[1].Value;

try
{
    switch (scenario)
    {
        case "kept":
            var served = new List<string>();
            for (int start = 1; start <= 3; start++)
            {
                var log = new StringWriter();
                Console.WriteLine($"start {start}: {await StartAndFetch("host_key", log)}");
                served.Add(Served(log));
            }

            Console.WriteLine(served.Distinct().Count() == 1 ? "one key served" : "keys differ");
            return 0;
        case "rekeyed":
            var rekeyed = new StringWriter();
            Console.WriteLine(await StartAndFetch("other_key", rekeyed));
            string text = rekeyed.ToString();
            Console.WriteLine($"key exchange logged: {text.Contains("SSH negotiated kex")}, login request logged: {text.Contains("login request")}");
            return 1;
        case "throwaway":
            var first = new StringWriter();
            var second = new StringWriter();
            await StartAndFetch(null, first);
            await StartAndFetch(null, second);
            Console.WriteLine(first.ToString().Split('\n')[0].TrimEnd('\r'));
            Console.WriteLine(Served(first) == Served(second) ? "same key" : "a different key after the restart");
            return 0;
        default:
            var none = new SurlServerOptions { ListenUrls = { "sftp://127.0.0.1:0" } };
            await using (await SurlServer.StartAsync(none))
            {
            }

            return 0;
    }
}
catch (SurlServerStartException e)
{
    Console.WriteLine($"server refused to start: {(int)e.ExitCode} {e.ExitCode}");
    Console.WriteLine(e.Lines[0]);
    return (int)e.ExitCode;
}
