// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests an Azure blob upload against a local stand-in: surl-mcp serves az as plain HTTP with a Bearer
// token, curl-mcp puts and gets blobs over http:// as an Azure client would, and read_file shows what was stored.
// Run: dotnet run program.cs [-- <case>]
//   put (default)  put a blob, then read the stored file with read_file
//   get-back       put a blob, then get it back
//   md5            compare the put answer's Content-MD5 with the upload's own hash
//   listed         list the container after a put
//   no-token, wrong-token, version-header, blob-type-header, missing-blob   refusals
//   uploads-off, uploads-off-reads   a server with uploads off: a put, then a get
//   plaintext-auth   a server without --allow-plaintext-auth
//   no-certificate   a server without --az-plaintext
#:package ModelContextProtocol.Core@2.4.0

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "put";
string note = "uploaded over az\n";

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

// surl-mcp: the stand-in. One Bearer token (an account with an empty name), uploads and listings on.
var surlArguments = new List<string> { "--user", ":t0ken-abc", "--list-directories" };
if (which != "no-certificate") surlArguments.Add("--az-plaintext");
if (which != "plaintext-auth") surlArguments.Add("--allow-plaintext-auth");
if (which is not ("uploads-off" or "uploads-off-reads")) surlArguments.Add("--allow-uploads");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "az://127.0.0.1:0" },
    ["arguments"] = surlArguments,
    ["files"] = new Dictionary<string, string> { ["photos/seed.txt"] = "already here\n" },
});
if (started.IsError == true)
{
    // A refused start carries surl's own exit code and message.
    Console.WriteLine($"start refused: exit {started.StructuredContent!.Value.GetProperty("exit_code")}, "
        + started.Content.OfType<TextContentBlock>().Single().Text.Split('\n')[0]);
    return 0;
}

string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
// bound_urls says az://127.0.0.1:port/; the plain-HTTP endpoint is the same address under http://.
string endpoint = $"http://127.0.0.1:{new Uri(started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!).Port}";
string token = which == "wrong-token" ? "t0ken-xyz" : "t0ken-abc";

switch (which)
{
    case "get-back":
        await Send("PUT", "/photos/note.txt", note);
        JsonElement got = await Send("GET", "/photos/note.txt");
        Console.WriteLine($"get: {got.GetProperty("statusCode")} {got.GetProperty("body").GetProperty("content").GetString()!.TrimEnd()}");
        break;

    case "md5":
        JsonElement stored = await Send("PUT", "/photos/note.txt", note);
        string mine = Convert.ToBase64String(MD5.HashData(Encoding.UTF8.GetBytes(note)));
        Console.WriteLine($"put: {stored.GetProperty("statusCode")}");
        Console.WriteLine($"Content-MD5: {Header(stored, "Content-MD5")}");
        Console.WriteLine($"the upload's own hash agrees: {Header(stored, "Content-MD5") == mine}");
        break;

    case "listed":
        await Send("PUT", "/photos/note.txt", note);
        JsonElement listing = await Send("GET", "/photos?restype=container&comp=list");
        string xml = listing.GetProperty("body").GetProperty("content").GetString()!;
        Console.WriteLine($"list: {listing.GetProperty("statusCode")}");
        foreach (string name in new[] { "note.txt", "seed.txt" })
        {
            Console.WriteLine($"{name} listed: {xml.Contains($"<Name>{name}</Name>")}");
        }

        break;

    case "missing-blob":
        JsonElement absent = await Send("GET", "/photos/other.txt");
        Console.WriteLine($"get: {absent.GetProperty("statusCode")} {Header(absent, "x-ms-error-code")}");
        break;

    case "uploads-off-reads":
        JsonElement seed = await Send("GET", "/photos/seed.txt");
        Console.WriteLine($"get: {seed.GetProperty("statusCode")} {seed.GetProperty("body").GetProperty("content").GetString()!.TrimEnd()}");
        break;

    default:
        // put, and every refusal of a put.
        JsonElement put = await Send("PUT", "/photos/note.txt", note);
        string code = Header(put, "x-ms-error-code");
        Console.WriteLine($"put: {put.GetProperty("statusCode")}{(code.Length > 0 ? " " + code : "")}");
        if (which == "put")
        {
            // The agent reads what the stand-in stored with surl-mcp's read_file, with no HTTP in between.
            CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = "/photos/note.txt" });
            string text = read.StructuredContent!.Value.GetProperty("text").GetString()!;
            Console.WriteLine($"stored {text.Length} bytes, identical to the upload: {text == note}");
        }

        break;
}

return 0;

// One request as an Azure client sends it: the REST version, the blob type for a put, the Bearer token.
// curl-mcp's transfer answers in text, as JSON; a refusal is an answer, not a tool error.
async Task<JsonElement> Send(string method, string path, string? upload = null)
{
    var headers = new JsonArray();
    if (which != "version-header") headers.Add(new JsonObject { ["name"] = "x-ms-version", ["value"] = "2023-11-03" });
    if (upload is not null && which != "blob-type-header") headers.Add(new JsonObject { ["name"] = "x-ms-blob-type", ["value"] = "BlockBlob" });
    if (which != "no-token") headers.Add(new JsonObject { ["name"] = "Authorization", ["value"] = $"Bearer {token}" });
    var request = new Dictionary<string, object?> { ["url"] = endpoint + path, ["method"] = method, ["headers"] = headers };
    if (upload is not null) request["body"] = new JsonObject { ["content"] = upload, ["isBase64"] = false };
    CallToolResult result = await curl.CallToolAsync("transfer", request);
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}

// The value of a response header, whatever its case, or an empty string.
string Header(JsonElement answer, string name) =>
    answer.GetProperty("headers").EnumerateArray()
        .FirstOrDefault(h => string.Equals(h.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase))
        is { ValueKind: JsonValueKind.Object } found ? found.GetProperty("value").GetString()! : "";
