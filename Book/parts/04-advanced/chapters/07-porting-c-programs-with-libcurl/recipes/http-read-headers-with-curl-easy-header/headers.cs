// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// headers.c, which fetches a URL and then reads the response headers with libcurl's header API,
// ported call for call to C# with Libcurl. Each line names the C call it stands for.
// Run: dotnet run headers.cs -- <url> <header name>...
#:package Surly.Libcurl@0.4.0-preview.5

using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Headers;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Headers;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: dotnet run headers.cs -- <url> <header name>...");
    return 2;
}

WriteCallback discard = data => data.Length;                             // the body is not wanted

await using CurlLibrary library = CurlGlobal.Init(CurlGlobalFlags.Default); // curl_global_init(CURL_GLOBAL_DEFAULT)
using CurlEasy curl = library.EasyInit();                               // curl = curl_easy_init()

// Before any transfer there are no headers to ask about.
CurlHeaderCode before = CurlEasyHeaders.Header(curl, "Content-Type", 0, CurlHeaderOrigin.Header, -1, out _);
Console.WriteLine($"before perform: {(int)before} {before}");

curl.SetOption(CurlOption.Url, args[0]);                                 // curl_easy_setopt(curl, CURLOPT_URL, argv[1])
curl.SetOption(CurlOption.Writefunction, discard);                       // curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, discard)
CurlCode res = curl.Perform();                                           // res = curl_easy_perform(curl)
if (res != CurlCode.Ok)
{
    Console.Error.WriteLine($"curl_easy_perform() failed: {CurlErrorText.Easy(res)}"); // curl_easy_strerror(res)
    return (int)res;
}

// Each name asked for: curl_easy_header(curl, name, 0, CURLH_HEADER, -1, &h).
foreach (string name in args[1..])
{
    CurlHeaderCode hc = CurlEasyHeaders.Header(curl, name, 0, CurlHeaderOrigin.Header, -1, out CurlHeader? h);
    Console.WriteLine(hc == CurlHeaderCode.Ok
        ? $"{name}: {h!.Name}: {h.Value} (amount {h.Amount}, index {h.Index})"   // printf("%s: %s\n", h->name, h->value)
        : $"{name}: {(int)hc} {hc}");                                         // CURLHE_MISSING and the rest
}

// A second Server header, and a request this handle never made.
CurlHeaderCode second = CurlEasyHeaders.Header(curl, "Server", 1, CurlHeaderOrigin.Header, -1, out _);   // CURLHE_BADINDEX
Console.WriteLine($"Server index 1: {(int)second} {second}");
CurlHeaderCode later = CurlEasyHeaders.Header(curl, "Server", 0, CurlHeaderOrigin.Header, 5, out _);     // CURLHE_NOREQUEST
Console.WriteLine($"Server request 5: {(int)later} {later}");

// Every header, in the order they came: while((h = curl_easy_nextheader(curl, CURLH_HEADER, -1, prev))) ...
// Date, ETag and Last-Modified change from run to run, so the walk prints only their names.
CurlHeader? prev = null;
while ((prev = CurlEasyHeaders.NextHeader(curl, CurlHeaderOrigin.Header, -1, prev!)) is not null)
{
    Console.WriteLine(prev.Name is "Date" or "ETag" or "Last-Modified" ? $"walk: {prev.Name}" : $"walk: {prev.Name}: {prev.Value}");
}

return 0;                                                                // curl_easy_cleanup and curl_global_cleanup run as the usings end
