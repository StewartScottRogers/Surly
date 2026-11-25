// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests a client that keeps a Bearer token between calls: it starts a registry through
// surl-mcp, calls through curl-mcp, revokes every token between two calls, and checks the client
// sees 401, logs in again for a new token and retries once. Then it waits for the recorded logins.
// Run: dotnet run token-session.cs [-- <mode>]
//   (no argument)  two calls with a revocation between them
//   steady         two calls, no revocation
//   broken         a fault answers every API call 401, even after a new login
//   guess          the client starts with a stale token and a wrong password
//   endpoint       what the token endpoint answers, a call with no token, and a fixed token account
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "";

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

// The server: a registry over TLS with the certificate in this folder, whose token endpoint
// issues a new token at every login.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "oci://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key", "--user", "app:s3cret" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string baseUrl = $"https://127.0.0.1:{new Uri(started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!).Port}/";
if (mode == "broken")
{
    await Tool("add_fault", new() { ["fault"] = "error;scheme=oci;path=/v2/;reply=401 UNAUTHORIZED" });
}

if (mode == "endpoint")
{
    using (JsonDocument answer = JsonDocument.Parse((await Curl("-sf", "-u", "app:s3cret", baseUrl + "token?service=surl")).Output))
    {
        Console.WriteLine($"token endpoint: expires_in {answer.RootElement.GetProperty("expires_in")}");
    }

    string headers = (await Curl("-s", "-D", "-", "-o", "api.out", baseUrl + "v2/")).Output;
    Console.WriteLine($"no token: {headers.Split('\n')[0].Trim()}, {headers.Split('\n').First(l => l.StartsWith("WWW-Authenticate:")).Trim()}");
    // A fixed Bearer token is an account with an empty user name; add_account adds one, and the
    // registry, which takes only the tokens it issues, still refuses it.
    string fixedToken;
    try
    {
        CallToolResult result = await surl.CallToolAsync("add_account", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["user"] = "", ["password"] = "tok-2",
        });
        fixedToken = result.IsError == true ? "refused" : "added";
    }
    catch (McpException failure)
    {
        fixedToken = $"refused, {failure.GetType().Name}";
    }

    Console.WriteLine($"add_account with an empty user: {fixedToken}");
    Console.WriteLine($"the fixed token at the registry: {(await Curl("-s", "--oauth2-bearer", "tok-2", "-o", "api.out", "-w", "%{http_code}", baseUrl + "v2/")).Output}");
    return 0;
}

// The client under test keeps its token here: a stale one in guess mode.
string? token = mode == "guess" ? "tok-from-yesterday" : null;
string password = mode == "guess" ? "guess" : "s3cret";
int logins = 0;

int exitCode = await CallApi();
if (exitCode == 0 && mode is "" or "steady")
{
    if (mode == "")
    {
        await Tool("reset_server", new());
        Console.WriteLine("reset: every token revoked");
    }

    exitCode = await CallApi();
}

// The logins the server judged: wait, at most 10 seconds each, for as many as were made, then list them.
long after = 0;
for (int i = 0; i < logins; i++)
{
    JsonElement waited = await Tool("wait_for_event", new()
    {
        ["kinds"] = new[] { "login_judged" }, ["after_sequence"] = after, ["timeout_ms"] = 10000,
    });
    after = waited.GetProperty("event").GetProperty("sequence").GetInt64();
}

JsonElement events = await Tool("get_events", new() { ["kinds"] = new[] { "login_judged" } });
foreach (JsonElement login in events.GetProperty("events").EnumerateArray())
{
    Console.WriteLine($"recorded: {login.GetProperty("account")} {login.GetProperty("mechanism")} {(login.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused")}");
}

return exitCode;

// The client: with no token it logs in for one; when the call is answered 401 it logs in again
// for a new token and retries once.
async Task<int> CallApi()
{
    if (token is null && !await LogIn())
    {
        return 1;
    }

    string status = await Call();
    Console.WriteLine($"call: {status}");
    if (status == "401")
    {
        if (!await LogIn())
        {
            return 1;
        }

        status = await Call();
        Console.WriteLine($"retry: {status}");
    }

    return status == "200" ? 0 : 1;
}

// GET /token with Basic (curl -u names no method); the token from the JSON answer.
async Task<bool> LogIn()
{
    logins++;
    (int code, string output) = await Curl("-sf", "-u", $"app:{password}", baseUrl + "token?service=surl");
    if (code != 0)
    {
        Console.WriteLine($"login: curl exit {code}");
        return false;
    }

    string? old = token;
    using JsonDocument answer = JsonDocument.Parse(output);
    token = answer.RootElement.GetProperty("token").GetString();
    Console.WriteLine(old is null ? "login: token issued" : old == token ? "login: same token" : "login: new token");
    return true;
}

// The API call with the kept token as a Bearer token: the status curl wrote.
async Task<string> Call() =>
    (await Curl("-s", "--oauth2-bearer", token!, "-o", "api.out", "-w", "%{http_code}", baseUrl + "v2/")).Output;

// curl-mcp's curl tool, trusting server.pem: exit code and output.
async Task<(int Code, string Output)> Curl(params string[] more)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "--cacert", "server.pem" }.Concat(more).ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement output = answer.RootElement.GetProperty("standardOutput");
    return (answer.RootElement.GetProperty("exitCode").GetInt32(),
            output.ValueKind == JsonValueKind.Object ? output.GetProperty("content").GetString() ?? "" : "");
}

// A surl-mcp tool on this server; its structured answer.
async Task<JsonElement> Tool(string name, Dictionary<string, object?> more)
{
    more["server_id"] = id;
    CallToolResult result = await surl.CallToolAsync(name, more);
    if (result.IsError == true)
    {
        throw new InvalidOperationException($"{name}: {result.Content.OfType<TextContentBlock>().First().Text}");
    }

    return result.StructuredContent!.Value;
}
