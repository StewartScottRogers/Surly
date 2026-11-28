// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Two accounts upload export.csv to an in-process Surl.Api FTP server: alice the right file, bob a wrong one, then
// alice one past the size limit. The test finds who stored the file from the server's record, by account and path.
// Run: dotnet run program.cs -- <case>
//   who             who stored /export.csv, and who stored it last
//   uploads         what each upload got back
//   file            what /export.csv holds now
//   refused         alice's exchanges with a STOR, against her store changes
//   log             the PASS lines in the server's verbose log
//   record          the PASS lines in the recorded exchanges
//   recording-off   the same uploads with RecordExchanges off: the record
//   plaintext-off   the same uploads with AllowPlaintextAuthentication off
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string which = args[0];
const string Path = "/export.csv";
string[] accounts = ["alice", "bob"];

// The stand-in: two accounts, passwords taken over plain FTP, uploads up to 100 bytes, and the record kept.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0" },
    Accounts = { new SurlAccount("alice", "north-7"), new SurlAccount("bob", "south-9") },
    AllowPlaintextAuthentication = which != "plaintext-off",
    AllowUploads = true,
    MaxFileSize = 100,
    RecordExchanges = which != "recording-off",
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Verbose,
};
await using SurlServer server = await SurlServer.StartAsync(options);

// The uploads: one client, so one connection and one exchange, each.
var results = new List<string>
{
    await Upload("alice", "north-7", "id,total\n1,42\n"),
    await Upload("bob", "south-9", "id,total\n1,0\n"),
    await Upload("alice", "north-7", string.Concat(Enumerable.Range(1, 20).Select(i => $"{i},42\n"))),
};

try
{
    await Settled();
}
catch (InvalidOperationException)
{
    Console.WriteLine("RecordExchanges is off: the record throws InvalidOperationException");
    return 1;
}

switch (which)
{
    case "who":
        return Who();
    case "uploads":
    case "plaintext-off":
        results.ForEach(Console.WriteLine);
        break;
    case "file":
        Console.Write(Encoding.UTF8.GetString((await server.Files.ReadAsync(Path)).Bytes.Span));
        break;
    case "refused":
        int asked = server.FindExchanges(new SurlEventQuery { Account = "alice" })
            .Count(x => Encoding.ASCII.GetString(x.Received).Contains("STOR export.csv\r\n"));
        int stored = server.FindEvents(new SurlEventQuery { Account = "alice", Store = StoreKind.Content, Name = Path }).Count;
        Console.WriteLine($"alice: {asked} exchanges with STOR export.csv, {stored} store change");
        break;
    case "log":
        // The log line for each exchange is written as it goes; stopping the server ends them all.
        await server.StopAsync();
        PassLines(log.ToString());
        break;
    case "record":
        PassLines(string.Concat(server.Exchanges.Select(x => Encoding.ASCII.GetString(x.Received))));
        break;
}

return 0;

// The finding: the store changes to the path in the exchanges that judged a login for each account, oldest first.
int Who()
{
    var changes = accounts
        .SelectMany(account => server.FindEvents(new SurlEventQuery { Account = account, Store = StoreKind.Content, Name = Path })
            .Select(change => (Account: account, Change: change)))
        .OrderBy(found => found.Change.Sequence)
        .ToList();
    if (changes.Count == 0)
    {
        Console.WriteLine($"no store change to {Path} in the record");
        return 1;
    }

    foreach (var (account, change) in changes)
    {
        Console.WriteLine($"exchange {change.ExchangeId}: {account} stored {Path}");
    }

    Console.WriteLine($"last stored by {changes[^1].Account}");
    return 0;
}

// The record is kept as exchanges run, so wait, with a bound, until every exchange that started has ended.
async Task Settled()
{
    for (int tries = 0; tries < 50; tries++)
    {
        int started = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.ExchangeStarted }).Count;
        int ended = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded }).Count;
        if (started == ended) return;
        await Task.Delay(200);
    }
}

async Task<string> Upload(string account, string password, string content)
{
    await using CurlClient client = CurlClient.Create();
    var put = new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}export.csv")
    {
        Body = CurlContent.FromBytes(Encoding.UTF8.GetBytes(content)),
        Authentication = new CurlAuthenticationOptions { UserName = account, Password = password },
    };
    try
    {
        await client.SendAsync(put, CancellationToken.None);
        return $"{account}: stored";
    }
    catch (CurlException failure)
    {
        return $"{account}: refused, curl code {(int)failure.Code} {failure.Code}";
    }
}

// The lines of a text that carry a PASS command, as they stand.
void PassLines(string text)
{
    foreach (string line in text.Split('\n').Where(l => l.Contains("PASS ")))
    {
        Console.WriteLine(line.Trim());
    }
}
