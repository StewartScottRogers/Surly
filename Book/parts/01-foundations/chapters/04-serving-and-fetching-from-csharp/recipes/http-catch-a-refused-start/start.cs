// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Starts a Surl.Api server that surl would refuse to start, and reports the refusal.
// Run: dotnet run start.cs -- <case>
//   https-no-cert   an https listen URL with no certificate
//   port-in-use     a second server on the port the first one bound
//   unknown-scheme  a listen URL whose scheme Surl has no server for
//   host-option     --version in AdditionalArguments, which is misuse, not a refusal
#:package Surly.Surl.Api@0.1.13

using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "https-no-cert";
await using var first = which == "port-in-use"
    ? await SurlServer.StartAsync(new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } })
    : null;

var options = new SurlServerOptions();
switch (which)
{
    case "https-no-cert":
        options.ListenUrls.Add("https://127.0.0.1:0");
        break;
    case "port-in-use":
        options.ListenUrls.Add(first!.BoundUrl.ToString());
        break;
    case "unknown-scheme":
        options.ListenUrls.Add("nosuch://127.0.0.1:0");
        break;
    case "left-bound":
        // A plain http listener on a fixed port beside the refused https one.
        var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        probe.Start();
        int port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        options.ListenUrls.Add($"http://127.0.0.1:{port}");
        options.ListenUrls.Add("https://127.0.0.1:0");
        try
        {
            await SurlServer.StartAsync(options);
        }
        catch (SurlServerStartException refused)
        {
            Console.WriteLine($"refused: {refused.ExitCode} ({(int)refused.ExitCode})");
        }

        // The refused start left nothing bound, so the same port starts at once.
        var again = new SurlServerOptions { ListenUrls = { $"http://127.0.0.1:{port}" } };
        await using (await SurlServer.StartAsync(again))
        {
            Console.WriteLine("the http port was free again");
        }

        return 0;
    case "host-option":
        options.ListenUrls.Add("http://127.0.0.1:0");
        options.AdditionalArguments.Add("--version");
        break;
    default:
        Console.Error.WriteLine($"unknown case {which}");
        return 2;
}

try
{
    await using var server = await SurlServer.StartAsync(options);
    Console.WriteLine($"started on {server.BoundUrl}");
    return 0;
}
catch (SurlServerStartException refused)
{
    // The exit code surl would have exited with, and the lines it would have written after "surl: ".
    Console.WriteLine($"refused: {refused.ExitCode} ({(int)refused.ExitCode})");
    foreach (string line in refused.Lines)
    {
        Console.WriteLine($"surl: {line}");
    }

    return (int)refused.ExitCode;
}
catch (ArgumentException misuse)
{
    // Not a start surl would refuse, but a call the library refuses: a bug in the caller.
    Console.WriteLine($"misuse: {misuse.Message}");
    return 100;
}
