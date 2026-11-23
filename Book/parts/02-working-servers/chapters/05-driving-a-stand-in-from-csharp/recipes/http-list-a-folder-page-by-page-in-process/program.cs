// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test reads a folder of hundreds of files straight from an in-process server's store, one page
// at a time, with Files.ListAsync, afterName and HasMore. No client and no HTTP.
// Run: dotnet run program.cs [-- <case>]
//   (none)    250 files in pages of 100
//   exact     200 files in pages of 100
//   order     names that differ in case
//   folders   a folder with files and a subfolder
//   cursor    files added while paging
//   limits    limits outside 1 to 1000, and a folder that is not there
#:package Surly.Surl.Api@*

using Surl.Api;

string which = args.Length > 0 ? args[0] : "pages";

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
int count = which == "exact" ? 200 : 250;
if (which is "pages" or "exact")
{
    for (int i = 1; i <= count; i++)
    {
        options.Files[$"logs/log-{i:000}.txt"] = "x\n"u8.ToArray();
    }
}
else if (which == "order")
{
    foreach (string name in new[] { "b.txt", "B.txt", "a.txt", "A.txt", "10.txt", "2.txt" })
    {
        options.Files[$"mixed/{name}"] = "x\n"u8.ToArray();
    }
}
else if (which == "folders")
{
    options.Files["site/index.html"] = "<p>hi</p>\n"u8.ToArray();
    options.Files["site/css/site.css"] = "p{}\n"u8.ToArray();
    options.Files["site/about.html"] = "<p>about</p>\n"u8.ToArray();
}
else if (which == "limits")
{
    options.Files["a.txt"] = "x\n"u8.ToArray();
}
else if (which == "cursor")
{
    foreach (string name in new[] { "b.txt", "d.txt", "f.txt", "h.txt" })
    {
        options.Files[$"mixed/{name}"] = "x\n"u8.ToArray();
    }
}
await using SurlServer server = await SurlServer.StartAsync(options);

switch (which)
{
    case "pages":
    case "exact":
    {
        // The paging loop a test reuses: ask again with the last name seen until HasMore is false.
        var names = new List<string>();
        string? after = null;
        int page = 0;
        while (true)
        {
            SurlFolderPage result = await server.Files.ListAsync("/logs", after, 100);
            page++;
            Console.WriteLine($"page {page}: {result.Entries.Count} entries, {result.Entries[0].Name} to {result.Entries[^1].Name}, more: {result.HasMore}");
            names.AddRange(result.Entries.Select(e => e.Name));
            if (!result.HasMore)
            {
                break;
            }
            after = result.Entries[^1].Name;
        }
        Console.WriteLine($"{names.Count} names, {names.Distinct().Count()} different, in order: {names.SequenceEqual(names.Order(StringComparer.Ordinal))}");
        break;
    }

    case "order":
    {
        SurlFolderPage result = await server.Files.ListAsync("/mixed", null, 100);
        Console.WriteLine(string.Join(" ", result.Entries.Select(e => e.Name)));
        break;
    }

    case "folders":
    {
        SurlFolderPage result = await server.Files.ListAsync("/site", null, 100);
        foreach (SurlFolderEntry entry in result.Entries)
        {
            Console.WriteLine($"{entry.Name}: {(entry.IsFolder ? "folder" : $"file, {entry.ByteCount} bytes")}");
        }
        break;
    }

    case "cursor":
    {
        SurlFolderPage first = await server.Files.ListAsync("/mixed", null, 2);
        Console.WriteLine($"first: {string.Join(" ", first.Entries.Select(e => e.Name))}, more: {first.HasMore}");
        // Files arrive between the pages: one before the cursor, one after it.
        await server.Files.PutAsync("/mixed/a.txt", "x\n"u8.ToArray());
        await server.Files.PutAsync("/mixed/e.txt", "x\n"u8.ToArray());
        SurlFolderPage second = await server.Files.ListAsync("/mixed", first.Entries[^1].Name, 100);
        Console.WriteLine($"second: {string.Join(" ", second.Entries.Select(e => e.Name))}, more: {second.HasMore}");
        break;
    }

    case "limits":
    {
        foreach (int limit in new[] { 0, 1001 })
        {
            try
            {
                await server.Files.ListAsync("/", null, limit);
                Console.WriteLine($"limit {limit}: listed");
            }
            catch (SurlReadRefusedException failure)
            {
                Console.WriteLine($"limit {limit}: {failure.Message}");
            }
        }
        SurlFolderPage biggest = await server.Files.ListAsync("/", null, 1000);
        Console.WriteLine($"limit 1000: {biggest.Entries.Count} entries");
        try
        {
            await server.Files.ListAsync("/nowhere", null, 100);
        }
        catch (SurlNotFoundException failure)
        {
            Console.WriteLine($"/nowhere: {failure.GetType().Name}, {failure.Message}");
        }
        break;
    }
}
return 0;
