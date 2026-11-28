// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves a file on IPv4 and IPv6 loopback from one in-process server and fetches it from each address.
// Run: dotnet run fetch-both.cs -- <case>
//   both         listen on 127.0.0.1 and [::1], fetch from each (the default)
//   v6-only      listen on [::1] only, then fetch from 127.0.0.1 at the same port
//   no-brackets  ask for a listen URL with the IPv6 address bare
//   bare-client  fetch from an IPv6 URL written without brackets
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "both";

var options = new SurlServerOptions();
switch (which)
{
    case "v6-only": options.ListenUrls.Add("http://[::1]:0"); break;
    case "no-brackets": options.ListenUrls.Add("http://::1:0"); break;
    default: options.ListenUrls.Add("http://127.0.0.1:0"); options.ListenUrls.Add("http://[::1]:0"); break;
}

options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException refused)
{
    // surl would have refused this start with the same exit code, and nothing is bound.
    Console.WriteLine($"refused: {(int)refused.ExitCode} ({refused.ExitCode})");
    return 1;
}

await using (server)
{
    // BoundUrls holds one Uri per listen URL, in order, with the port the system chose.
    foreach (Uri bound in server.BoundUrls)
    {
        Console.WriteLine($"bound {bound} {bound.HostNameType} host={bound.Host} idn={bound.IdnHost}");
    }

    await using CurlClient client = CurlClient.Create();
    var targets = new List<string>();
    switch (which)
    {
        case "v6-only": targets.Add($"http://127.0.0.1:{server.BoundUrl.Port}/hello.txt"); break;
        case "bare-client": targets.Add($"http://::1:{server.BoundUrl.Port}/hello.txt"); break;
        default: targets.AddRange(server.BoundUrls.Select(bound => $"{bound}hello.txt")); break;
    }

    foreach (string url in targets)
    {
        try
        {
            CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url), CancellationToken.None);
            await using Stream body = response.Body;
            string text = (await new StreamReader(body).ReadToEndAsync()).TrimEnd();
            // Info.PrimaryIp is the address the answer came from.
            Console.WriteLine($"{(int)response.StatusCode} {text} from {response.Info.PrimaryIp}");
        }
        catch (CurlException failure)
        {
            Console.WriteLine($"CurlException {(int)failure.Code} {failure.Code}");
            return 1;
        }
    }
}

return 0;
