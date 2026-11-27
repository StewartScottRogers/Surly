// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent plans a client's move from FTP to SFTP from what the client really did: surl-mcp serves both
// over one store, curl-mcp's curl tool plays the old FTP job, and the agent reads the FTP commands from
// get_exchanges, tries the SFTP equivalent of each, and reports the gaps. server.pem, server.key,
// host_key and up.txt are in the folder.
// Run: dotnet run plan-move.cs -- <case>
//   plan       the job, the commands it used, a trial of each over SFTP and the gaps (the default)
//   commands   the job, then every command of each FTP exchange
//   workaround the rename gap, worked around: rm the old name first, then rename
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "plan";

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

// FTP (TLS on request) and SFTP over one in-memory store: the job's account, uploads and listings on,
// tonight's report, yesterday's upload, the job's log and a file to clear away.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0", "sftp://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key", "--hostkey", "host_key", "--user", "job:s3cret", "--allow-uploads", "--list-directories" },
    ["files"] = new Dictionary<string, string>
    {
        ["out/report.csv"] = "region,total\r\nnorth,1200\r\n", ["in/upload.csv"] = "yesterday\n", ["in/log.txt"] = "run 1\n", ["in/old.csv"] = "old\n",
    },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string[] urls = started.StructuredContent!.Value.GetProperty("bound_urls").EnumerateArray().Select(u => u.GetString()!).ToArray();
string ftp = urls[0], sftp = urls[1];

// The old job, as it runs every night: one curl call per step, FTP with TLS.
string[] f = ["-sS", "--ssl-reqd", "--cacert", "server.pem", "-u", "job:s3cret"];
await Curl([.. f, "-l", ftp + "out/"]);
await Curl([.. f, "-o", "report.csv", ftp + "out/report.csv"]);
await Curl([.. f, "-T", "up.txt", ftp + "in/upload.tmp"]);
await Curl([.. f, "-T", "up.txt", "--append", ftp + "in/log.txt"]);
await Curl([.. f, "-Q", "RNFR in/upload.tmp", "-Q", "RNTO in/upload.csv", "-Q", "DELE in/old.csv", "-l", ftp + "in/"]);
await Curl([.. f, "-I", ftp + "out/report.csv"]);

// What the job really sent: the commands of every FTP exchange, less the login and session ones.
CallToolResult got = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["scheme"] = "ftp" });
var session = new HashSet<string> { "USER", "PASS", "AUTH", "PBSZ", "PROT", "PWD", "CWD", "EPSV", "PASV", "TYPE", "QUIT", "REST" };
var used = new List<string>();
foreach (JsonElement exchange in got.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray())
{
    // A file's bytes travel between the server's 150 and its next reply; the rest the client sent are commands.
    var commands = new List<string>();
    bool data = false;
    foreach (JsonElement chunk in exchange.GetProperty("chunks").EnumerateArray())
    {
        string text = chunk.GetProperty("data").GetString()!;
        if (chunk.GetProperty("direction").GetString() == "sent")
        {
            data = text.StartsWith("150");
        }
        else if (!data)
        {
            commands.AddRange(text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split(' ')[0].ToUpperInvariant()));
        }
    }

    if (which == "commands")
    {
        Console.WriteLine(string.Join(" ", commands));
    }

    used.AddRange(commands.Where(c => !session.Contains(c)));
}

used = used.Distinct().ToList();
Console.WriteLine($"FTP commands the job used: {string.Join(", ", used)}");
if (which == "commands")
{
    CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = "/in/upload.csv" });
    Console.WriteLine($"in/upload.csv after RNTO: {read.StructuredContent!.Value.GetProperty("text").GetString()!.Trim()}");
    return 0;
}

// Back to the start, so each SFTP trial meets the store the job met.
await surl.CallToolAsync("reset_server", new Dictionary<string, object?> { ["server_id"] = id });

// The SFTP equivalent of each command, and how to tell it worked.
string[] s = ["-sS", "-k", "-u", "job:s3cret"];
var trials = new Dictionary<string, (string What, string[] Args, Func<int, string, bool> Worked)>
{
    ["NLST"] = ("-l", [.. s, "-l", sftp + "out/"], (code, output) => code == 0 && output.Contains("report.csv")),
    ["RETR"] = ("get", [.. s, "-o", "report.csv", sftp + "out/report.csv"], (code, _) => code == 0),
    ["SIZE"] = ("long listing", [.. s, sftp + "out/"], (code, output) => code == 0 && output.Contains(" 26 ")),
    ["STOR"] = ("-T", [.. s, "-T", "up.txt", sftp + "in/upload.tmp"], (code, _) => code == 0),
    ["APPE"] = ("-T --append", [.. s, "-T", "up.txt", "--append", sftp + "in/log.txt"], (code, _) => code == 0),
    ["RNFR"] = ("rename onto an existing name", [.. s, "-Q", "rename /in/upload.tmp /in/upload.csv", "-l", sftp + "in/"], (code, _) => code == 0),
    ["DELE"] = ("rm", [.. s, "-Q", "rm /in/old.csv", "-l", sftp + "in/"], (code, _) => code == 0),
    ["MDTM"] = ("-I", [.. s, "-I", sftp + "out/report.csv"], (code, output) => code == 0 && output.Contains("Last-Modified")),
};
if (which == "workaround")
{
    await Curl([.. s, "-T", "up.txt", sftp + "in/upload.tmp"]);
    trials["RNFR"] = ("rm, then rename", [.. s, "-Q", "rm /in/upload.csv", "-Q", "rename /in/upload.tmp /in/upload.csv", "-l", sftp + "in/"], (code, _) => code == 0);
    trials["MDTM"] = ("long listing", [.. s, sftp + "out/"], (code, output) => code == 0 && output.TrimEnd().EndsWith(" report.csv"));
    used = ["RNFR", "MDTM"];
}

var gaps = new List<string>();
foreach (string command in used)
{
    if (command == "RNTO")
    {
        continue;
    }

    (string what, string[] argv, Func<int, string, bool> worked) = trials[command];
    (int code, string output, string error) = await Curl(argv);
    bool ok = worked(code, output);
    Console.WriteLine($"{command}: sftp {what}, exitCode {code}, {(ok ? "works" : "GAP" + (error.Length > 0 ? ": " + error : ", no Last-Modified"))}");
    if (which == "workaround" && command == "MDTM")
    {
        Console.WriteLine($"  {output.TrimEnd()}");
    }

    if (!ok)
    {
        gaps.Add(command == "RNFR" ? "RNFR/RNTO" : command);
    }
}

Console.WriteLine($"gaps: {(gaps.Count == 0 ? "none" : string.Join(", ", gaps))}");
return 0;

// One curl tool call: its exit code, standard output and standard error.
async Task<(int, string, string)> Curl(string[] argv)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = argv });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return (answer.RootElement.GetProperty("exitCode").GetInt32(),
        answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!,
        answer.RootElement.GetProperty("standardError").GetProperty("content").GetString()!.Trim());
}
