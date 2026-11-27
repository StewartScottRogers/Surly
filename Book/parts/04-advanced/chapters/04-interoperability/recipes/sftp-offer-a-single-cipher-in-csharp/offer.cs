// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# file-based app: `dotnet run offer.cs -- SCENARIO`. The keys come from setup: in recipe.yml:
// host_key and client_key, each with a .pub half.
//   narrowed (the default), file, other-cipher, other-mac, default-offer, default-macs, two, unknown-cipher, unknown-mac, weak-cipher, weak-allowed
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Buffers.Binary;
using System.Text;
using Curl.Api;
using Curl.Api.Options.Protocols;
using Surl.Api;

string scenario = args.Length > 0 ? args[0] : "narrowed";

// The server's own words on the cipher and MAC options, as scenario by scenario arguments.
string[] extra = scenario switch
{
    "narrowed" or "file" or "other-cipher" or "other-mac" => ["--ssh-ciphers", "aes256-ctr", "--ssh-macs", "hmac-sha2-256"],
    "two" => ["--ssh-ciphers", "aes256-ctr,aes128-ctr", "--ssh-macs", "hmac-sha2-256"],
    "unknown-cipher" => ["--ssh-ciphers", "bogus"],
    "unknown-mac" => ["--ssh-macs", "bogus"],
    "weak-cipher" => ["--ssh-ciphers", "aes128-cbc"],
    "weak-allowed" => ["--allow-weak-ssh-algorithms", "--ssh-ciphers", "aes128-cbc"],
    _ => [],
};

var options = new SurlServerOptions { ListenUrls = { "sftp://127.0.0.1:0" }, RecordExchanges = true };
options.HostKeyFiles.Add("host_key");
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
foreach (string word in new[] { "--authorized-keys", "alice:client_key.pub" }.Concat(extra))
    options.AdditionalArguments.Add(word);

// A refused option is an exception at start, with surl's own line first.
SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException e)
{
    Console.WriteLine($"{e.ExitCode}: {e.Lines[0]}");
    return 0;
}

await using (server)
{
    // Curl.Api logs in with the client key and fetches the file; the exchange it makes holds the server's offer.
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl.ToString().Replace("sftp://", "sftp://alice@")}hello.txt")
    {
        Ssh = new CurlSshOptions { PrivateKeyFile = "client_key", PublicKeyFile = "client_key.pub", AuthenticationTypes = SshAuthenticationTypes.PublicKey },
        Tls = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { VerifyPeer = false } },
    };
    await using CurlClient client = CurlClient.Create();
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();
    await client.DisposeAsync();

    // The server's key exchange message follows its version line: a length, a padding length, message type 20 and a
    // 16-byte cookie, then name lists: key exchange, host key, ciphers each way, MACs each way.
    byte[] sent = server.Exchanges.First().Sent;
    int at = Array.IndexOf(sent, (byte)'\n') + 1 + 4 + 1 + 1 + 16;
    var lists = new List<string[]>();
    for (int i = 0; i < 6; i++)
    {
        int length = BinaryPrimitives.ReadInt32BigEndian(sent.AsSpan(at, 4));
        lists.Add(Encoding.ASCII.GetString(sent, at + 4, length).Split(','));
        at += 4 + length;
    }
    string[] ciphers = lists[2], macs = lists[4];

    switch (scenario)
    {
        case "narrowed" or "two":
            Console.WriteLine($"ciphers: {string.Join(", ", ciphers)}");
            Console.WriteLine($"MACs: {string.Join(", ", macs)}");
            break;
        case "file": Console.Write(text); break;
        case "other-cipher": Console.WriteLine($"offers aes128-ctr: {ciphers.Contains("aes128-ctr")}"); break;
        case "other-mac": Console.WriteLine($"offers hmac-sha2-512: {macs.Contains("hmac-sha2-512")}"); break;
        case "default-macs": Console.WriteLine($"first MAC: {macs[0]}"); break;
        case "default-offer":
            Console.WriteLine($"first cipher: {ciphers[0]}");
            Console.WriteLine($"offers aes256-ctr as well: {ciphers.Contains("aes256-ctr")}");
            Console.WriteLine($"offers more than one MAC: {macs.Length > 1}");
            Console.WriteLine($"offers a CBC cipher: {ciphers.Any(c => c.EndsWith("-cbc"))}");
            break;
        default: Console.WriteLine($"ciphers: {string.Join(", ", ciphers)}"); break;
    }
}
return 0;
