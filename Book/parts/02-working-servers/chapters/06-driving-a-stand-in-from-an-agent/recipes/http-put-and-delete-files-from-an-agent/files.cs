// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent puts and deletes files on a running surl server through surl-mcp, and fetches them
// with curl through curl-mcp after each change. Run: dotnet run files.cs -- <case>
//   solution              put /reports/a.txt, fetch it, delete it, fetch it again
//   read-back             read_file and list_files after a put
//   overwrite             a second put_file on the same path
//   bytes-as-base64       bytes that are not text, put and read as base64
//   text-or-base64        put_file with neither text nor base64
//   client-needs-a-login  a client's DELETE, then delete_file, on hello.txt
//   nothing-to-delete     delete_file on a missing file and on a folder
//   hidden-refused        put_file on /.env
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "solution";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it.
string here = Directory.GetCurrentDirectory();
await using McpClient surl = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "surl-mcp", Command = "surl-mcp",
    EnvironmentVariables = new Dictionary<string, string?> { ["SURL_MCP_ROOT"] = here },
}));
await using McpClient curl = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "curl-mcp", Command = "curl-mcp",
    EnvironmentVariables = new Dictionary<string, string?> { ["CURL_MCP_ROOT"] = here },
}));

// A plain HTTP server on a free port, holding one file in memory.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "solution":
        await Put("/reports/a.txt", "text", "quarterly\n");
        await Fetch("reports/a.txt");
        await Delete("/reports/a.txt");
        await Fetch("reports/a.txt");
        break;
    case "read-back":
        await Put("/reports/a.txt", "text", "quarterly\n");
        await Read("/reports/a.txt");
        Report("list_files /", await surl.CallToolAsync("list_files", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["path"] = "/",
        }));
        break;
    case "overwrite":
        await Put("/reports/a.txt", "text", "quarterly\n");
        await Put("/reports/a.txt", "text", "revised\n");
        await Fetch("reports/a.txt");
        break;
    case "bytes-as-base64":
        await Put("/data.bin", "base64", "AAEC/w==");
        await Read("/data.bin");
        break;
    case "text-or-base64":
        await Put("/empty.txt", null, null);
        break;
    case "client-needs-a-login":
        // -X DELETE sends a DELETE; -w "%{http_code}" prints the status.
        CallToolResult deleted = await curl.CallToolAsync("curl", new Dictionary<string, object?>
        {
            ["args"] = new[] { "-s", "-X", "DELETE", "-w", "%{http_code}", url + "hello.txt" },
        });
        using (JsonDocument answer = JsonDocument.Parse(deleted.Content.OfType<TextContentBlock>().Single().Text))
        {
            Console.WriteLine($"curl -X DELETE hello.txt: exit {answer.RootElement.GetProperty("exitCode").GetInt32()}, "
                + answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString());
        }

        await Fetch("hello.txt");
        await Delete("/hello.txt");
        await Fetch("hello.txt");
        break;
    case "nothing-to-delete":
        await Put("/reports/a.txt", "text", "quarterly\n");
        await Delete("/reports/missing.txt");
        await Delete("/reports");
        break;
    case "hidden-refused":
        await Put("/.env", "text", "SECRET=1\n");
        break;
    default:
        Console.Error.WriteLine($"unknown case {which}");
        return 2;
}

return 0;

// put_file takes the content as text or as base64; it answers the path and its byte count.
async Task Put(string path, string? kind, string? content)
{
    var arguments = new Dictionary<string, object?> { ["server_id"] = server, ["path"] = path };
    if (kind is not null)
    {
        arguments[kind] = content;
    }

    Report("put_file " + path, await surl.CallToolAsync("put_file", arguments));
}

async Task Delete(string path) => Report("delete_file " + path,
    await surl.CallToolAsync("delete_file", new Dictionary<string, object?> { ["server_id"] = server, ["path"] = path }));

async Task Read(string path) => Report("read_file " + path,
    await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = server, ["path"] = path }));

// surl-mcp answers in structured content, printed here as JSON; a tool error is text that says why.
void Report(string call, CallToolResult result) => Console.WriteLine(result.IsError == true
    ? $"{call}: error: {result.Content.OfType<TextContentBlock>().Single().Text}"
    : $"{call}: {result.StructuredContent!.Value.GetRawText()}");

// transfer is curl-mcp's plain fetch; it answers the status and the body, in text, as JSON.
async Task Fetch(string path)
{
    CallToolResult result = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + path });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    int status = answer.RootElement.GetProperty("statusCode").GetInt32();
    string body = status == 200 ? " " + answer.RootElement.GetProperty("body").GetProperty("content").GetString()!.TrimEnd('\n') : "";
    Console.WriteLine($"transfer {path}: {status}{body}");
}
