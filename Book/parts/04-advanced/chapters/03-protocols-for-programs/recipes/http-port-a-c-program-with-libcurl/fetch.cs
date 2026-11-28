// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// fetch.c, the classic libcurl easy-handle program, ported call for call to C# with Libcurl,
// the libcurl API for C#. Each line names the C call it stands for.
// Run: dotnet run fetch.cs -- <url>
#:package Surly.Libcurl@0.4.0-preview.7

using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: dotnet run fetch.cs -- <url>");
    return 2;
}

Stream stdout = Console.OpenStandardOutput();

// static size_t write_cb(char *data, size_t size, size_t nmemb, void *userdata)
WriteCallback writeCallback = data =>
{
    stdout.Write(data);                                                  // fwrite(data, size, nmemb, stdout)
    return data.Length;                                                  // return size * nmemb
};

await using CurlLibrary library = CurlGlobal.Init(CurlGlobalFlags.Default); // curl_global_init(CURL_GLOBAL_DEFAULT)
using CurlEasy curl = library.EasyInit();                               // curl = curl_easy_init()
curl.SetOption(CurlOption.Url, args[0]);                                 // curl_easy_setopt(curl, CURLOPT_URL, argv[1])
curl.SetOption(CurlOption.Writefunction, writeCallback);                 // curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, write_cb)
CurlCode res = curl.Perform();                                           // res = curl_easy_perform(curl)
if (res != CurlCode.Ok)
{
    Console.Error.WriteLine($"curl_easy_perform() failed: {CurlErrorText.Easy(res)}"); // curl_easy_strerror(res)
}
else
{
    curl.GetInfo(CurlInfo.ResponseCode, out object? responseCode);      // curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &response_code)
    Console.Error.WriteLine($"response code {(long)responseCode!}");
}

return (int)res;                                                         // curl_easy_cleanup and curl_global_cleanup run here, as the usings end
