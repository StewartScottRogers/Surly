// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Hosts surl's TELNET server with Surl.Api and holds a line session with it through Curl.Api.
// Run: dotnet run telnet-session.cs [-- case]
//   session    send hello, world and quit, and print the echo (the default)
//   upper      end the session with QUIT in capitals
//   no-quit    send one line and read until the server has gone quiet
//   too-long   send a line of 9000 characters
//   banner     read the banner and the terminal type the client announces
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Surl.Api;
using Surl.Output;

string which = args.Length > 0 ? args[0] : "session";

var log = new StringWriter();
var options = new SurlServerOptions { ListenUrls = { "telnet://127.0.0.1:0" }, Log = log, LogLevel = LogLevel.Verbose };
await using var server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

// The lines to send, as the body of the request: Curl.Api sends a body to a Telnet server as curl -T does.
string[] lines = which switch
{
    "upper" => ["one", "QUIT", "never read"],
    "no-quit" => ["hello"],
    "too-long" => [new string('a', 9000), "never read"],
    "banner" => [],
    _ => ["hello", "world", "quit"],
};
var request = new CurlRequest(HttpMethod.Put, server.BoundUrl.ToString())
{
    Body = Encoding.ASCII.GetBytes(string.Concat(lines.Select(line => line + "\r\n"))),
    Telnet = new CurlTelnetOptions { TelnetOptions = ["TTYPE=vt100"] },
};

// SendAsync returns once the session is open; the response body is what the server sends.
CurlResponse response = await client.SendAsync(request, CancellationToken.None);
await using Stream body = response.Body;
var text = new StringBuilder();
var chunk = new byte[1024];
string state;
while (true)
{
    // Read until the server closes, or until it has said nothing for five seconds: long enough
    // for a close to arrive on a loaded machine, short enough to end a session that stays open.
    using var quiet = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    try
    {
        int read = await body.ReadAsync(chunk, quiet.Token);
        if (read == 0)
        {
            state = "the server closed the connection";
            break;
        }

        text.Append(Encoding.ASCII.GetString(chunk, 0, read));
        if (which == "banner" && text.ToString().Contains('\n'))
        {
            state = "the banner has arrived";
            break;
        }
    }
    catch (OperationCanceledException)
    {
        state = "the server went quiet and the session is still open";
        break;
    }
}

if (which == "banner")
{
    // The first line only; the server's verbose log names the terminal type the client announced.
    Console.WriteLine(text.ToString().Split("\r\n")[0]);
    await Task.Delay(500);
    foreach (string line in log.ToString().Split('\n').Where(l => l.Contains("terminal type")))
    {
        Console.WriteLine(line[(line.IndexOf('*') + 2)..].TrimEnd());
    }

    return 0;
}

Console.Write(text.ToString().Replace("\r\n", "\n"));
Console.WriteLine($"[{state}]");
return 0;
