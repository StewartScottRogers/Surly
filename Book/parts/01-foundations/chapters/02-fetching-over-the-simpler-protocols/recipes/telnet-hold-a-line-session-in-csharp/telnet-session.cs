// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Hosts surl's TELNET server with Surl.Api, holds a line session with it, and reads its banner with Curl.Api.
// Run: dotnet run telnet-session.cs [-- case]
//   session    send hello, world and quit, and print the echo (the default)
//   upper      end the session with QUIT in capitals
//   no-quit    send one line and read until the server has gone quiet
//   too-long   send a line of 9000 characters
//   banner     read the banner with Curl.Api and the terminal type it announces
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using System.Net.Sockets;
using System.Text;
using Curl.Api;
using Surl.Api;
using Surl.Output;

string which = args.Length > 0 ? args[0] : "session";

var log = new StringWriter();
var options = new SurlServerOptions { ListenUrls = { "telnet://127.0.0.1:0" }, Log = log, LogLevel = LogLevel.Verbose };
await using var server = await SurlServer.StartAsync(options);

if (which == "banner")
{
    // Curl.Api opens the session and delivers what the server sends; the program stops after the first line.
    await using CurlClient client = CurlClient.Create();
    var request = new CurlRequest(HttpMethod.Get, server.BoundUrl.ToString())
    {
        Telnet = new CurlTelnetOptions { TelnetOptions = ["TTYPE=vt100"] },
    };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    string text = "";
    var buffer = new byte[256];
    while (!text.Contains('\n'))
    {
        int read = await body.ReadAsync(buffer);
        text += Encoding.ASCII.GetString(buffer, 0, read);
    }

    Console.Write(text);
    await Task.Delay(500);
    foreach (string line in log.ToString().Split('\n').Where(l => l.Contains("terminal type")))
    {
        Console.WriteLine(line[(line.IndexOf('*') + 2)..].TrimEnd());
    }

    return 0;
}

// A line session by hand: Curl.Api cannot send a line to a TELNET server, so the lines go over a socket.
using var tcp = new TcpClient();
await tcp.ConnectAsync(server.BoundUrl.Host, server.BoundUrl.Port);
var stream = tcp.GetStream();
string[] lines = which switch
{
    "upper" => ["one", "QUIT", "never read"],
    "no-quit" => ["hello"],
    "too-long" => [new string('a', 9000), "never read"],
    _ => ["hello", "world", "quit"],
};
foreach (string line in lines)
{
    await stream.WriteAsync(Encoding.ASCII.GetBytes(line + "\r\n"));
}

// Read until the server closes, or until it has said nothing for a second.
var raw = new List<byte>();
string state;
var chunk = new byte[1024];
while (true)
{
    using var quiet = new CancellationTokenSource(TimeSpan.FromSeconds(1));
    try
    {
        int read = await stream.ReadAsync(chunk, quiet.Token);
        if (read == 0)
        {
            state = "the server closed the connection";
            break;
        }

        raw.AddRange(chunk.Take(read));
    }
    catch (OperationCanceledException)
    {
        state = "the server went quiet and the session is still open";
        break;
    }
}

// Telnet option negotiation is IAC (255) and two more bytes; the rest is the text.
var echoed = new List<byte>();
for (int i = 0; i < raw.Count; i++)
{
    if (raw[i] == 255 && i + 2 < raw.Count)
    {
        i += 2;
        continue;
    }

    echoed.Add(raw[i]);
}

Console.Write(Encoding.ASCII.GetString(echoed.ToArray()).Replace("\r\n", "\n"));
Console.WriteLine($"[{state}]");
return 0;
