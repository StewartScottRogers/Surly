// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Reads a server-streaming gRPC method with Curl.Api against a Surl.Api server in one process.
// Run: dotnet run read-a-stream.cs -- MODE
//   stream      Expand with a count of 3, each reply as JSON
//   empty       a count of 0, and no count
//   limit       a count above 1000
//   arrival     each reply held back 300 ms, read for one second
//   part-way    a stream that ends in a status after its replies
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "stream";

// The server, in-process: port 0 takes a free one. surl.test.Echo is built in, with Expand, a server stream.
await using SurlServer server = await SurlServer.StartAsync(new SurlServerOptions
{
    ListenUrls = { "grpc://127.0.0.1:0" },
    AllowAnonymous = true,
});
await using CurlClient client = CurlClient.Create();
string expand = $"{server.BoundUrl}surl.test.Echo/Expand";

// The first Curl.Api request in a process pays a one-time set-up of about four seconds; make it before any timing.
await Send("""{"message":"warm-up"}""", CancellationToken.None);

switch (which)
{
    case "stream":
        Console.WriteLine(Compact(await Send("""{"message":"s","count":3}""", CancellationToken.None)));
        break;
    case "empty":
        Console.WriteLine($"count 0: [{await Send("""{"message":"s","count":0}""", CancellationToken.None)}]");
        Console.WriteLine($"no count: [{await Send("""{"message":"s"}""", CancellationToken.None)}]");
        break;
    case "limit":
    case "part-way":
        try
        {
            string request = which == "limit"
                ? """{"message":"s","count":1001}"""
                : """{"message":"s","count":3,"statusCode":13,"statusMessage":"boom"}""";
            Console.WriteLine(await Send(request, CancellationToken.None));
        }
        catch (CurlException failed)
        {
            Console.WriteLine($"{failed.Code} ({(int)failed.Code}): {failed.Message}");
        }
        break;
    case "arrival":
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var seen = new StringBuilder();
        try
        {
            CurlResponse response = await client.SendAsync(Request("""{"message":"s","count":10,"delayMilliseconds":300}"""), stop.Token);
            await using Stream body = response.Body;
            var buffer = new byte[4096];
            int read;
            while ((read = await body.ReadAsync(buffer, stop.Token)) > 0)
                seen.Append(Encoding.UTF8.GetString(buffer, 0, read));
            Console.WriteLine("stream ended");
        }
        catch (OperationCanceledException)
        {
            int replies = seen.ToString().Split("\"message\"").Length - 1;
            Console.WriteLine($"cancelled with {(replies is > 0 and < 10 ? "some" : replies.ToString())} of 10 replies");
        }
        break;
    }
    default:
        Console.Error.WriteLine($"unknown mode {which}");
        return 2;
}

return 0;

CurlRequest Request(string json) =>
    new(HttpMethod.Post, expand) { Body = Encoding.UTF8.GetBytes(json) };

// One call: the replies are the response body, a JSON object each.
async Task<string> Send(string json, CancellationToken cancel)
{
    CurlResponse response = await client.SendAsync(Request(json), cancel);
    await using Stream body = response.Body;
    return (await new StreamReader(body).ReadToEndAsync(cancel)).TrimEnd();
}

static string Compact(string text) => string.Join(" ", text.Split('\n').Select(line => line.Trim()));
