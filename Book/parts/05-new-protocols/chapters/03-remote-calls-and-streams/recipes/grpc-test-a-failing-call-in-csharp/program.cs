// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Makes a gRPC server fail calls with chosen statuses and reads the statuses back, with Surl.Api and the
// framework's HttpClient, in one process. Run: dotnet run program.cs
#:package Surly.Surl.Api@0.1.12

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Surl.Api;

// The server, in-process: port 0 takes a free one. surl.test.Echo is built in.
await using SurlServer server = await SurlServer.StartAsync(new SurlServerOptions
{
    ListenUrls = { "grpc://127.0.0.1:0" },
    AllowAnonymous = true,
});
using var http = new HttpClient();
string root = $"http://127.0.0.1:{server.BoundUrl.Port}/";

// EchoRequest { string message = 1; uint32 count = 2; int32 status_code = 3; string status_message = 4;
//               uint32 delay_milliseconds = 5; }
const int Message = 1, Count = 2, StatusCode = 3, StatusMessage = 4, Delay = 5;
string[] names =
[
    "OK", "CANCELLED", "UNKNOWN", "INVALID_ARGUMENT", "DEADLINE_EXCEEDED", "NOT_FOUND", "ALREADY_EXISTS",
    "PERMISSION_DENIED", "RESOURCE_EXHAUSTED", "FAILED_PRECONDITION", "ABORTED", "OUT_OF_RANGE",
    "UNIMPLEMENTED", "INTERNAL", "UNAVAILABLE", "DATA_LOSS", "UNAUTHENTICATED",
];

await Fail("not found", "Echo", [.. Text(StatusMessage, "no such order"), .. Number(StatusCode, 5)]);
await Fail("invalid", "Echo", [.. Text(StatusMessage, "quantity must be positive"), .. Number(StatusCode, 3)]);
await Fail("no message", "Echo", Number(StatusCode, 14));
await Fail("zero", "Echo", [.. Text(Message, "order 7"), .. Number(StatusCode, 0)]);
await Fail("percent", "Echo", [.. Text(StatusMessage, "disk 100% full"), .. Number(StatusCode, 9)]);
await Fail("out of range", "Echo", [.. Text(StatusMessage, "odd"), .. Number(StatusCode, 99)]);
await Fail("part way", "Expand", [.. Text(Message, "s"), .. Number(Count, 3), .. Number(StatusCode, 13), .. Text(StatusMessage, "boom")]);
await Fail("server deadline", "Echo", Number(StatusCode, 4));

// The client's own deadline is not a status: the server holds the reply back and the client stops waiting.
try
{
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(1));
    await Call("Echo", [.. Text(Message, "x"), .. Number(Delay, 3000)], deadline.Token);
    Console.WriteLine("client gives up: the reply came");
}
catch (OperationCanceledException)
{
    Console.WriteLine("client gives up: no status, the call was cancelled after 1 s");
}
return 0;

// One call, and what curl would have made of it: the status, its message, the replies, and the exit code.
async Task Fail(string label, string method, byte[] request)
{
    (List<byte[]> replies, int status, string message) = await Call(method, request, CancellationToken.None);
    Console.WriteLine($"{label}: status {status} ({names[status]}), message [{message}], replies {replies.Count}, curl exit {(status == 0 ? 0 : 22)}");
}

// A gRPC call is HTTP/2 with prior knowledge, a POST of one length-prefixed message, and the status in the
// trailers; a call that fails before any reply has them in the headers instead.
async Task<(List<byte[]>, int, string)> Call(string method, byte[] message, CancellationToken cancel)
{
    byte[] framed = [0, .. BitConverter.GetBytes(message.Length).Reverse(), .. message];
    using var content = new ByteArrayContent(framed) { Headers = { ContentType = new MediaTypeHeaderValue("application/grpc") } };
    using var send = new HttpRequestMessage(HttpMethod.Post, $"{root}surl.test.Echo/{method}")
    {
        Content = content, Version = HttpVersion.Version20, VersionPolicy = HttpVersionPolicy.RequestVersionExact,
    };
    send.Headers.TryAddWithoutValidation("TE", "trailers");
    using HttpResponseMessage response = await http.SendAsync(send, cancel);
    byte[] data = await response.Content.ReadAsByteArrayAsync(cancel);
    var replies = new List<byte[]>();
    for (int at = 0; at < data.Length;)
    {
        int length = BitConverter.ToInt32(data.AsSpan(at + 1, 4).ToArray().Reverse().ToArray());
        replies.Add(data[(at + 5)..(at + 5 + length)]);
        at += 5 + length;
    }
    string Header(string name) =>
        response.TrailingHeaders.TryGetValues(name, out var t) ? t.First()
        : response.Headers.TryGetValues(name, out var h) ? h.First() : "";
    return (replies, int.Parse(Header("grpc-status")), Uri.UnescapeDataString(Header("grpc-message")));
}

// Protocol Buffers by hand: a field is its number and a wire type, then a length and bytes or a varint.
static byte[] Text(int field, string value) => [(byte)(field << 3 | 2), (byte)value.Length, .. Encoding.UTF8.GetBytes(value)];

static byte[] Number(int field, int value)
{
    var bytes = new List<byte> { (byte)(field << 3) };
    for (; value > 127; value >>= 7) bytes.Add((byte)(value | 128));
    bytes.Add((byte)value);
    return [.. bytes];
}
