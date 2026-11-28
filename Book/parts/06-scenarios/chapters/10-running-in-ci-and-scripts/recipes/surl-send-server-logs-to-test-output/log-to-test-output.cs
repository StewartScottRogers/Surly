// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Each test gets its own in-process server whose log goes to the test's output, line by line,
// so a failing test shows what the server saw. A small runner stands in for the test framework:
// it keeps each test's output and prints it only when the test fails, as xUnit and NUnit do.
// Run: dotnet run log-to-test-output.cs [-- verbose|info]
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Surl.Api;
using Surl.Output;

LogLevel level = args.Length > 0 && args[0] == "info" ? LogLevel.Info : LogLevel.Verbose;

int failed = 0;
failed += await Run("fetches the report", output => FetchTest(output, "report.csv"));
failed += await Run("fetches the summary", output => FetchTest(output, "summary.csv"));
return failed == 0 ? 0 : 1;

// One test: a server with report.csv, its log in this test's output; fetch a path, expect 200.
async Task FetchTest(Action<string> output, string path)
{
    var options = new SurlServerOptions
    {
        ListenUrls = { "http://127.0.0.1:0" },
        Log = new TestOutputWriter(output),
        LogLevel = level,
    };
    options.Files["report.csv"] = "id,total\n1,40\n"u8.ToArray();
    await using SurlServer server = await SurlServer.StartAsync(options);

    await using CurlClient client = CurlClient.Create();
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
    await response.Body.DisposeAsync();
    if (response.StatusCode != 200)
    {
        throw new Exception($"expected 200 for {path}, got {response.StatusCode}");
    }
}

// The stand-in framework: runs a test with its own output, reports it, and shows the output of a failure.
static async Task<int> Run(string name, Func<Action<string>, Task> test)
{
    var output = new List<string>();
    try
    {
        await test(line => { lock (output) { output.Add(line); } });
        Console.WriteLine($"PASS {name}");
        return 0;
    }
    catch (Exception failure)
    {
        Console.WriteLine($"FAIL {name}: {failure.Message}");
        foreach (string line in output)
        {
            Console.WriteLine($"  {line}");
        }

        return 1;
    }
}

// A TextWriter that hands each whole line to the test's output, such as xUnit's
// ITestOutputHelper.WriteLine. The server writes from several threads, so it locks.
sealed class TestOutputWriter(Action<string> writeLine) : TextWriter
{
    private readonly StringBuilder _line = new();

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        lock (_line)
        {
            if (value == '\n')
            {
                writeLine("[surl] " + _line.ToString().TrimEnd('\r'));
                _line.Clear();
            }
            else
            {
                _line.Append(value);
            }
        }
    }

    public override void Write(string? value)
    {
        lock (_line)
        {
            foreach (char c in value ?? "")
            {
                Write(c);
            }
        }
    }

    public override void WriteLine(string? value)
    {
        lock (_line)
        {
            Write(value);
            Write('\n');
        }
    }
}
