// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves a file over FTP with Surl.Api, refusing the first login on purpose, and fetches it with
// Curl.Api, retrying a refused login after a wait. Run: dotnet run retry-login.cs -- [scenario]
//   retry      refuse one login; retry up to 3 times, 1 s apart (the default)
//   permanent  refuse one login; a client that counts a refused login as final, then a second fetch
//   wrong      refuse one login; the client sends a wrong password and retries twice
//   slow       refuse two logins; retry up to 3 times, 2 s apart
//   strict     no rule, but the server was not told to check a password sent in clear
// A second argument, such as "retries", prints only the lines that start with it.
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text;
using Curl.Api;
using Curl.Api.Codes;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string scenario = args.Length > 0 ? args[0] : "retry";

// A second argument, a label, prints only that label's lines, so each case checks one claim.
if (args.Length > 1)
{
    Console.SetOut(new LineFilter(Console.Out, args[1]));
}
int allowed = scenario switch { "permanent" => 0, "wrong" => 2, "strict" => 1, _ => 3 };
TimeSpan wait = TimeSpan.FromSeconds(scenario == "slow" ? 2 : 1);

// The server: one account, one file, and a rule that refuses the first login (two for "slow")
// whatever the password. Its verbose log goes to a StringWriter; its events are recorded.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0" },
    Accounts = { new SurlAccount("app", "s3cret") },
    AllowPlaintextAuthentication = scenario != "strict",
    RecordExchanges = true,
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Verbose,
};
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
if (scenario != "strict")
{
    options.Faults.Add(SurlFault.RefuseLogin() with { Times = scenario == "slow" ? 2 : 1 });
}

await using SurlServer server = await SurlServer.StartAsync(options);
var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt")
{
    Authentication = new CurlAuthenticationOptions { UserName = "app", Password = scenario == "wrong" ? "wrong" : "s3cret" },
};

// The client: a refused login throws CurlException with LoginDenied (67); wait and try again,
// up to `allowed` retries. Any other failure is not this program's to retry.
int retries = 0;
bool done = false;
for (int attempt = 1; !done; attempt++)
{
    try
    {
        await using CurlClient client = CurlClient.Create();
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        Console.Write($"attempt {attempt}: {await new StreamReader(body).ReadToEndAsync()}");
        done = true;
    }
    catch (CurlException failure) when (failure.Code == CurlCode.LoginDenied)
    {
        Console.Write($"attempt {attempt}: {failure.Code} ({(int)failure.Code})");
        if (retries == allowed)
        {
            Console.WriteLine();
            break;
        }

        Console.WriteLine($", waiting {wait.TotalSeconds} s");
        await Task.Delay(wait);
        retries++;
    }
}

Console.WriteLine($"retries: {retries}");
if (scenario == "permanent")
{
    // The rule is spent, so the next fetch logs in.
    await using CurlClient again = CurlClient.Create();
    CurlResponse response = await again.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    Console.Write($"next fetch: {await new StreamReader(body).ReadToEndAsync()}");
}

// What the server judged: each login, accepted or refused, and whether the rule refused it.
await server.StopAsync();
var logins = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>();
Console.WriteLine($"logins: {string.Join(", ", logins.Select(l => l.IsAccepted ? "accepted" : l.IsRefusedByFault ? "refused by the rule" : "refused"))}");
foreach (SurlFaultRule rule in server.Faults.List())
{
    Console.WriteLine($"rule {rule.Id}: {rule.Remaining} left");
}

string text = log.ToString();
Console.WriteLine($"530 sent: {Count(text, "530 Login incorrect")}");
Console.WriteLine($"PASS ****: {Count(text, "PASS ****")}");
return done ? 0 : 67;

static int Count(string text, string what) => text.Split('\n').Count(line => line.Contains(what));

// Shows only the lines that start with one label, and the indented lines under them.
sealed class LineFilter(TextWriter inner, string label) : TextWriter
{
    private readonly StringBuilder line = new();
    private bool keeping;

    public override Encoding Encoding => inner.Encoding;

    public override void Write(char value)
    {
        if (value == '\r')
        {
            return;
        }

        if (value != '\n')
        {
            line.Append(value);
            return;
        }

        string text = line.ToString();
        line.Clear();
        keeping = text.StartsWith(label) || (keeping && text.StartsWith(' '));
        if (keeping)
        {
            inner.Write(text + "\n");
            inner.Flush();
        }
    }
}
