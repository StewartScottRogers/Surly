// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// post-form.c, which posts a multipart form (a text field and a file) with libcurl's curl_mime API,
// ported call for call to C# with Libcurl. Each line names the C call it stands for.
// Run: dotnet run post-form.cs -- <url> <file>
#:package Surly.Libcurl@0.4.0-preview.6

using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Mime;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: dotnet run post-form.cs -- <url> <file>");
    return 2;
}

WriteCallback discard = data => data.Length;                             // the response body is not wanted

await using CurlLibrary library = CurlGlobal.Init(CurlGlobalFlags.Default); // curl_global_init(CURL_GLOBAL_DEFAULT)
using CurlEasy curl = library.EasyInit();                               // curl = curl_easy_init()

using CurlMime mime = library.MimeInit();                                // mime = curl_mime_init(curl)
CurlMimePart part = mime.AddPart();                                      // part = curl_mime_addpart(mime)
part.SetName("title");                                                   // curl_mime_name(part, "title")
part.SetData("November");                                                // curl_mime_data(part, "November", CURL_ZERO_TERMINATED)
part = mime.AddPart();                                                   // part = curl_mime_addpart(mime)
part.SetName("file");                                                    // curl_mime_name(part, "file")
CurlCode fileCode = part.SetFileData(args[1]);                           // curl_mime_filedata(part, argv[2])
if (fileCode != CurlCode.Ok)
{
    Console.Error.WriteLine($"curl_mime_filedata() failed: {CurlErrorText.Easy(fileCode)}");
    return (int)fileCode;
}

part.SetType("text/csv");                                                // curl_mime_type(part, "text/csv")

curl.SetOption(CurlOption.Url, args[0]);                                 // curl_easy_setopt(curl, CURLOPT_URL, argv[1])
curl.SetOption(CurlOption.Mimepost, mime);                               // curl_easy_setopt(curl, CURLOPT_MIMEPOST, mime)
curl.SetOption(CurlOption.Writefunction, discard);                       // curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, discard)
CurlCode res = curl.Perform();                                           // res = curl_easy_perform(curl)
if (res != CurlCode.Ok)
{
    Console.Error.WriteLine($"curl_easy_perform() failed: {CurlErrorText.Easy(res)}"); // curl_easy_strerror(res)
    return (int)res;
}

curl.GetInfo(CurlInfo.ResponseCode, out long code);                      // curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &code)
curl.GetInfo(CurlInfo.SizeUploadT, out long sent);                       // curl_easy_getinfo(curl, CURLINFO_SIZE_UPLOAD_T, &sent)
Console.WriteLine($"response code {code}, {sent} bytes sent");
return 0;                                                                // curl_mime_free, curl_easy_cleanup and curl_global_cleanup run as the usings end
