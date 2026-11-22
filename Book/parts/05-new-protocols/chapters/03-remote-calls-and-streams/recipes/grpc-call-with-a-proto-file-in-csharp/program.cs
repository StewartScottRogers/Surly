// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Calls a gRPC method by its field numbers, as a .proto file gives them, and by reflection, with Surl.Api,
// Curl.Api and the framework's HttpClient, in one process. Run: dotnet run program.cs
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

// The server, in-process: port 0 takes a free one. surl.test.Echo and reflection are built in.
await using SurlServer server = await SurlServer.StartAsync(new SurlServerOptions
{
    ListenUrls = { "grpc://127.0.0.1:0" },
    AllowAnonymous = true,
});
using var http = new HttpClient();
string root = $"http://127.0.0.1:{server.BoundUrl.Port}/";

// What echo.proto says, as numbers: EchoRequest { string message = 1; uint32 count = 2; }
// and EchoReply { string message = 1; uint32 index = 2; }. A .proto file is these names and numbers;
// on the wire only the numbers are sent.
const int Message = 1, Count = 2, Index = 2;

// With the file: the program encodes the request itself and decodes the reply itself.
byte[] request = Text(Message, "hello");
Reply echo = await Call("surl.test.Echo/Echo", request);
Console.WriteLine($"echo: request {Convert.ToHexString(request)}, reply {Convert.ToHexString(echo.Messages[0])}, message {Read(echo.Messages[0], Message)}, status {echo.Status}");

Reply expand = await Call("surl.test.Echo/Expand", [.. Text(Message, "hi"), .. Number(Count, 2)]);
Console.WriteLine($"expand: {expand.Messages.Count} replies: {string.Join(", ", expand.Messages.Select(m => $"{Read(m, Message)}/{Read(m, Index)}"))}");

// A field number the type does not have is not an error for the server; it reads nothing from it.
Reply wrong = await Call("surl.test.Echo/Echo", Text(9, "hello"));
Console.WriteLine($"field 9: reply message [{Read(wrong.Messages[0], Message)}], status {wrong.Status}");

// Without the file: server reflection answers with the services it serves. The question is a
// ServerReflectionRequest whose field 7, list_services, is set to an empty string.
Reply listed = await Call("grpc.reflection.v1.ServerReflection/ServerReflectionInfo", Text(7, ""));
Console.WriteLine($"reflection lists: {string.Join(", ", Names(listed.Messages[0]))}");

// Curl.Api calls by reflection alone: it finds the types itself and sends an empty request, so the
// reply is the empty reply, printed as JSON; the same empty request by hand is a reply of no bytes.
await using CurlClient client = CurlClient.Create();
await using (Stream body = (await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}surl.test.Echo/Echo"), CancellationToken.None)).Body)
{
    Console.WriteLine($"Curl.Api, no body: {(await new StreamReader(body).ReadToEndAsync()).Trim()}");
}
Reply empty = await Call("surl.test.Echo/Echo", []);
Console.WriteLine($"by hand, empty request: reply of {empty.Messages[0].Length} bytes");

// Curl.Api refuses what reflection cannot find, as a CurlException with curl's own exit code.
await Missing("unknown service", "surl.test.Other/Echo");
await Missing("unknown method", "surl.test.Echo/Shout");
return 0;

async Task Missing(string label, string method)
{
    try
    {
        await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{method}"), CancellationToken.None);
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"Curl.Api, {label}: CurlException {(int)failure.Code} {failure.Code}");
    }
}

// One call: HTTP/2 with prior knowledge, a POST of one length-prefixed message, and the status in the trailers.
async Task<Reply> Call(string method, byte[] message)
{
    byte[] framed = [0, .. BitConverter.GetBytes(message.Length).Reverse(), .. message];
    using var content = new ByteArrayContent(framed) { Headers = { ContentType = new MediaTypeHeaderValue("application/grpc") } };
    using var send = new HttpRequestMessage(HttpMethod.Post, root + method)
    {
        Content = content, Version = HttpVersion.Version20, VersionPolicy = HttpVersionPolicy.RequestVersionExact,
    };
    send.Headers.TryAddWithoutValidation("TE", "trailers");
    using HttpResponseMessage response = await http.SendAsync(send);
    byte[] data = await response.Content.ReadAsByteArrayAsync();
    var messages = new List<byte[]>();
    for (int at = 0; at < data.Length;)
    {
        int length = BitConverter.ToInt32(data.AsSpan(at + 1, 4).ToArray().Reverse().ToArray());
        messages.Add(data[(at + 5)..(at + 5 + length)]);
        at += 5 + length;
    }
    string status = response.TrailingHeaders.TryGetValues("grpc-status", out var trailer) ? trailer.First() : "none";
    return new Reply(messages, status);
}

// Protocol Buffers by hand: a field is its number and wire type, then a length and bytes, or a varint.
static byte[] Text(int field, string value) => [(byte)(field << 3 | 2), (byte)value.Length, .. Encoding.UTF8.GetBytes(value)];
static byte[] Number(int field, int value) => [(byte)(field << 3), (byte)value];

static IEnumerable<(int Field, byte[] Bytes, int Number)> Fields(byte[] data)
{
    for (int at = 0; at < data.Length;)
    {
        int field = data[at] >> 3, wire = data[at] & 7;
        at++;
        if (wire == 0) { yield return (field, [], data[at++]); }
        else { int length = data[at++]; yield return (field, data[at..(at + length)], 0); at += length; }
    }
}

static string Read(byte[] message, int field) =>
    Fields(message).Where(f => f.Field == field).Select(f => f.Bytes.Length > 0 ? Encoding.UTF8.GetString(f.Bytes) : f.Number.ToString()).FirstOrDefault() ?? (field == 2 ? "0" : "");

// ServerReflectionResponse.list_services_response is field 6, holding repeated service, each with a name.
static IEnumerable<string> Names(byte[] response) =>
    Fields(response).Where(f => f.Field == 6).SelectMany(f => Fields(f.Bytes))
        .Select(service => Encoding.UTF8.GetString(Fields(service.Bytes).First().Bytes));

record Reply(List<byte[]> Messages, string Status);
