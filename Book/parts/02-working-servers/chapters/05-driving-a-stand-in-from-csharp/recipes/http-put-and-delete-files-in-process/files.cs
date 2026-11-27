// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Puts a file into a running in-process HTTP server and takes it away again, fetching it with a
// client each time. Run: dotnet run files.cs [-- overwrite|folder|events|client-delete|nothing|hidden]
//   (no argument)  put /reports/a.txt, fetch it, delete it, fetch it again
//   overwrite      put the same path twice
//   folder         list /reports and the root after the put and after the delete
//   events         the store events the put and the delete made
//   client-delete  a client's own DELETE, with no login
//   nothing        delete a file that is not there, and a folder
//   hidden         put a path whose name starts with a dot
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "solution";

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

switch (which)
{
    case "solution":
        await Put("reports/a.txt", "quarterly\n");
        await Get("reports/a.txt");
        await Delete("reports/a.txt");
        await Get("reports/a.txt");
        break;
    case "overwrite":
        await Put("reports/a.txt", "quarterly\n");
        await Put("reports/a.txt", "revised\n");
        await Get("reports/a.txt");
        break;
    case "folder":
        await Put("reports/a.txt", "quarterly\n");
        await List("/");
        await List("/reports");
        await Delete("reports/a.txt");
        await List("/");
        await List("/reports");
        break;
    case "events":
        await Put("reports/a.txt", "quarterly\n");
        await Delete("reports/a.txt");
        foreach (SurlEvent e in server.FindEvents(new SurlEventQuery { Store = StoreKind.Content }))
        {
            var change = (StoreChangedEvent)e;
            Console.WriteLine($"event: {change.Change} {change.Name}");
        }

        break;
    case "client-delete":
        CurlResponse denied = await client.SendAsync(
            new CurlRequest(HttpMethod.Delete, $"{server.BoundUrl}hello.txt"), CancellationToken.None);
        await using (Stream body = denied.Body) { await body.CopyToAsync(Stream.Null); }
        Console.WriteLine($"client DELETE hello.txt: {denied.StatusCode}");
        await Get("hello.txt");
        await Delete("hello.txt");
        await Get("hello.txt");
        break;
    case "nothing":
        await Put("reports/a.txt", "quarterly\n");
        await Delete("reports/missing.txt");
        await Delete("reports");
        break;
    case "hidden":
        await Put(".env", "secret\n");
        break;
}

return 0;

// The server's side: put and delete need no login, and go straight to the store. The path starts with a slash.
async Task Put(string path, string text)
{
    try
    {
        await server.Files.PutAsync("/" + path, Encoding.UTF8.GetBytes(text));
        Console.WriteLine($"put /{path}: {text.Length} bytes");
    }
    catch (Exception e) { Console.WriteLine($"put /{path}: {e.GetType().Name}: {e.Message}"); }
}

async Task Delete(string path)
{
    try
    {
        await server.Files.DeleteAsync("/" + path);
        Console.WriteLine($"delete /{path}: done");
    }
    catch (Exception e) { Console.WriteLine($"delete /{path}: {e.GetType().Name}: {e.Message}"); }
}

// The client's side: a GET, as any client of the server would make it.
async Task Get(string path)
{
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();
    Console.WriteLine($"GET {path}: {response.StatusCode}{(response.StatusCode == 200 ? " " + text.TrimEnd() : "")}");
}

async Task List(string folder)
{
    try
    {
        SurlFolderPage page = await server.Files.ListAsync(folder);
        string names = string.Join(" ", page.Entries.Select(e => e.IsFolder ? e.Name + "/" : e.Name));
        Console.WriteLine($"{folder}: {(names.Length == 0 ? "(empty)" : names)}");
    }
    catch (Exception e) { Console.WriteLine($"{folder}: {e.GetType().Name}: {e.Message}"); }
}
