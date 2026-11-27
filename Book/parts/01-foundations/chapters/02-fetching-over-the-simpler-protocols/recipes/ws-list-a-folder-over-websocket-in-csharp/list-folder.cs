// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Lists a folder of an in-process server over WebSocket with Curl.Api, and shows what happens when the
// server has listings off.
// Run: dotnet run list-folder.cs [-- <path>... | off | log]
//   (no argument) the root
//   <path>...     these paths, one connection each, such as notes/ notes, hello.txt, .secret or nowhere/
//   off           the root of a server without ListDirectories
//   log           the server's own account of the listing it sent
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;

string which = args.Length > 0 ? args[0] : "";

var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "ws://127.0.0.1:0" },
    ListDirectories = which != "off",
    Log = log,
    LogLevel = LogLevel.Verbose,
};
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
options.Files["notes/today.txt"] = "nothing happened\n"u8.ToArray();
options.Files[".secret"] = "not for you\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);

string[] paths = which is "off" or "log" or "" ? [""] : args;
await using CurlClient client = CurlClient.Create();
foreach (string path in paths)
{
    try
    {
        await using CurlWebSocket socket = await client.ConnectWebSocketAsync($"{server.BoundUrl}{path}", CancellationToken.None);
        CurlWebSocketMessage message = await socket.ReceiveAsync(CancellationToken.None);
        Console.WriteLine($"/{path}: {message.Type}, {message.Text!.Replace("\n", "|")}");
        CurlWebSocketMessage next = await socket.ReceiveAsync(CancellationToken.None);
        Console.WriteLine($"then: {next.Type}");
    }
    catch (CurlException refused)
    {
        Console.WriteLine($"/{path}: refused, {refused.Code} ({(int)refused.Code})");
    }
}

if (which == "log")
{
    foreach (string line in log.ToString().Split('\n').Where(l => l.Contains("as a text message")))
    {
        Console.WriteLine(line[(line.IndexOf('*') + 2)..].TrimEnd());
    }
}

return 0;
