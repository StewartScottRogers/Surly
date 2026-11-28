// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Plays one RTSP stream with libcurl's CURLOPT_RTSP_REQUEST, through Libcurl, the libcurl API
// for C#: DESCRIBE, SETUP, PLAY, RECEIVE until the last RTP packet, TEARDOWN. The RTP payloads
// are saved, in order, to the output file.
// Run: dotnet run rtsp-play.cs -- <rtsp-url> <output-file> [transport]
#:package Surly.Libcurl@0.4.0-preview.7

using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options.Protocols;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

if (args.Length is < 2 or > 3)
{
    Console.Error.WriteLine("usage: dotnet run rtsp-play.cs -- <rtsp-url> <output-file> [transport]");
    return 2;
}

string transport = args.Length == 3 ? args[2] : "RTP/AVP/TCP;unicast;interleaved=0-1";
var description = new MemoryStream();                        // DESCRIBE's body, the SDP
var media = new MemoryStream();                              // the RTP payloads, in order
int rtpPackets = 0, rtcpPackets = 0;
bool lastPacket = false;
CurlCode lastResult = CurlCode.Ok;                           // the last curl_easy_perform's result

await using CurlLibrary library = CurlGlobal.Init();         // curl_global_init
using CurlEasy easy = library.EasyInit();                    // curl_easy_init
easy.SetOption(CurlOption.Url, args[0]);
easy.SetOption(CurlOption.RtspStreamUri, args[0]);
easy.SetOption(CurlOption.Writefunction, (WriteCallback)(data =>
{
    description.Write(data);
    return data.Length;
}));

// Each call is one whole interleaved frame: '$', the channel, a two-byte length, then the packet.
easy.SetOption(CurlOption.Interleavefunction, (InterleaveCallback)(data =>
{
    ReadOnlySpan<byte> packet = data[4..];
    if (data[1] == 0)                                        // channel 0: RTP
    {
        rtpPackets++;
        media.Write(packet[12..]);                           // after the 12-byte RTP header
        lastPacket = (packet[1] & 0x80) != 0;                // the marker bit ends the stream
    }
    else                                                     // channel 1: RTCP
    {
        rtcpPackets++;
    }

    return data.Length;
}));

int status = Request(RtspRequest.Describe);
if (status != 200)
{
    return Refused("DESCRIBE", status);
}

string mediaLine = Encoding.UTF8.GetString(description.ToArray()).Split("\r\n").First(line => line.StartsWith("m="));
Console.WriteLine($"DESCRIBE {status}: {mediaLine}");

easy.SetOption(CurlOption.RtspTransport, transport);
status = Request(RtspRequest.Setup);
if (status != 200)
{
    return Refused("SETUP", status);
}

easy.GetInfo(CurlInfo.RtspSessionId, out object? session);  // libcurl sends it on every later request
Console.WriteLine($"SETUP {status}");
Console.Error.WriteLine($"session {session}");

status = Request(RtspRequest.Play);
if (status != 200)
{
    return Refused("PLAY", status);
}

Console.WriteLine($"PLAY {status}");

// RECEIVE sends nothing; it reads the interleaved frames that follow PLAY's answer.
while (!lastPacket)
{
    if (Request(RtspRequest.Receive) < 0)
    {
        return Fail();
    }
}

status = Request(RtspRequest.Teardown);
Console.WriteLine($"TEARDOWN {status}");
Console.WriteLine($"RTP packets: {rtpPackets} ({media.Length} bytes), RTCP packets: {rtcpPackets}");
File.WriteAllBytes(args[1], media.ToArray());
return status == 200 ? 0 : 22;

// One curl_easy_perform for one RTSP request: the status code, or -1 when the transfer failed.
int Request(RtspRequest request)
{
    easy.SetOption(CurlOption.RtspRequest, request);
    lastResult = easy.Perform();
    if (lastResult != CurlCode.Ok)
    {
        return -1;
    }

    easy.GetInfo(CurlInfo.ResponseCode, out object? code);
    return (int)(long)code!;
}

// An answer other than 200 ends the program as curl -f would, with 22.
int Refused(string method, int code)
{
    if (code < 0)
    {
        return Fail();
    }

    Console.WriteLine($"{method} {code}");
    return 22;
}

int Fail()
{
    Console.Error.WriteLine($"curl error {(int)lastResult} ({lastResult}): {easy.ErrorText}");
    return (int)lastResult;
}
