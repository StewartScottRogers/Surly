// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests a gateway's CoAP observation: surl-mcp serves thermostat.txt over CoAP and HTTP, one curl tool
// call observes it with --coap-observe while another replaces the file, and the agent reads what the observer
// was sent.
// Run: dotnet run observe.cs -- <case>
//   coap        replace the file with a CoAP PUT (the default)
//   http        replace it with an HTTP PUT
//   put_file    replace it with surl-mcp's put_file
//   delete      delete it with a CoAP DELETE
//   no-uploads  the same CoAP PUT to a server without --allow-uploads
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string how = args.Length > 0 ? args[0] : "coap";

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

// CoAP and HTTP over one store holding thermostat.txt; clients with no login may write.
string[] options = how == "no-uploads" ? ["--allow-anonymous"] : ["--allow-anonymous", "--allow-uploads"];
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "coap://127.0.0.1:0", "http://127.0.0.1:0" },
    ["arguments"] = options,
    ["files"] = new Dictionary<string, string> { ["thermostat.txt"] = "setpoint=68" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string coap = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "thermostat.txt";
string http = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()! + "thermostat.txt";

// The gateway: a curl call that observes for at most 6 seconds. It runs while the agent goes on.
Task<CallToolResult> gateway = curl.CallToolAsync("curl", new Dictionary<string, object?>
{
    ["args"] = new[] { "-s", "-m", "6", "--coap-observe", coap },
}).AsTask();

// The observer is registered once the server has answered its exchange; wait for that, a tenth of a second at a time.
for (int tries = 0; !await Answered() && tries < 100; tries++)
{
    await Task.Delay(100);
}

// The file is replaced, or deleted, by another client.
if (how == "put_file")
{
    await surl.CallToolAsync("put_file", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = "/thermostat.txt", ["text"] = "setpoint=70" });
    Console.WriteLine("put_file done");
}
else
{
    string[] change = how switch
    {
        "http" => ["-s", "-S", "-T", "new.txt", "-w", "%{http_code}", http],
        "delete" => ["-s", "-S", "-i", "-X", "DELETE", coap],
        _ => ["-s", "-S", "-i", "-X", "PUT", "-d", "@new.txt", coap],
    };
    JsonElement changed = Answer(await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = change }));
    Console.WriteLine($"change answered {changed.GetProperty("standardOutput").GetProperty("content").GetString()!.Split('\r', '\n')[0]}");
}

// What the gateway was sent, a line per representation, and how its curl ended.
JsonElement seen = Answer(await gateway);
Console.Write(seen.GetProperty("standardOutput").GetProperty("content").GetString());
Console.WriteLine($"gateway exit {seen.GetProperty("exitCode").GetInt32()}");
return 0;

// get_exchanges records what crossed the wire; the CoAP exchange has a sent chunk once the registration is answered.
async Task<bool> Answered()
{
    CallToolResult got = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["scheme"] = "coap" });
    return got.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray()
        .Any(e => e.GetProperty("chunks").EnumerateArray().Any(c => c.GetProperty("direction").GetString() == "sent"));
}

// curl-mcp answers in text, as JSON, also when curl's exit is not 0.
static JsonElement Answer(CallToolResult result) =>
    JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
