// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// The old client (HttpClient) and the new one (Curl.Api) make the same two calls, each against its
// own in-process Surl.Api server, and the test compares what the servers recorded.
// Run: dotnet run program.cs [-- <case>]
//   (none)    compare the two records, request by request (the default)
//   headers   the header names each client sent
//   aligned   the old client told to send what the new one does by default
//   stored    what each server holds after the upload
//   refused   the same run against servers that allow no anonymous upload
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "compare";

// Two servers with the same options, one for each client, so each record holds one client's calls and no other.
SurlServerOptions Options()
{
    var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true, AllowUploads = true, AllowAnonymous = which != "refused" };
    options.Files["report.txt"] = "totals\n"u8.ToArray();
    return options;
}
await using SurlServer oldServer = await SurlServer.StartAsync(Options());
await using SurlServer newServer = await SurlServer.StartAsync(Options());

int oldStatus, newStatus;

// The old client: a GET, then a PUT with a header of its own.
using (var old = new HttpClient())
{
    if (which == "aligned")
    {
        old.DefaultRequestHeaders.Add("Accept", "*/*");
    }
    await old.GetStringAsync($"{oldServer.BoundUrl}report.txt");
    var put = new HttpRequestMessage(HttpMethod.Put, $"{oldServer.BoundUrl}up.txt")
    {
        Content = new StringContent("hello", Encoding.UTF8, "text/plain"),
    };
    put.Headers.Add("X-Trace", "abc");
    oldStatus = (int)(await old.SendAsync(put)).StatusCode;
}

// The new client: the same two calls.
await using (CurlClient client = CurlClient.Create())
{
    CurlResponse get = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{newServer.BoundUrl}report.txt"), CancellationToken.None);
    await using (Stream body = get.Body)
    {
        await body.CopyToAsync(Stream.Null);
    }
    CurlResponse put = await client.SendAsync(new CurlRequest(HttpMethod.Put, $"{newServer.BoundUrl}up.txt")
    {
        Body = "hello"u8.ToArray(),
        Headers = [new("X-Trace", "abc"), new("Content-Type", "text/plain; charset=utf-8")],
    }, CancellationToken.None);
    newStatus = put.StatusCode;
    await using (Stream body = put.Body)
    {
        await body.CopyToAsync(Stream.Null);
    }
}

// Both clients are disposed, so their connections are closed: wait, bounded, for each server to record the end.
var ended = new SurlEventQuery { Scheme = "http", Kind = SurlEventKind.ExchangeEnded };
await oldServer.WaitForEventAsync(ended, TimeSpan.FromSeconds(30));
await newServer.WaitForEventAsync(ended, TimeSpan.FromSeconds(30));
List<Recorded> before = Requests(oldServer);
List<Recorded> after = Requests(newServer);

switch (which)
{
    case "compare":
    case "aligned":
        Console.WriteLine($"requests recorded: {before.Count} and {after.Count}");
        for (int i = 0; i < before.Count; i++)
        {
            Console.WriteLine($"{before[i].Line}: same line {before[i].Line == after[i].Line}, same body {before[i].Body == after[i].Body}, " +
                $"headers only in the old: [{string.Join(", ", before[i].Headers.Except(after[i].Headers))}], only in the new: [{string.Join(", ", after[i].Headers.Except(before[i].Headers))}]");
        }
        break;

    case "headers":
        Console.WriteLine($"old, PUT: {string.Join(", ", before[1].Headers.Select(h => h.Split(':')[0]))}");
        Console.WriteLine($"new, PUT: {string.Join(", ", after[1].Headers.Select(h => h.Split(':')[0]))}");
        break;

    case "refused":
        Console.WriteLine($"PUT answered {oldStatus} to the old client and {newStatus} to the new one");
        Console.WriteLine($"requests recorded: {before.Count} and {after.Count}");
        break;

    case "stored":
        SurlFileContents fromOld = await oldServer.Files.ReadAsync("/up.txt");
        SurlFileContents fromNew = await newServer.Files.ReadAsync("/up.txt");
        Console.WriteLine($"old server holds: {Encoding.UTF8.GetString(fromOld.Bytes.Span)} ({fromOld.ByteCount} bytes)");
        Console.WriteLine($"new server holds: {Encoding.UTF8.GetString(fromNew.Bytes.Span)} ({fromNew.ByteCount} bytes)");
        break;
}
return 0;

// The requests a server recorded, in order: the request line, the headers (the host's port left out) and the body.
static List<Recorded> Requests(SurlServer server)
{
    string text = string.Concat(server.Exchanges.OrderBy(e => e.ExchangeId).Select(e => Encoding.UTF8.GetString(e.Received)));
    var found = new List<Recorded>();
    while (text.Length > 0)
    {
        int end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        string[] lines = text[..end].Split("\r\n");
        var headers = lines.Skip(1).Select(h => h.StartsWith("Host:") ? "Host: <server>" : h).OrderBy(h => h, StringComparer.Ordinal).ToList();
        string length = lines.Skip(1).FirstOrDefault(h => h.StartsWith("Content-Length:"))?.Split(':')[1].Trim() ?? "0";
        int bodyLength = int.Parse(length);
        found.Add(new Recorded(lines[0], headers, text.Substring(end + 4, bodyLength)));
        text = text[(end + 4 + bodyLength)..];
    }
    return found;
}

record Recorded(string Line, List<string> Headers, string Body);
