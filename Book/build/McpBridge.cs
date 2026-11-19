// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
// The MCP client verify.py uses (ADR-0008 item 7): the official C# SDK, so the hosts are
// judged by a real client, not by one the book wrote. Neither surl-mcp nor curl-mcp uses this
// SDK, which makes it an independent check that they speak the protocol.
//
//   dotnet run McpBridge.cs -- <host command> [host arguments...]
//
// It starts the host over stdio, in the current directory and environment, and writes one
// JSON line: {"name","version","protocolVersion","tools":{name: inputSchema}}. Then, for each
// line {"tool","arguments"} read from standard input, it calls the tool and writes one line:
// {"isError","structured","text"}, or {"failure"} when the call failed below the tool (a
// JSON-RPC error, a closed host). End of input stops the host.
#:package ModelContextProtocol.Core@2.2.0

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: dotnet run McpBridge.cs -- <host command> [host arguments...]");
    return 2;
}

// The host is started here rather than by the SDK's StdioClientTransport, which on Windows
// runs it through cmd.exe /c: cmd's quote rules then break any command or argument with a
// space in it (C:\Users\First Last\...). ArgumentList passes each argument as it is; the
// SDK still does all of the protocol over the host's standard input and output.
var start = new ProcessStartInfo(args[0])
{
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
    WorkingDirectory = Directory.GetCurrentDirectory(),
};
foreach (string argument in args[1..])
{
    start.ArgumentList.Add(argument);
}
using Process host = Process.Start(start)!;
host.ErrorDataReceived += (_, line) => { if (line.Data is not null) Console.Error.WriteLine(line.Data); };
host.BeginErrorReadLine();
var transport = new StreamClientTransport(host.StandardInput.BaseStream, host.StandardOutput.BaseStream);

var output = Console.Out;
output.NewLine = "\n";
void Write(JsonNode line) => output.WriteLine(line.ToJsonString());

await using McpClient client = await McpClient.CreateAsync(transport);

var tools = new JsonObject();
foreach (McpClientTool tool in await client.ListToolsAsync())
{
    tools[tool.Name] = JsonNode.Parse(tool.ProtocolTool.InputSchema.GetRawText());
}
Write(new JsonObject
{
    ["name"] = client.ServerInfo.Name,
    ["version"] = client.ServerInfo.Version,
    ["protocolVersion"] = client.NegotiatedProtocolVersion,
    ["tools"] = tools,
});
output.Flush();

string? request;
while ((request = Console.In.ReadLine()) is not null)
{
    if (request.Length == 0)
    {
        continue;
    }
    JsonNode call = JsonNode.Parse(request)!;
    var arguments = new Dictionary<string, object?>();
    foreach (var (name, value) in call["arguments"]?.AsObject() ?? new JsonObject())
    {
        arguments[name] = value is null ? null : JsonDocument.Parse(value.ToJsonString()).RootElement.Clone();
    }
    try
    {
        CallToolResult result = await client.CallToolAsync(call["tool"]!.GetValue<string>(), arguments);
        string text = string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        Write(new JsonObject
        {
            ["isError"] = result.IsError ?? false,
            ["structured"] = result.StructuredContent is { } structured ? JsonNode.Parse(structured.GetRawText()) : null,
            ["text"] = text,
        });
    }
    catch (McpProtocolException e)
    {
        Write(new JsonObject { ["failure"] = $"{(int)e.ErrorCode} {e.Message}" });
    }
    catch (Exception e) when (e is McpException or IOException or InvalidOperationException)
    {
        Write(new JsonObject { ["failure"] = $"{e.GetType().Name}: {e.Message}" });
    }
    output.Flush();
}

// End of input: close the session, then give the host a moment to exit on its own.
await client.DisposeAsync();
host.StandardInput.Close();
if (!host.WaitForExit(5000))
{
    host.Kill(entireProcessTree: true);
}
return 0;
