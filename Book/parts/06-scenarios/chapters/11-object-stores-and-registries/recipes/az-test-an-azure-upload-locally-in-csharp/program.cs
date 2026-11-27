// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of an app's Azure blob upload against a stand-in: Surl.Api serves az as plain HTTP with a Bearer
// token, and Curl.Api puts and gets blobs over http:// as an Azure SDK pointed at an emulator would.
// Run: dotnet run program.cs [-- <case>]
//   put (default)  put a blob, then read the stored file from the server
//   get-back       put a blob, then get it back over HTTP
//   md5            compare the put answer's Content-MD5 with the file's own hash
//   listed         list the container after a put
//   no-token, wrong-token, version-header, blob-type-header, missing-blob   refusals
//   uploads-off, uploads-off-reads   a server with uploads off: a put, then a get
//   plaintext-auth   a server without AllowPlaintextAuthentication
//   no-certificate   a server without --az-plaintext
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Security.Cryptography;
using System.Text;
using Curl.Api;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "put";
byte[] note = "uploaded over az\n"u8.ToArray();

// The stand-in: one Bearer token (an account with an empty name), uploads and listings on.
var options = new SurlServerOptions
{
    ListenUrls = { "az://127.0.0.1:0" },
    AllowUploads = which is not ("uploads-off" or "uploads-off-reads"),
    ListDirectories = true,
    AllowPlaintextAuthentication = which != "plaintext-auth",
};
if (which != "no-certificate") options.AdditionalArguments.Add("--az-plaintext");
options.Accounts.Add(new SurlAccount("", "t0ken-abc"));
options.Files["photos/seed.txt"] = "already here\n"u8.ToArray();

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException refused)
{
    Console.WriteLine($"start refused: exit {(int)refused.ExitCode}, {refused.Lines[0]}");
    return 0;
}

await using (server)
await using (CurlClient client = CurlClient.Create())
{
    // BoundUrl is az://127.0.0.1:port/; the plain-HTTP endpoint is the same address under http://.
    string endpoint = $"http://127.0.0.1:{server.BoundUrl.Port}";
    string token = which == "wrong-token" ? "t0ken-xyz" : "t0ken-abc";

    // One request as an Azure SDK sends it: the REST version, the blob type for a put, the Bearer token.
    async Task<CurlResponse> Send(HttpMethod method, string path, bool put = false, bool withToken = true)
    {
        var headers = new List<KeyValuePair<string, string>>();
        if (which != "version-header") headers.Add(new("x-ms-version", "2023-11-03"));
        if (put && which != "blob-type-header") headers.Add(new("x-ms-blob-type", "BlockBlob"));
        if (withToken && which != "no-token") headers.Add(new("Authorization", $"Bearer {token}"));
        return await client.SendAsync(new CurlRequest(method, endpoint + path)
        {
            Headers = headers,
            Body = put ? CurlContent.FromBytes(note) : null,
        }, CancellationToken.None);
    }

    string Header(CurlResponse response, string name) =>
        response.Headers.FirstOrDefault(h => h.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value ?? "";

    async Task<string> Text(CurlResponse response)
    {
        await using Stream body = response.Body;
        return await new StreamReader(body).ReadToEndAsync();
    }

    switch (which)
    {
        case "get-back":
            await Send(HttpMethod.Put, "/photos/note.txt", put: true);
            CurlResponse got = await Send(HttpMethod.Get, "/photos/note.txt");
            Console.WriteLine($"get: {got.StatusCode} {(await Text(got)).TrimEnd()}");
            break;

        case "md5":
            CurlResponse stored = await Send(HttpMethod.Put, "/photos/note.txt", put: true);
            string mine = Convert.ToBase64String(MD5.HashData(note));
            Console.WriteLine($"put: {stored.StatusCode}");
            Console.WriteLine($"Content-MD5: {Header(stored, "Content-MD5")}");
            Console.WriteLine($"the file's own hash agrees: {Header(stored, "Content-MD5") == mine}");
            break;

        case "listed":
            await Send(HttpMethod.Put, "/photos/note.txt", put: true);
            CurlResponse listing = await Send(HttpMethod.Get, "/photos?restype=container&comp=list");
            string xml = await Text(listing);
            Console.WriteLine($"list: {listing.StatusCode}");
            foreach (string name in new[] { "note.txt", "seed.txt" })
            {
                Console.WriteLine($"{name} listed: {xml.Contains($"<Name>{name}</Name>")}");
            }

            break;

        case "missing-blob":
            CurlResponse absent = await Send(HttpMethod.Get, "/photos/other.txt");
            Console.WriteLine($"get: {absent.StatusCode} {Header(absent, "x-ms-error-code")}");
            break;

        case "uploads-off-reads":
            CurlResponse seed = await Send(HttpMethod.Get, "/photos/seed.txt");
            Console.WriteLine($"get: {seed.StatusCode} {(await Text(seed)).TrimEnd()}");
            break;

        default:
            // put, and every refusal of a put.
            CurlResponse put = await Send(HttpMethod.Put, "/photos/note.txt", put: true);
            string code = Header(put, "x-ms-error-code");
            Console.WriteLine($"put: {put.StatusCode}{(code.Length > 0 ? " " + code : "")}");
            if (which == "put")
            {
                // The test reads what the stand-in stored, with no HTTP in between.
                SurlFileContents file = await server.Files.ReadAsync("/photos/note.txt");
                Console.WriteLine($"stored {file.Bytes.Length} bytes, identical to the upload: {file.Bytes.Span.SequenceEqual(note)}");
            }

            break;
    }
}

return 0;
