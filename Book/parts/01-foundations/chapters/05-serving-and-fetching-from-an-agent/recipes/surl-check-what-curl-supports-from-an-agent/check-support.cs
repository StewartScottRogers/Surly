// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent asks curl-mcp what this curl supports, checks the answer against curl -V, and asks surl-mcp
// whether it can serve the same schemes, over the official MCP SDK.
// Run: dotnet run check-support.cs [-- version|agree|plan]
//   version       the fields of the version tool's answer (the default)
//   agree         the version tool against curl -V, run through the curl tool
//   plan          for each scheme a plan needs, whether curl speaks it and whether surl serves it
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "version";

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

// curl-mcp: version takes no arguments and answers in text, as JSON.
CallToolResult versionResult = await curl.CallToolAsync("version", new Dictionary<string, object?>());
JsonElement version = JsonDocument.Parse(versionResult.Content.OfType<TextContentBlock>().Single().Text).RootElement;
string[] protocols = version.GetProperty("protocols").EnumerateArray().Select(p => p.GetString()!).ToArray();
string[] extended = version.GetProperty("extendedProtocols").EnumerateArray().Select(p => p.GetString()!).ToArray();
string[] features = version.GetProperty("features").EnumerateArray().Select(f => f.GetString()!).ToArray();

switch (which)
{
    case "version":
        Console.WriteLine($"version: {version.GetProperty("version")}");
        Console.WriteLine($"version line starts: {version.GetProperty("versionLine").GetString()!.StartsWith("curl " + version.GetProperty("version").GetString(), StringComparison.Ordinal)}");
        Console.WriteLine($"protocols: {protocols.Length > 0}, ftp {protocols.Contains("ftp")}, tftp {protocols.Contains("tftp")}, redis {protocols.Contains("redis")}");
        Console.WriteLine($"extended protocols: redis {extended.Contains("redis")}, ftp {extended.Contains("ftp")}");
        Console.WriteLine($"features: SSL {features.Contains("SSL")}");
        Console.WriteLine($"tls backend named: {version.GetProperty("tlsBackend").GetString()!.Length > 0}");
        break;
    case "agree":
        // curl -V through the curl tool: the same facts, as the command line prints them.
        CallToolResult dashV = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = new[] { "-V" } });
        string text = JsonDocument.Parse(dashV.Content.OfType<TextContentBlock>().Single().Text).RootElement
            .GetProperty("standardOutput").GetProperty("content").GetString()!;
        string[] lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        string[] listed = lines.First(l => l.StartsWith("Protocols: ", StringComparison.Ordinal))["Protocols: ".Length..].Split(' ');
        string[] listedFeatures = lines.First(l => l.StartsWith("Features: ", StringComparison.Ordinal))["Features: ".Length..].Split(' ');
        Console.WriteLine($"first line is the version line: {lines[0] == version.GetProperty("versionLine").GetString()}");
        Console.WriteLine($"every protocol of the tool is in curl -V: {protocols.All(listed.Contains)}");
        Console.WriteLine($"features are the same: {features.OrderBy(f => f).SequenceEqual(listedFeatures.OrderBy(f => f))}");
        break;
    case "plan":
        // The plan: which of these can this agent both serve and fetch?
        foreach (string scheme in new[] { "http", "ftp", "tftp", "dict", "redis", "carrier-pigeon" })
        {
            bool fetches = protocols.Contains(scheme) || extended.Contains(scheme);
            CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
            {
                ["listen_urls"] = new[] { $"{scheme}://127.0.0.1:0" },
            });
            Console.WriteLine($"{scheme}: curl {(fetches ? "yes" : "no")}, surl {(started.IsError == true ? "no" : "yes")}");
        }

        break;
}

return 0;
