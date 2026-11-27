// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent mails a report as an attachment and checks it survived: surl-mcp runs the mail server,
// curl-mcp's curl tool builds the message from -F parts and sends it, and read_message gives the
// stored message, whose attachment the agent decodes and compares with the file.
// Run: dotnet run send-report.cs -- <case>
//   check    send, then decode the attachment and compare it with report.csv (the default)
//   top      the same send, then the message's own header
//   parts    the same send, then each part's header
//   as-is    the report attached with no type= or encoder=, then each part's header
//   missing  the report attached from a file that is not there
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "check";

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

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "app@example.com:apppass", "--mailbox", "ann@example.com:annpass" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The send: one -F for the text and one for the file, the headers with -H.
string file = which switch
{
    "as-is" => "=@report.csv",
    "missing" => "=@nosuch.csv;type=text/csv;encoder=base64",
    _ => "=@report.csv;type=text/csv;encoder=base64",
};
CallToolResult sent = await curl.CallToolAsync("curl", new Dictionary<string, object?>
{
    ["args"] = new[]
    {
        "-s", "-S", "-u", "app@example.com:apppass", "--mail-from", "app@example.com", "--mail-rcpt", "ann@example.com",
        "-H", "Subject: Nightly build report", "-H", "From: app@example.com", "-H", "To: ann@example.com",
        "-F", "=The nightly build report is attached.", "-F", file, "-w", "%{response_code}", smtp + "example.com",
    },
});
using JsonDocument answer = JsonDocument.Parse(sent.Content.OfType<TextContentBlock>().Single().Text);
int exit = answer.RootElement.GetProperty("exitCode").GetInt32();
if (exit != 0)
{
    Console.WriteLine($"curl exit {exit}: {answer.RootElement.GetProperty("standardError").GetProperty("content").GetString()!.Trim()}");
    return 0;
}

Console.WriteLine($"curl exit 0, reply {answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()}");

// Delivery is asynchronous: wait for Ann's, with a bound, then read the stored message.
CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["name"] = "ann@example.com", ["timeout_ms"] = 10000,
});
if (waited.StructuredContent!.Value.TryGetProperty("timed_out", out _))
{
    Console.WriteLine("no delivery to ann@example.com within 10 s");
    return 1;
}

CallToolResult read = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
{
    ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX", ["uid"] = 1,
});
string text = read.StructuredContent!.Value.GetProperty("text").GetString()!;

// The MIME parts: the message's header names the boundary; each part is a header, a blank line and a body.
int end = text.IndexOf("\r\n\r\n");
string header = text[..end];
string boundary = Regex.Match(header, "boundary=\"?([^\"\r\n;]+)").Groups[1].Value;
List<(string Header, string Body)> parts = text[(end + 4)..].Split("--" + boundary)
    .Skip(1).TakeWhile(p => !p.StartsWith("--"))
    .Select(p => p[2..^2])
    .Select(p => p.StartsWith("\r\n") ? ("", p[2..]) : (p[..p.IndexOf("\r\n\r\n")], p[(p.IndexOf("\r\n\r\n") + 4)..]))
    .ToList();

switch (which)
{
    case "top":
        header.Split("\r\n").Where(l => Regex.IsMatch(l, "^(Content-Type|Mime-Version|Subject|From|To):")).ToList().ForEach(Console.WriteLine);
        break;
    case "parts" or "as-is":
        for (int i = 0; i < parts.Count; i++)
        {
            Console.WriteLine($"part {i + 1}: {(parts[i].Header == "" ? "no header" : parts[i].Header.Replace("\r\n", " | "))}");
        }

        break;
    default:
        // The agent's check: the part marked as an attachment, decoded from base64, is report.csv byte for byte.
        (string Header, string Body) part = parts.Single(p => p.Header.Contains("Content-Disposition: attachment"));
        byte[] decoded = Convert.FromBase64String(part.Body);
        byte[] report = File.ReadAllBytes(Path.Combine(here, "report.csv"));
        string name = Regex.Match(part.Header, "filename=\"([^\"]+)\"").Groups[1].Value;
        Console.WriteLine(decoded.AsSpan().SequenceEqual(report) ? $"{name}: {decoded.Length} bytes, the same as the file" : $"{name} differs from the file");
        break;
}

return 0;
