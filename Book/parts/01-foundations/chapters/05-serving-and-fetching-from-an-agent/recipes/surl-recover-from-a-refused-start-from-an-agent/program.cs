// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent's start_server is refused; it reads surl's exit code and message, changes the one thing
// they name, starts again, and fetches a file to show the server is up.
// Run: dotnet run program.cs [-- <case>]
//   no-certificate  https with no certificate (the default)
//   bad-option      --max-time with a value that is not a number
//   no-directory    --directory naming a folder that does not exist
//   path-in-url     a listen URL with a path
//   unknown-scheme  a scheme surl does not serve, which the agent does not know how to fix
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "no-certificate";

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

// The first try, with one mistake in it.
string listen = which switch
{
    "no-certificate" => "https://127.0.0.1:0",
    "path-in-url" => "http://127.0.0.1:0/site/",
    "unknown-scheme" => "htp://127.0.0.1:0",
    _ => "http://127.0.0.1:0",
};
List<string> arguments = which switch
{
    "bad-option" => ["--max-time", "soon"],
    "no-directory" => ["--directory", "sites"],
    _ => [],
};

for (int attempt = 1; ; attempt++)
{
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { listen },
        ["arguments"] = arguments,
        ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
    });
    JsonElement answer = started.StructuredContent!.Value;
    if (started.IsError != true)
    {
        string url = answer.GetProperty("bound_urls")[0].GetString()!;
        Console.WriteLine($"start {attempt}: listening");
        var request = new Dictionary<string, object?> { ["url"] = url + "hello.txt", ["timeoutSeconds"] = 10 };
        if (url.StartsWith("https:")) request["insecure"] = true;   // a throwaway certificate nobody signed
        CallToolResult fetched = await curl.CallToolAsync("transfer", request);
        JsonElement got = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
        Console.Write($"fetched: {got.GetProperty("statusCode")} {got.GetProperty("body").GetProperty("content").GetString()}");
        return 0;
    }

    // A refusal: surl's exit code, its name, and surl's own lines.
    int code = answer.GetProperty("exit_code").GetInt32();
    string name = answer.GetProperty("exit_code_name").GetString()!;
    string message = answer.GetProperty("lines")[0].GetString()!;
    Console.WriteLine($"start {attempt}: refused, {code} {name}: {message}");
    if (attempt == 2)
    {
        Console.WriteLine("giving up after one fix");
        return code;
    }

    // One fix per code, read from surl --aihelp exit-codes; anything else is passed on.
    string? fix = name switch
    {
        "CertificateProblem" => "add --self-signed, a throwaway certificate for a test",
        "FailedInit" when message.EndsWith("expected a proper numerical parameter") => $"give {OptionNamed(message)} a number of seconds, 30",
        "CouldNotReadFile" => "drop --directory and serve the file from memory",
        "MalformedUrl" => "write the listen URL with no path",
        _ => null,
    };
    if (fix is null)
    {
        Console.WriteLine($"no fix known for {name}: passing exit {code} on");
        return code;
    }

    Console.WriteLine($"fix: {fix}");
    switch (name)
    {
        case "CertificateProblem":
            arguments.Add("--self-signed");
            break;
        case "FailedInit":
            arguments[arguments.IndexOf(OptionNamed(message)) + 1] = "30";
            break;
        case "CouldNotReadFile":
            arguments.RemoveRange(arguments.IndexOf("--directory"), 2);
            break;
        case "MalformedUrl":
            listen = new Uri(listen).GetLeftPart(UriPartial.Authority);
            break;
    }
}

// The option an exit-2 message names: "option --max-time: expected a proper numerical parameter".
static string OptionNamed(string message) => Regex.Match(message, @"option (\S+):").Groups[1].Value;
