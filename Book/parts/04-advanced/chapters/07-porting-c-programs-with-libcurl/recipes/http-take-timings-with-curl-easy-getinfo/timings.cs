// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// timings.c, which fetches each URL on one easy handle and reports libcurl's timers for it,
// ported call for call to C# with Libcurl. Each line names the C call it stands for.
// Run: dotnet run timings.cs -- <url>...
#:package Surly.Libcurl@0.4.0-preview.6

using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: dotnet run timings.cs -- <url>...");
    return 2;
}

// static size_t discard(char *data, size_t size, size_t nmemb, void *userdata) { return size * nmemb; }
WriteCallback discard = data => data.Length;

await using CurlLibrary library = CurlGlobal.Init(CurlGlobalFlags.Default); // curl_global_init(CURL_GLOBAL_DEFAULT)
using CurlEasy curl = library.EasyInit();                               // curl = curl_easy_init()
curl.SetOption(CurlOption.Writefunction, discard);                       // curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, discard)

CurlCode res = CurlCode.Ok;
foreach (string url in args)                                             // for(i = 1; i < argc; i++)
{
    curl.SetOption(CurlOption.Url, url);                                 // curl_easy_setopt(curl, CURLOPT_URL, argv[i])
    res = curl.Perform();                                                // res = curl_easy_perform(curl)
    if (res != CurlCode.Ok)
    {
        Console.Error.WriteLine($"curl_easy_perform() failed: {CurlErrorText.Easy(res)}"); // curl_easy_strerror(res)
        break;
    }

    curl.GetInfo(CurlInfo.ResponseCode, out long code);                  // curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &code)
    curl.GetInfo(CurlInfo.NumConnects, out long connects);               // curl_easy_getinfo(curl, CURLINFO_NUM_CONNECTS, &connects)
    curl.GetInfo(CurlInfo.ConnectTimeT, out long connect);               // curl_off_t, microseconds: CURLINFO_CONNECT_TIME_T
    curl.GetInfo(CurlInfo.PretransferTimeT, out long pretransfer);       // CURLINFO_PRETRANSFER_TIME_T
    curl.GetInfo(CurlInfo.StarttransferTimeT, out long starttransfer);   // CURLINFO_STARTTRANSFER_TIME_T
    curl.GetInfo(CurlInfo.TotalTimeT, out long total);                   // CURLINFO_TOTAL_TIME_T
    curl.GetInfo(CurlInfo.StarttransferTime, out double starttransferSeconds); // the older double: CURLINFO_STARTTRANSFER_TIME

    // printf("%s %ld connects %ld connect %s pretransfer %s starttransfer %s total %s (%.6f)\n", ...)
    Console.WriteLine($"{new Uri(url).AbsolutePath} {code} connects {connects} connect {Seconds(connect)} pretransfer {Seconds(pretransfer)} " +
                      $"starttransfer {Seconds(starttransfer)} total {Seconds(total)} ({starttransferSeconds:F6})");
}

return (int)res;                                                         // curl_easy_cleanup and curl_global_cleanup run as the usings end

// CURL_FORMAT_CURL_OFF_T ".%06ld": whole seconds, then the microseconds.
static string Seconds(long microseconds) => $"{microseconds / 1000000}.{microseconds % 1000000:D6}";
