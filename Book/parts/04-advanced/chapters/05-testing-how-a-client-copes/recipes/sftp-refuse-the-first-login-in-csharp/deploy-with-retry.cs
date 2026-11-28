// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An in-process SFTP server that refuses the first login attempts, and an upload that retries a
// refused login once. Prints each try, what the server stored and every login it judged.
// Run: dotnet run deploy-with-retry.cs [-- retry|no-retry|one-attempt|wrong-password]
//   retry          refuse two attempts; the client retries a refused login once (the default)
//   no-retry       the same server; the client tries once
//   one-attempt    refuse one attempt; the client tries once
//   wrong-password refuse two attempts; the client retries once with a wrong password
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using Curl.Api;
using Curl.Api.Codes;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Tls;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "retry";
int refusals = mode == "one-attempt" ? 1 : 2;

// surl --throwaway-hostkey -u alice:s3cret --allow-uploads --fault "refuse-login;times=N" sftp://127.0.0.1:0
var options = new SurlServerOptions
{
    ListenUrls = { "sftp://127.0.0.1:0" },
    ThrowawayHostKey = true,
    Accounts = { new SurlAccount("alice", "s3cret") },
    AllowUploads = true,
    RecordExchanges = true,
};
options.Faults.Add(SurlFault.RefuseLogin() with { Times = refusals });
await using var server = await SurlServer.StartAsync(options);

// curl -k -u alice:s3cret -T release.txt sftp://.../release.txt
await using CurlClient client = CurlClient.Create();
var upload = new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}release.txt")
{
    Body = "build 42\n"u8.ToArray(),
    Authentication = new CurlAuthenticationOptions { UserName = "alice", Password = mode == "wrong-password" ? "wrong" : "s3cret" },
    Tls = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { VerifyPeer = false } },
};

// The deploy step: one retry of a refused login, after a second; any other failure is final.
int maxRetries = mode is "retry" or "wrong-password" ? 1 : 0;
CurlCode result = CurlCode.Ok;
for (int attempt = 1; ; attempt++)
{
    try
    {
        CurlResponse response = await client.SendAsync(upload, CancellationToken.None);
        await using (Stream body = response.Body)
        {
            await body.CopyToAsync(Stream.Null);
        }

        Console.WriteLine($"try {attempt}: uploaded");
        result = CurlCode.Ok;
        break;
    }
    catch (CurlException e) when (e.Code == CurlCode.LoginDenied)
    {
        Console.WriteLine($"try {attempt}: {e.Code}");
        result = e.Code;
        if (attempt > maxRetries)
        {
            break;
        }

        await Task.Delay(TimeSpan.FromSeconds(1));
    }
}

// What the server holds, every login it judged, in order, and what is left of the rule.
try
{
    SurlFileContents stored = await server.Files.ReadAsync("/release.txt");
    Console.Write($"stored: {System.Text.Encoding.UTF8.GetString(stored.Bytes.Span)}");
}
catch (SurlNotFoundException)
{
    Console.WriteLine("stored: nothing");
}

foreach (LoginJudgedEvent login in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>())
{
    string verdict = login.IsAccepted ? "accepted" : login.IsRefusedByFault ? "refused by the rule" : "refused";
    Console.WriteLine($"login {login.Mechanism} {login.Account}: {verdict}");
}

Console.WriteLine($"rule refusals left: {server.Faults.List().Single().Remaining}");
return (int)result;
