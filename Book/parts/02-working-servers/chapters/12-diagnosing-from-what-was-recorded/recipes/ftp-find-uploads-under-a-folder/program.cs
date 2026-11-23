// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test finds every upload under /uploads in the server's record, and not the one in /uploads2,
// with SurlEventQuery.Name. The server and the client are both in this process.
// Run: dotnet run program.cs [-- <case>]
//   (none)    the uploads under /uploads (the default)
//   all       the same query with no Name
//   nested    a Name that is a subfolder
//   case      a Name in the wrong case
//   detail    what one upload's event holds
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using Curl.Api;
using Curl.Api.Options.Protocols;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "under";

var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0" },
    AllowAnonymous = true,
    AllowUploads = true,
    RecordExchanges = true,
};
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

// The code under test uploads four files: three under /uploads, one in the folder /uploads2.
await Upload("uploads/a.txt", "alpha\n");
await Upload("uploads/b.txt", "beta\n");
await Upload("uploads/2026/c.txt", "gamma\n");
await Upload("uploads2/d.txt", "delta\n");

// The last upload's event is kept after the earlier ones: wait, bounded, for it, and the rest are there.
var last = new SurlEventQuery { Store = StoreKind.Content, Name = "/uploads2/d.txt" };
await server.WaitForEventAsync(last, TimeSpan.FromSeconds(30));

string name = which switch
{
    "all" => "",
    "nested" => "/uploads/2026",
    "case" => "/Uploads",
    _ => "/uploads",
};
var query = new SurlEventQuery { Store = StoreKind.Content, Name = name == "" ? null : name };
IReadOnlyList<SurlEvent> found = server.FindEvents(query);

if (which == "detail")
{
    var first = (StoreChangedEvent)server.FindEvents(new SurlEventQuery { Store = StoreKind.Content, Name = "/uploads/a.txt" }).Single();
    Console.WriteLine($"{first.Change} {first.Name}, {first.ByteCount} bytes, exchange recorded: {first.ExchangeId is not null}");
    return 0;
}

// A new folder is a change too: Created for the folder, Written for each file stored. The uploads are the writes.
var changes = found.Cast<StoreChangedEvent>().ToList();
var uploads = changes.Where(c => c.Change == StoreChange.Written).ToList();
Console.WriteLine($"name {(name == "" ? "(none)" : name)}: {changes.Count} events, {uploads.Count} written");
foreach (StoreChangedEvent upload in uploads)
{
    Console.WriteLine($"  {upload.Name} {upload.ByteCount} bytes");
}
return 0;

async Task Upload(string path, string text)
{
    var put = new CurlRequest(HttpMethod.Put, $"ftp://127.0.0.1:{server.BoundUrl.Port}/{path}")
    {
        Body = System.Text.Encoding.UTF8.GetBytes(text),
        Ftp = new CurlFtpOptions { CreateMissingDirectories = FtpCreateMissingDirectories.Create },
    };
    CurlResponse response = await client.SendAsync(put, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
}
