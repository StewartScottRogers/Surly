// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Sends each message to a WebSocket server with libcurl's curl_ws_send and prints what
// curl_ws_recv brings back, through Libcurl, the libcurl API for C#.
// Run: dotnet run ws-echo.cs -- <ws-url> <message>...
#:package Libcurl@0.2.0

using System.Buffers.Binary;
using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.WebSocket;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;
using Libcurl.WebSocket;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: dotnet run ws-echo.cs -- <ws-url> <message>...");
    return 2;
}

await using CurlLibrary library = CurlGlobal.Init();         // curl_global_init
using CurlEasy easy = library.EasyInit();                    // curl_easy_init
easy.SetOption(CurlOption.Url, args[0]);
easy.SetOption(CurlOption.ConnectOnly, 2L);                  // 2: upgrade to WebSocket, then stop
CurlCode result = easy.Perform();                            // curl_easy_perform
if (result != CurlCode.Ok)
{
    return Fail(result);
}

// Deliberately small, so a longer message takes several curl_ws_recv calls.
byte[] buffer = new byte[64];

foreach (string message in args[1..])
{
    result = easy.WebSocketSend(Encoding.UTF8.GetBytes(message), out _, 0, CurlWebSocketFlags.Text);
    if (result != CurlCode.Ok)
    {
        return Fail(result);
    }

    result = ReceiveMessage(out CurlWebSocketFlags flags, out byte[] payload);
    if (result != CurlCode.Ok)
    {
        return Fail(result);
    }

    if (flags.HasFlag(CurlWebSocketFlags.Close))
    {
        Console.WriteLine(CloseLine(payload));                 // the server ended it first
        return 1;
    }

    Console.WriteLine($"{flags} {payload.Length}: {Encoding.UTF8.GetString(payload)}");
}

// Close with 1000, normal closure, and wait for the server's CLOSE in answer.
byte[] normal = new byte[2];
BinaryPrimitives.WriteUInt16BigEndian(normal, 1000);
result = easy.WebSocketSend(normal, out _, 0, CurlWebSocketFlags.Close);
if (result == CurlCode.Ok)
{
    result = ReceiveMessage(out _, out byte[] closing);
    if (result == CurlCode.Ok)
    {
        Console.WriteLine(CloseLine(closing));
    }
}

return result == CurlCode.Ok ? 0 : Fail(result);

// curl_ws_recv until a whole message is in: CURLE_AGAIN means nothing has arrived yet, and a
// frame's bytesleft (and CURLWS_CONT) say whether more of the message is still to come.
CurlCode ReceiveMessage(out CurlWebSocketFlags flags, out byte[] payload)
{
    var whole = new MemoryStream();
    flags = CurlWebSocketFlags.None;
    while (true)
    {
        CurlCode code = easy.WebSocketRecv(buffer, out int received, out CurlWebSocketFrame? meta);
        if (code == CurlCode.Again)
        {
            Thread.Sleep(10);
            continue;
        }

        if (code != CurlCode.Ok)
        {
            payload = whole.ToArray();
            return code;
        }

        whole.Write(buffer, 0, received);
        flags = meta!.Value.Flags;
        if (meta.Value.BytesLeft == 0 && !flags.HasFlag(CurlWebSocketFlags.Continuation))
        {
            payload = whole.ToArray();
            return CurlCode.Ok;
        }
    }
}

// A CLOSE frame's payload starts with a two-byte status code, if it has one.
static string CloseLine(byte[] payload) =>
    payload.Length >= 2 ? $"close {BinaryPrimitives.ReadUInt16BigEndian(payload)}" : "close";

int Fail(CurlCode code)
{
    Console.Error.WriteLine($"curl error {(int)code} ({code}): {easy.ErrorText}");
    return (int)code;
}
