// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Resumes a partial FTP download with Curl.Api: REST with the length already on disk, the rest appended,
// and the joined file compared with the server's. The recorded exchange shows the REST the server saw.
// Run: dotnet run resume.cs [-- fresh|restart|offset|wrong|checked]
//   (no argument) a partial copy of 65,536 bytes is resumed
//   fresh         no local file at all
//   restart       the same partial copy, fetched again without REST
//   offset        REST 100000, whatever is on disk
//   wrong         a partial copy of 65,536 zeros
//   checked       the resume without IgnoreContentLength
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "resume";

// The image: 200,000 bytes that repeat from run to run.
byte[] image = new byte[200_000];
new Random(7).NextBytes(image);

var options = new SurlServerOptions { ListenUrls = { "ftp://127.0.0.1:0" }, AllowAnonymous = true, RecordExchanges = true };
options.Files["update.iso"] = image;
await using SurlServer server = await SurlServer.StartAsync(options);

// What a download cut off at 65,536 bytes leaves on disk, or what this run starts from instead.
const string Local = "update.iso";
if (File.Exists(Local))
{
    File.Delete(Local);
}

if (which != "fresh" && which != "offset")
{
    File.WriteAllBytes(Local, which == "wrong" ? new byte[65_536] : image[..65_536]);
}

// REST asks the server to start after that many bytes. The offset is the length on disk, or 100000 for
// "offset", and "restart" sends none and so starts at byte 0.
long have = File.Exists(Local) ? new FileInfo(Local).Length : 0;
long from = which == "offset" ? 100_000 : which == "restart" ? 0 : have;
bool append = from > 0;
try
{
    // IgnoreContentLength stops the client checking the bytes against SIZE, which is the whole file's.
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}update.iso")
    {
        Ftp = append ? new CurlFtpOptions { PreQuote = [$"REST {from}"] } : null,
        Http = new CurlHttpOptions { IgnoreContentLength = which != "checked" },
    };
    await using CurlClient client = CurlClient.Create();
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    // Resuming appends; a fetch from the start replaces what was there.
    await using var file = new FileStream(Local, which == "restart" || !File.Exists(Local) ? FileMode.Create : FileMode.Append);
    await body.CopyToAsync(file);
    file.Close();
    Console.WriteLine($"{which}: from {from}: {new FileInfo(Local).Length - (which == "restart" || !File.Exists(Local) ? 0 : have)} bytes");
}
catch (CurlException failure)
{
    Console.WriteLine($"{which}: from {from}: {failure.Code}");
}

// The test's check: the local file against the server's, and the commands the server received.
await WaitForEndsAsync();
byte[] got = File.ReadAllBytes(Local);
byte[] expected = which == "offset" ? image[100_000..] : image;
bool same = got.AsSpan().SequenceEqual(expected);
Console.WriteLine($"{Local}: {got.Length} bytes, {(same ? (which == "offset" ? "identical to the last 100000 bytes of the server's" : "identical to the server's") : "DIFFERENT from the server's")}");
string[] received = Encoding.ASCII.GetString(server.Exchanges[0].Received).Split("\r\n");
Console.WriteLine($"server received: {string.Join(", ", received.Where(l => l.StartsWith("REST ") || l.StartsWith("RETR ")))}");
Console.WriteLine($"server answered: {Encoding.ASCII.GetString(server.Exchanges[0].Sent).Split("\r\n").FirstOrDefault(l => l.StartsWith("350")) ?? "no 350"}");
return same ? 0 : 1;

// The server records an exchange a moment after the client has gone, so wait (at most 10 s) until
// every exchange it started has ended before reading the record.
async Task WaitForEndsAsync()
{
    var started = new SurlEventQuery { Kind = SurlEventKind.ExchangeStarted };
    var ended = new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded };
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); server.FindEvents(ended).Count < server.FindEvents(started).Count && waited.Elapsed.TotalSeconds < 10;)
    {
        await Task.Delay(100);
    }
}
