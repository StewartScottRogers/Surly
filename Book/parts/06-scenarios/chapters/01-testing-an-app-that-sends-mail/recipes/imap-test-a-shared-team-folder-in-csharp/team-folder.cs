// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a shared team folder: an in-process Surl server shares Shared/team between ann and bob;
// ann appends a notice, both read it with Curl.Api, bob flags it, and ann sees the flag.
// Run: dotnet run team-folder.cs [-- <case>]
//   (no argument)  the test: append as ann, read as both, flag as bob, search as ann
//   non-member     carol, who is not a member, tries to read the folder
//   deliver        the test seeds mail for team@example.com through Surl.Api
//   curl-store     the flag is asked of Curl.Api, as a request method
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "test";

// SMTP and IMAP over one mail store for example.com; ann and bob share the folder team.
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
foreach (string who in new[] { "ann", "bob", "carol" })
{
    options.Mailboxes.Add($"{who}@example.com:{who}pass");
}
options.SharedFolders.Add("team=ann@example.com,bob@example.com");
await using var server = await SurlServer.StartAsync(options);
Uri imap = server.BoundUrls[1];
byte[] notice = "From: app@example.com\r\nTo: team@example.com\r\nSubject: Server maintenance tonight\r\n\r\nThe build server is down from 22:00.\r\n"u8.ToArray();
await using CurlClient client = CurlClient.Create();

switch (mode)
{
    case "deliver":
        // The folder has no address, so the store will not deliver to one.
        try
        {
            await server.Mail.DeliverAsync("team@example.com", notice);
        }
        catch (SurlSeedRefusedException refused)
        {
            Console.WriteLine($"not delivered: {refused.Message}");
        }

        return 1;
    case "curl-store":
        try
        {
            _ = new CurlRequest(new HttpMethod(@"STORE 1 +FLAGS (\Flagged)"), $"{imap}Shared/team");
        }
        catch (FormatException refused)
        {
            Console.WriteLine($"{refused.GetType().Name}: {refused.Message}");
        }

        return 1;
}

// Ann puts the notice in the folder: a PUT with a body of known length is an IMAP APPEND, which answers no body.
string appended = await Fetch("ann", "Shared/team", notice);
Console.WriteLine($"ann appends: {(appended == "" ? "done" : appended)}");

if (mode == "non-member")
{
    Console.WriteLine($"carol reads: {await Fetch("carol", "Shared/team;UID=1")}");
    foreach (string owner in new[] { "ann@example.com", "carol@example.com" })
    {
        var names = (await server.Mail.ListMailboxesAsync(owner)).Select(box => box.Name);
        Console.WriteLine($"{owner} has: {string.Join(", ", names)}");
    }

    return 1;
}

// Both members read it with Curl.Api.
foreach (string who in new[] { "bob", "ann" })
{
    Console.WriteLine($"{who} reads: {await Fetch(who, "Shared/team;UID=1;SECTION=HEADER.FIELDS%20(SUBJECT)")}");
}

// Bob flags it; Curl.Api cannot send STORE, so the test speaks IMAP for it.
Console.WriteLine($"bob flags: {await Imap.SendAsync(imap, "bob", @"STORE 1 +FLAGS (\Flagged)")}");

// Ann sees the flag, over IMAP and in the store.
string found = await Fetch("ann", "Shared/team?FLAGGED");
Console.WriteLine($"ann searches: {found}");
var listed = await server.Mail.ListMessagesAsync("ann@example.com", "Shared/team");
Console.WriteLine($"store: UID {listed.Messages[0].Uid} {string.Join(" ", listed.Messages[0].Flags)}");
return found == "* SEARCH 1" ? 0 : 1;

// One Curl.Api request as a member: a GET, or with a body a PUT. Answers the body's first line, or the error.
async Task<string> Fetch(string who, string path, byte[]? body = null)
{
    var request = new CurlRequest(body is null ? HttpMethod.Get : HttpMethod.Put, $"{imap}{path}")
    {
        Authentication = new CurlAuthenticationOptions { UserName = $"{who}@example.com", Password = $"{who}pass" },
        Body = body is null ? null : CurlContent.FromStream(new MemoryStream(body), body.Length),
    };
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream stream = response.Body;
        return (await new StreamReader(stream).ReadToEndAsync()).Split("\r\n")[0];
    }
    catch (CurlException failure)
    {
        return $"curl error {(int)failure.Code}: {failure.Message}";
    }
}

// One IMAP command as a member, on a connection of its own: log in with CRAM-MD5, select the
// folder, send the command, and answer the reply that matters, the untagged FETCH if there is
// one, else the tagged OK or NO.
static class Imap
{
    public static async Task<string> SendAsync(Uri imap, string who, string command)
    {
        using var tcp = new TcpClient(imap.Host, imap.Port);
        using var reader = new StreamReader(tcp.GetStream(), Encoding.ASCII);
        using var writer = new StreamWriter(tcp.GetStream(), Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
        await reader.ReadLineAsync();
        await writer.WriteLineAsync("a1 AUTHENTICATE CRAM-MD5");
        byte[] challenge = Convert.FromBase64String((await reader.ReadLineAsync())![2..]);
        string digest = Convert.ToHexStringLower(HMACMD5.HashData(Encoding.ASCII.GetBytes($"{who}pass"), challenge));
        await writer.WriteLineAsync(Convert.ToBase64String(Encoding.ASCII.GetBytes($"{who}@example.com {digest}")));
        await Until(reader, "a1 ");
        await writer.WriteLineAsync("a2 SELECT Shared/team");
        await Until(reader, "a2 ");
        await writer.WriteLineAsync($"a3 {command}");

        List<string> lines = await Until(reader, "a3 ");
        return lines.FirstOrDefault(line => line.Contains(" FETCH ")) ?? lines[^1];
    }

    private static async Task<List<string>> Until(StreamReader reader, string tag)
    {
        var lines = new List<string>();
        while (await reader.ReadLineAsync() is { } line)
        {
            lines.Add(line);
            if (line.StartsWith(tag, StringComparison.Ordinal))
            {
                break;
            }
        }

        return lines;
    }
}
