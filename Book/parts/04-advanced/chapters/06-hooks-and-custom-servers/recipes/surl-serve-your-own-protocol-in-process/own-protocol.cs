// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An in-house protocol served by Surl.Api: a small line echo server, plugged in as the scheme echo://,
// gets Surl's listeners, limits, events, recording and faults. A TcpClient plays the app.
// Run: dotnet run own-protocol.cs [-- <case>]
//   (no argument)  one session: a line echoed, QUIT answered BYE; then what the server recorded
//   events         the same session, then its exchange_ended event
//   fault          a drop rule, once, on the echo listener: two sessions
//   limit          --max-line 16, and a line longer than that
//   idle           IdleTimeout of one second, and a client silent for longer
//   taken          the custom server registered as http, a scheme Surl serves
#:package Surly.Surl.Api@*

using System.Net.Sockets;
using System.Text;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "session";

// The echo server on a free port, beside nothing else, with every exchange recorded.
var options = new SurlServerOptions { ListenUrls = { "echo://127.0.0.1:0" }, RecordExchanges = true };
options.ProtocolServers.Add(new SurlProtocolServer(mode == "taken" ? "http" : "echo", services => new EchoServer()));
if (mode == "fault")
{
    options.Faults.Add(SurlFault.DropConnection() with { Listener = "echo://127.0.0.1:0", Times = 1 });
}

if (mode == "limit")
{
    options.AdditionalArguments.Add("--max-line");
    options.AdditionalArguments.Add("16");
}

if (mode == "idle")
{
    options.IdleTimeout = TimeSpan.FromSeconds(1);
}

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (ArgumentException e)
{
    Console.WriteLine($"refused: {e.Message}");
    return 1;
}

await using (server)
{
    switch (mode)
    {
        case "session" or "events":
            Console.Write(await Talk(server, "hello\r\nQUIT\r\n"));
            var ended = (ExchangeEndedEvent)await server.WaitForEventAsync(
                new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, Scheme = "echo" }, TimeSpan.FromSeconds(10), CancellationToken.None);
            if (mode == "events")
            {
                Console.WriteLine($"exchange {ended.ExchangeId} ended: {ended.ReceivedByteCount} bytes received, {ended.SentByteCount} sent");
                break;
            }

            SurlExchangeRecord record = server.Exchanges.Single();
            Console.WriteLine($"recorded {record.Scheme} exchange: client sent {Escape(record.Received)}, server sent {Escape(record.Sent)}");
            break;
        case "fault":
            string first = await Talk(server, "hello\r\nQUIT\r\n");
            Console.WriteLine($"first session: {(first == "" ? "nothing back" : first)}");
            Console.Write($"second session: {await Talk(server, "hello\r\nQUIT\r\n")}");
            break;
        case "limit":
            Console.Write(await Talk(server, "short line\r\nthis line is longer than sixteen bytes\r\nQUIT\r\n"));
            break;
        case "idle":
            string late = await Talk(server, "hello\r\nQUIT\r\n", silence: TimeSpan.FromSeconds(3));
            Console.WriteLine($"after 3 s of silence: {(late == "" ? "nothing back" : late)}");
            break;
    }
}

return 0;

// One client session: connect, optionally say nothing for a while, send text, read until the server closes.
static async Task<string> Talk(SurlServer server, string text, TimeSpan silence = default)
{
    using var tcp = new TcpClient();
    await tcp.ConnectAsync("127.0.0.1", server.BoundUrl.Port);
    NetworkStream stream = tcp.GetStream();
    await Task.Delay(silence);
    var got = new MemoryStream();
    try
    {
        await stream.WriteAsync(Encoding.ASCII.GetBytes(text));
        await stream.CopyToAsync(got);
    }
    catch (IOException)
    {
        // A dropped connection is reset; what came before it is kept.
    }

    return Encoding.ASCII.GetString(got.ToArray());
}

static string Escape(byte[] bytes) => Encoding.ASCII.GetString(bytes).Replace("\r", "\\r").Replace("\n", "\\n");

// The in-house protocol: each line comes back as sent; QUIT is answered BYE and ends the session; a
// line longer than the server's line limit is answered ERR line too long and ends it.
sealed class EchoServer : IConnectionProtocolServer
{
    public IReadOnlyList<string> Schemes => ["echo"];

    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        var line = new List<byte>();
        var buffer = new byte[4096];
        int read;
        while ((read = await connection.ReadAsync(buffer, context.CancellationToken)) > 0)
        {
            foreach (byte b in buffer[..read])
            {
                line.Add(b);
                if (line.Count > context.Limits.MaxLineBytes)
                {
                    await Send("ERR line too long");
                    return;
                }

                if (b != (byte)'\n')
                {
                    continue;
                }

                string text = Encoding.ASCII.GetString(line.ToArray()).TrimEnd('\r', '\n');
                line.Clear();
                if (text == "QUIT")
                {
                    await Send("BYE");
                    return;
                }

                await Send(text);
            }
        }

        async Task Send(string text) => await connection.WriteAsync(Encoding.ASCII.GetBytes(text + "\r\n"), context.CancellationToken);
    }
}
