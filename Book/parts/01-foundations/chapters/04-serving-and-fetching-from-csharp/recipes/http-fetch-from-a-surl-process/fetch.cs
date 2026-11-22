// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Fetches one URL with Curl.Api, from a server running somewhere else.
// Run: dotnet run fetch.cs -- <url>
#:package Surly.Curl.Api@*

using Curl.Api;
using Curl.Api.Errors;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: dotnet run fetch.cs -- <url>");
    return 2;
}

await using CurlClient client = CurlClient.Create();
try
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, args[0]), CancellationToken.None);
    await using Stream body = response.Body;
    if (response.StatusCode >= 400)
    {
        // Like curl -f: an error status is reported, not printed, and the exit code is 22.
        Console.Error.WriteLine($"{response.StatusCode} {response.ReasonPhrase}");
        return 22;
    }

    Console.Write(await new StreamReader(body).ReadToEndAsync());
    return 0;
}
catch (CurlException failure)
{
    // No answer at all: the exit code is curl's own for the same failure.
    Console.Error.WriteLine($"curl error {(int)failure.Code} ({failure.Code}): {failure.Message}");
    return (int)failure.Code;
}
