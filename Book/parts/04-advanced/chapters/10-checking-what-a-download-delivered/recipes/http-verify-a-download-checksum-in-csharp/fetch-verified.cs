// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An installer's download step, tested in process: it keeps a file only if its SHA-256 is the
// published one. Surl.Api serves the file; Curl.Api downloads it.
// Run: dotnet run fetch-verified.cs [-- wrong-digest|changed|missing]
//   (no argument) download tool-1.0.bin and check it against the published digest
//   wrong-digest  check the same download against another digest
//   changed       keep a good copy, change the served file, and download it again
//   missing       download a file the server does not have
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Security.Cryptography;
using Curl.Api;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "match";
const string Published = "0e2fb8e221331b806feda5547ebd3fa59614053f86f9f0d92b8e840976e0e80c";

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["tool-1.0.bin"] = "installer for tool 1.0\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

int exit;
switch (which)
{
    case "wrong-digest":
        exit = await FetchVerified("tool-1.0.bin", "3a781f0b1f6c0a682e41bd887f6307082508282ab9c96d8a6bf1dc62f0a237c2", "other.bin");
        break;
    case "changed":
        await FetchVerified("tool-1.0.bin", Published, "tool-1.0.bin");
        await server.Files.PutAsync("/tool-1.0.bin", "installer for tool 1.0, with something added\n"u8.ToArray());
        exit = await FetchVerified("tool-1.0.bin", Published, "tool-1.0.bin");
        Console.WriteLine($"on disk: tool-1.0.bin {(File.Exists("tool-1.0.bin") ? File.ReadAllText("tool-1.0.bin").TrimEnd() : "missing")}; .part {(File.Exists("tool-1.0.bin.part") ? "left" : "gone")}");
        break;
    case "missing":
        exit = await FetchVerified("tool-2.0.bin", Published, "tool-2.0.bin");
        Console.WriteLine($"on disk: tool-2.0.bin {(File.Exists("tool-2.0.bin") ? "written" : "missing")}");
        break;
    default:
        exit = await FetchVerified("tool-1.0.bin", Published, "tool-1.0.bin");
        break;
}

return exit;

// Downloads path to file.part, hashing as it writes; renames it to file only if the digest matches.
async Task<int> FetchVerified(string path, string want, string file)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
    await using Stream body = response.Body;
    if (response.StatusCode >= 400)
    {
        Console.WriteLine($"refused {file}: {response.StatusCode} {response.ReasonPhrase}");
        return 22;
    }

    string got;
    await using (FileStream part = File.Create(file + ".part"))
    using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
    {
        byte[] buffer = new byte[81920];
        for (int n; (n = await body.ReadAsync(buffer)) > 0;)
        {
            hash.AppendData(buffer, 0, n);
            await part.WriteAsync(buffer.AsMemory(0, n));
        }

        got = Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    if (got != want)
    {
        File.Delete(file + ".part");
        Console.WriteLine($"refused {file}: SHA-256 is {got}, not {want}");
        return 1;
    }

    File.Move(file + ".part", file, overwrite: true);
    Console.WriteLine($"kept {file}: SHA-256 {got}");
    return 0;
}
