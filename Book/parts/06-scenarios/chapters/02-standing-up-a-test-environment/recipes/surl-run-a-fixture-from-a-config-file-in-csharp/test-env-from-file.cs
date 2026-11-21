// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Starts the test environment test-env.conf describes with Surl.Api, tries each server once with
// Curl.Api, and checks what the server recorded.
// Run: dotnet run test-env-from-file.cs [-- start|no-plaintext|<file>|urls-in-file]
//   start         test-env.conf, with the listen URLs in code (the default)
//   no-plaintext  the same, with --no-allow-plaintext-auth added after the file
//   <file>        another config file in place of test-env.conf, such as broken.conf
//   urls-in-file  with-urls.conf, which names its own listen URLs, and none in code
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "start";

// The options live in the file; the listen URLs, all on port 0, live in the code.
var options = new SurlServerOptions();
if (mode != "urls-in-file")
{
    options.ListenUrls.Add("http://127.0.0.1:0");
    options.ListenUrls.Add("ftp://127.0.0.1:0");
    options.ListenUrls.Add("smtp://127.0.0.1:0");
}

options.ConfigFiles.Add(mode switch { "start" or "no-plaintext" => "test-env.conf", "urls-in-file" => "with-urls.conf", _ => mode });
if (mode == "no-plaintext")
{
    options.AdditionalArguments.Add("--no-allow-plaintext-auth");
}

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException refused)
{
    Console.WriteLine($"refused: {refused.ExitCode} ({(int)refused.ExitCode})");
    Console.WriteLine(refused.Lines[0]);
    return (int)refused.ExitCode;
}
catch (ArgumentException misused)
{
    Console.WriteLine($"misused: {misused.Message}");
    return 1;
}

await using (server)
await using (CurlClient client = CurlClient.Create())
{
    if (mode == "urls-in-file")
    {
        // The file's url line is the only listen URL; it names no account, so no login is needed.
        Console.WriteLine($"listening on {server.BoundUrls.Count} URL from the file, {server.BoundUrls[0].Scheme}");
        Console.WriteLine($"HTTP: {await Fetch(client, $"{server.BoundUrls[0]}hello.txt", null)}");
        return 0;
    }

    var tester = new CurlAuthenticationOptions { UserName = "tester", Password = "secret" };
    int before;
    try
    {
        // HTTP and FTP serve one directory, so both find hello.txt.
        Console.WriteLine($"HTTP: {await Fetch(client, $"{server.BoundUrls[0]}hello.txt", tester)}");
        Console.WriteLine($"FTP: {await Fetch(client, $"{server.BoundUrls[1]}hello.txt", tester)}");

        // SMTP takes the app's mail after its login. The directory keeps mail from earlier runs.
        before = (await server.Mail.ListMailboxesAsync("app@example.com")).Single().MessageCount;
        var mail = new CurlRequest(HttpMethod.Put, $"{server.BoundUrls[2]}example.com")
        {
            Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "secret" },
            Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = ["app@example.com"] },
            Body = CurlContent.FromStream(new MemoryStream("Subject: Ready\r\n\r\nThe environment is up.\r\n"u8.ToArray())),
        };
        CurlResponse sent = await client.SendAsync(mail, CancellationToken.None);
        await sent.Body.DisposeAsync();
        Console.WriteLine($"SMTP: {sent.StatusCode}");
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"curl error {(int)failure.Code}: {failure.Message}");
        return (int)failure.Code;
    }

    // What the server recorded: the mailbox, read from the server's side.
    SurlMailboxInfo inbox = (await server.Mail.ListMailboxesAsync("app@example.com")).Single();
    Console.WriteLine($"stored: {inbox.MessageCount - before} more message in {inbox.Name}");
}

return 0;

// One GET: the body, or the status when it is an HTTP error.
static async Task<string> Fetch(CurlClient client, string url, CurlAuthenticationOptions? login)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url) { Authentication = login }, CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();
    return response.StatusCode >= 400 ? $"{response.StatusCode} {response.ReasonPhrase}" : text.TrimEnd();
}
