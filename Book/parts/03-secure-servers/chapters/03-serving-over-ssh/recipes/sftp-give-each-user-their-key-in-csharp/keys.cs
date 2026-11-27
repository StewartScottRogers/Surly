// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# file-based app: `dotnet run keys.cs -- SCENARIO`. The keys come from setup: in recipe.yml:
// host_key, deploy_a, deploy_a2 and deploy_b, each with a .pub half, and a_keys, two lines.
//   own-key (the default), second-user, a-key-as-b, b-key-as-a, second-key, second-key-as-b, unknown-user, events
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Protocols;
using Surl.Api;
using Surl.Protocol.Abstractions;

string scenario = args.Length > 0 ? args[0] : "own-key";

// One server, two deploy users: deploy-a has the two keys in a_keys, deploy-b the one in deploy_b.pub.
var options = new SurlServerOptions { ListenUrls = { "sftp://127.0.0.1:0" }, RecordExchanges = true };
options.HostKeyFiles.Add("host_key");
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
foreach (string word in new[] { "--authorized-keys", "deploy-a:a_keys", "--authorized-keys", "deploy-b:deploy_b.pub" })
    options.AdditionalArguments.Add(word);
await using var server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

// A login as user with the key in file `key`: the file's contents, or the code that refused it.
async Task<string> Fetch(string user, string key)
{
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl.ToString().Replace("sftp://", $"sftp://{user}@")}hello.txt")
    {
        Ssh = new CurlSshOptions
        {
            PrivateKeyFile = key, PublicKeyFile = key + ".pub", AuthenticationTypes = SshAuthenticationTypes.PublicKey,
        },
        Tls = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { VerifyPeer = false } },
    };
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        return (await new StreamReader(body).ReadToEndAsync()).TrimEnd();
    }
    catch (CurlException e)
    {
        return $"refused, {e.Code} ({(int)e.Code})";
    }
}

switch (scenario)
{
    case "own-key": Console.WriteLine($"deploy-a, own key: {await Fetch("deploy-a", "deploy_a")}"); break;
    case "second-user": Console.WriteLine($"deploy-b, own key: {await Fetch("deploy-b", "deploy_b")}"); break;
    case "a-key-as-b": Console.WriteLine($"deploy-b, deploy-a's key: {await Fetch("deploy-b", "deploy_a")}"); break;
    case "b-key-as-a": Console.WriteLine($"deploy-a, deploy-b's key: {await Fetch("deploy-a", "deploy_b")}"); break;
    case "second-key": Console.WriteLine($"deploy-a, second key: {await Fetch("deploy-a", "deploy_a2")}"); break;
    case "second-key-as-b": Console.WriteLine($"deploy-b, deploy-a's second key: {await Fetch("deploy-b", "deploy_a2")}"); break;
    case "unknown-user": Console.WriteLine($"deploy-c, deploy-a's key: {await Fetch("deploy-c", "deploy_a")}"); break;
    case "events":
        await Fetch("deploy-a", "deploy_a");
        await Fetch("deploy-b", "deploy_a");
        // A key the server refuses is only offered, never used to sign, so it leaves no login_judged event.
        var judged = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>().ToList();
        foreach (string user in new[] { "deploy-a", "deploy-b" })
        {
            var mine = judged.Where(e => e.Account == user).ToList();
            string accepted = string.Join(", ", mine.Where(e => e.IsAccepted).Select(e => e.Mechanism));
            Console.WriteLine($"{user}: accepted by [{accepted}], refused logins: {mine.Count(e => !e.IsAccepted) > 0}");
        }
        break;
}
return 0;
