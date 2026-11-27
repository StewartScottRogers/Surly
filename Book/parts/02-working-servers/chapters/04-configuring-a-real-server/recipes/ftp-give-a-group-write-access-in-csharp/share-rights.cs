// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of per-account rights on a share: the editors group may write the share team, the
// readers group may only read it. The test uploads as an editor and as a reader over FTP and
// passes when the first lands in the folder and the second is refused.
// Run: dotnet run share-rights.cs [-- <case>]
//   (no argument)  an editor's upload and a reader's, then a read by the reader
//   http           the reader's read and upload over HTTP
//   outsider       an account in neither group
//   anonymous      a second server with AllowAnonymous: no login, then Ed's login
//   plaintext      a server without AllowPlaintextAuthentication: Ed's login
//   bare           a grant that names the group without its @
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "";

// The shared folder, made fresh for the test, with one file in it.
string team = Directory.CreateTempSubdirectory("team-").FullName;
File.WriteAllText(Path.Combine(team, "plan.txt"), "plan v1\n");

// Four accounts, two groups, one share with a grant for each group; FTP and HTTP on free ports.
var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0", "http://127.0.0.1:0" },
    Accounts = { new SurlAccount("ed", "edpass"), new SurlAccount("erin", "erinpass"), new SurlAccount("rita", "ritapass"), new SurlAccount("gus", "guspass") },
    Groups = { "editors=ed,erin", "readers=rita" },
    Shares = { "team=" + team },
    ShareAccess = { mode == "bare" ? "team=editors:rw" : "team=@editors:rw,@readers:r" },
    AllowUploads = true,
    AllowPlaintextAuthentication = mode != "plaintext",
    AllowAnonymous = mode == "anonymous",
};

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException refused)
{
    Console.WriteLine($"refused: {refused.ExitCode} {refused.Lines[0]}");
    return 2;
}

await using (server)
{
    string ftp = $"{server.BoundUrls[0]}team/";
    string http = $"{server.BoundUrls[1]}team/";
    await using CurlClient client = CurlClient.Create();

    switch (mode)
    {
        case "":
            Console.WriteLine($"ed uploads draft.txt: {await Send(ftp + "draft.txt", "ed", "draft from ed\n")}");
            Console.WriteLine($"rita uploads late.txt: {await Send(ftp + "late.txt", "rita", "a reader's edit\n")}");
            Console.WriteLine($"rita reads plan.txt: {await Send(ftp + "plan.txt", "rita", null)}");
            string[] onDisk = Directory.GetFiles(team).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!;
            Console.WriteLine($"in the folder: {string.Join(", ", onDisk)}");
            bool pass = File.Exists(Path.Combine(team, "draft.txt")) && !File.Exists(Path.Combine(team, "late.txt"));
            Console.WriteLine(pass ? "PASS: editors write, readers only read" : "FAIL");
            return pass ? 0 : 1;
        case "http":
            Console.WriteLine($"rita GET plan.txt: {await Send(http + "plan.txt", "rita", null)}");
            Console.WriteLine($"rita PUT late.txt: {await Send(http + "late.txt", "rita", "a reader's edit\n")}");
            break;
        case "outsider":
            Console.WriteLine($"gus reads plan.txt: {await Send(ftp + "plan.txt", "gus", null)}");
            break;
        case "anonymous":
            Console.WriteLine($"no login reads plan.txt: {await Send(ftp + "plan.txt", null, null)}");
            Console.WriteLine($"ed reads plan.txt: {await Send(ftp + "plan.txt", "ed", null)}");
            break;
        case "plaintext":
            Console.WriteLine($"ed reads plan.txt: {await Send(ftp + "plan.txt", "ed", null)}");
            break;
    }

    return 0;

    // One request: a GET, or a PUT of text; with a user, that account logs in. Answers what a GET
    // read or the status of a PUT, or the CurlException's code.
    async Task<string> Send(string url, string? user, string? text)
    {
        var request = new CurlRequest(text is null ? HttpMethod.Get : HttpMethod.Put, url)
        {
            Authentication = user is null ? null : new() { UserName = user, Password = user + "pass" },
            Body = text is null ? null : CurlContent.FromBytes(Encoding.ASCII.GetBytes(text)),
        };
        try
        {
            CurlResponse response = await client.SendAsync(request, CancellationToken.None);
            await using Stream body = response.Body;
            string got = await new StreamReader(body).ReadToEndAsync();
            return text is null ? got.TrimEnd() : $"{response.StatusCode}";
        }
        catch (CurlException failure)
        {
            return $"refused, {failure.Code} ({(int)failure.Code})";
        }
    }
}
