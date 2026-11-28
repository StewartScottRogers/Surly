// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// fetch.c, which downloads one file over FTP and gives up from its progress callback when the
// download takes longer than a time limit, ported call for call to C# with Libcurl.
// Each line names the C call it stands for.
// Run: dotnet run fetch.cs -- <url> <seconds> [quiet]
#:package Surly.Libcurl@0.4.0-preview.7

using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: dotnet run fetch.cs -- <url> <seconds> [quiet]");
    return 2;
}

long limit = long.Parse(args[1]);                                        // long limit = atol(argv[2]): seconds; 0 is no limit
bool quiet = args.Length > 2 && args[2] == "quiet";                      // "quiet" leaves CURLOPT_NOPROGRESS at its default, 1
long received = 0;
long calls = 0;
System.Diagnostics.Stopwatch start = new();                              // time_t start

await using CurlLibrary library = CurlGlobal.Init(CurlGlobalFlags.Default); // curl_global_init(CURL_GLOBAL_DEFAULT)
using CurlEasy curl = library.EasyInit();                               // curl = curl_easy_init()

// static size_t write_cb(char *data, size_t size, size_t nmemb, void *userdata): count and drop
WriteCallback write = data =>
{
    received += data.Length;
    return data.Length;
};

// static int xferinfo(void *p, curl_off_t dltotal, curl_off_t dlnow, curl_off_t ultotal, curl_off_t ulnow)
TransferInfoCallback xferinfo = (dltotal, dlnow, ultotal, ulnow) =>
{
    calls++;
    long elapsed = (long)start.Elapsed.TotalSeconds;                     // time_t elapsed = time(NULL) - start
    if (limit > 0 && elapsed > limit && (dltotal == 0 || dlnow < dltotal))
    {
        Console.WriteLine($"giving up after {elapsed} s with {dlnow} of {dltotal} bytes"); // printf(...)
        return 1;                                                        // non-zero: abort the transfer
    }

    return 0;
};

curl.SetOption(CurlOption.Url, args[0]);                                 // curl_easy_setopt(curl, CURLOPT_URL, argv[1])
curl.SetOption(CurlOption.Writefunction, write);                         // CURLOPT_WRITEFUNCTION, write_cb
curl.SetOption(CurlOption.Xferinfofunction, xferinfo);                   // CURLOPT_XFERINFOFUNCTION, xferinfo
if (!quiet)
{
    curl.SetOption(CurlOption.Noprogress, 0L);                           // CURLOPT_NOPROGRESS, 0L: call xferinfo
}

start.Start();                                                           // start = time(NULL)
CurlCode res = curl.Perform();                                           // res = curl_easy_perform(curl)
if (res != CurlCode.Ok)
{
    // fprintf(stderr, "curl_easy_perform() failed: %s\n", curl_easy_strerror(res))
    Console.Error.WriteLine($"curl_easy_perform() failed: {CurlErrorText.Easy(res)}");
}
else
{
    Console.WriteLine($"received {received} bytes");
}

Console.WriteLine(calls > 0 ? "progress callback called" : "progress callback never called");
return (int)res;                                                         // curl_easy_cleanup and curl_global_cleanup run as the usings end
