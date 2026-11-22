// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# file-based app: `dotnet run sign.cs -- SCENARIO`.
//   fetch (the default), wrong, unknown, unsigned, password, scope, missing, events, default, default-wrong
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Protocol.Abstractions;

string scenario = args.Length > 0 ? args[0] : "fetch";

// One account, an access key ID and its secret key. SigV4 alone, unless the scenario starts "default".
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
options.Accounts.Add(new SurlAccount("AKIAEXAMPLE", "topsecret"));
if (!scenario.StartsWith("default"))
    options.AuthenticationMethods = new List<string> { "aws-sigv4" };
await using var server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

string lastBody = "";

// One request, signed or logged in as given; the answer is the status line.
async Task<CurlResponse> Send(string path, CurlAuthenticationOptions? login)
{
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}") { Authentication = login };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    lastBody = await new StreamReader(body).ReadToEndAsync();
    return response;
}
static CurlAuthenticationOptions Signed(string key, string secret, string scope = "aws:amz:us-east-1:s3") =>
    new() { UserName = key, Password = secret, AwsSigV4 = scope };
static string Line(CurlResponse r) => $"{(int)r.StatusCode} {r.ReasonPhrase}";

switch (scenario)
{
    case "fetch":
        CurlResponse ok = await Send("hello.txt", Signed("AKIAEXAMPLE", "topsecret"));
        Console.WriteLine(Line(ok));
        Console.Write(lastBody);
        break;
    case "wrong":
        Console.WriteLine(Line(await Send("hello.txt", Signed("AKIAEXAMPLE", "wrong"))));
        break;
    case "unknown":
        Console.WriteLine(Line(await Send("hello.txt", Signed("NOBODY", "topsecret"))));
        break;
    case "unsigned":
        CurlResponse bare = await Send("hello.txt", null);
        Console.WriteLine($"{Line(bare)}; challenges: {bare.Headers.Count(h => h.Key.Equals("WWW-Authenticate", StringComparison.OrdinalIgnoreCase))}");
        break;
    case "password":
        foreach (var method in new[] { HttpAuthenticationMethods.Basic, HttpAuthenticationMethods.Digest })
        {
            var login = new CurlAuthenticationOptions { UserName = "AKIAEXAMPLE", Password = "topsecret", HttpAuthentication = method };
            Console.WriteLine($"{method}: {Line(await Send("hello.txt", login))}");
        }
        break;
    case "scope":
        Console.WriteLine(Line(await Send("hello.txt", Signed("AKIAEXAMPLE", "topsecret", "aws:amz:eu-west-1:ec2"))));
        break;
    case "missing":
        Console.WriteLine(Line(await Send("missing.txt", Signed("AKIAEXAMPLE", "topsecret"))));
        break;
    case "events":
        await Send("hello.txt", Signed("AKIAEXAMPLE", "topsecret"));
        await Send("hello.txt", Signed("AKIAEXAMPLE", "wrong"));
        foreach (var e in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>())
            Console.WriteLine($"{e.Mechanism} {e.Account}: {(e.IsAccepted ? "accepted" : "refused")}");
        break;
    case "default":
        Console.WriteLine(Line(await Send("hello.txt", Signed("AKIAEXAMPLE", "topsecret"))));
        break;
    case "default-wrong":
        CurlResponse refused = await Send("hello.txt", Signed("AKIAEXAMPLE", "wrong"));
        Console.WriteLine($"{Line(refused)}; Digest challenge: {refused.Headers.Any(h => h.Key.Equals("WWW-Authenticate", StringComparison.OrdinalIgnoreCase) && h.Value.StartsWith("Digest"))}");
        break;
}
return 0;
