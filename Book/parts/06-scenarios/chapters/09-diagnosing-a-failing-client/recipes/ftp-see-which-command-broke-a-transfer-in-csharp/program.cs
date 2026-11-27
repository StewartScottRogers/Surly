// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An FTP job that fails now and then runs against an in-process Surl.Api server whose fault rules answer its first
// upload 451 and drop its first download. The test reads the server's record and prints, for each session that
// ended on a failure, the job's last command and the reply that ended it. Run: dotnet run program.cs -- <case>
//   runs      what each of three runs of the job got back
//   failed    the failed sessions, from the recorded exchanges, with the fault rule that acted in each
//   passing   how many sessions sent STOR nightly.csv
//   real      a server with no fault rule and no AllowUploads: one run, then the failed sessions
//   locked    a server with no anonymous login: one run
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args[0];
bool faulty = which is "runs" or "failed" or "passing";

// The stand-in: uploads taken, two fault rules spent once each, and the record kept.
var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0" },
    AllowAnonymous = which != "locked",
    AllowUploads = which != "real",
    RecordExchanges = true,
};
options.Files["outbox/README.txt"] = "Uploads land here.\n"u8.ToArray();
options.Files["inbox/orders.csv"] = "order,qty\n7,3\n"u8.ToArray();
if (faulty)
{
    options.Faults.Add(SurlFault.ProtocolError("451 Local error in processing") with { Scheme = "ftp", Command = "STOR", Path = "/outbox", Times = 1 });
    options.Faults.Add(SurlFault.DropConnection() with { Stage = FaultStage.Command, Command = "RETR", Path = "/inbox", Times = 1 });
}

await using SurlServer server = await SurlServer.StartAsync(options);

// The job runs three times on the faulty server, once on the others.
for (int run = 1; run <= (faulty ? 3 : 1); run++)
{
    string result = await Job();
    if (which is "runs" or "real" or "locked") Console.WriteLine($"run {run}: {result}");
}

await Settled();

switch (which)
{
    case "failed":
    case "real":
        return Failed();
    case "passing":
        int sent = server.Exchanges.Count(x => Commands(x).Contains("STOR nightly.csv"));
        Console.WriteLine($"{sent} sessions sent STOR nightly.csv");
        break;
}

return 0;

// The FTP job: uploads nightly.csv to outbox/, then fetches inbox/orders.csv. A failed step ends the run.
async Task<string> Job()
{
    try
    {
        await using (CurlClient client = CurlClient.Create())
        {
            var put = new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}outbox/nightly.csv") { Body = CurlContent.FromBytes("id,total\n1,42\n"u8.ToArray()) };
            await client.SendAsync(put, CancellationToken.None);
        }

        await using (CurlClient client = CurlClient.Create())
        {
            CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}inbox/orders.csv"), CancellationToken.None);
            await using Stream body = response.Body;
            await body.CopyToAsync(Stream.Null);
        }

        return "passed";
    }
    catch (CurlException failure)
    {
        return $"curl code {(int)failure.Code} {failure.Code}";
    }
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

// Each session that did not end on a 2xx or 3xx reply to its last command, and the fault rule that acted in it.
int Failed()
{
    var faults = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).Cast<FaultAppliedEvent>().ToList();
    bool any = false;
    foreach (SurlExchangeRecord exchange in server.Exchanges)
    {
        (string? command, string? reply) = LastWords(exchange);
        if (command is null || reply is ['2' or '3', ..]) continue;
        any = true;
        Console.WriteLine($"exchange {exchange.ExchangeId}: {command} -> {reply ?? "no reply"}");
        foreach (FaultAppliedEvent fault in faults.Where(f => f.ExchangeId == exchange.ExchangeId))
        {
            Console.WriteLine($"  fault {fault.RuleId}: {fault.Kind} at {fault.Stage} stage, on {fault.Target}");
        }
    }

    if (!any) Console.WriteLine("no failed session in the record");
    return any ? 0 : 1;
}

// The last command a session sent before QUIT, and the last reply to it, if any. The chunks are in the order they
// crossed; a file's bytes cross between a 1xx reply and the next reply, and are not commands.
(string? Command, string? Reply) LastWords(SurlExchangeRecord exchange)
{
    string? command = null, reply = null;
    bool inData = false;
    foreach (SurlExchangeChunk chunk in exchange.Chunks)
    {
        string text = Encoding.ASCII.GetString(chunk.Bytes.Span);
        if (chunk.Direction == SurlExchangeDirection.Sent && Regex.IsMatch(text, @"^[1-5]\d\d "))
        {
            if (command is not null) reply = text.TrimEnd();
            inData = text[0] == '1';
        }
        else if (chunk.Direction == SurlExchangeDirection.Received && !inData)
        {
            foreach (string line in text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
            {
                if (line == "QUIT") return (command, reply);
                (command, reply) = (line, null);
            }
        }
    }

    return (command, reply);
}

// The commands a session sent, a file's bytes left out.
List<string> Commands(SurlExchangeRecord exchange)
{
    var commands = new List<string>();
    bool inData = false;
    foreach (SurlExchangeChunk chunk in exchange.Chunks)
    {
        string text = Encoding.ASCII.GetString(chunk.Bytes.Span);
        if (chunk.Direction == SurlExchangeDirection.Sent && Regex.IsMatch(text, @"^[1-5]\d\d ")) inData = text[0] == '1';
        else if (chunk.Direction == SurlExchangeDirection.Received && !inData) commands.AddRange(text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries));
    }

    return commands;
}
