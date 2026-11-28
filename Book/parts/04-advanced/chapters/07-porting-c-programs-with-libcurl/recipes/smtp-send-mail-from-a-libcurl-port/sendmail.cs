// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// sendmail.c, which sends one mail message over SMTP with CURLOPT_MAIL_FROM, CURLOPT_MAIL_RCPT and a
// read callback that hands libcurl the message a line at a time, ported call for call to C# with Libcurl.
// Each line names the C call it stands for.
// Run: dotnet run sendmail.cs -- <smtp url> <user:password> <from> <to>
#:package Surly.Libcurl@0.4.0-preview.7

using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

if (args.Length < 4)
{
    Console.Error.WriteLine("usage: dotnet run sendmail.cs -- <smtp url> <user:password> <from> <to>");
    return 2;
}

// static const char *payload_text[]: the message, header and body, every line ending in CR LF
string[] payload =
[
    $"To: <{args[3]}>\r\n",
    $"From: <{args[2]}>\r\n",
    "Subject: Nightly report\r\n",
    "\r\n",
    "The nightly run finished: 42 files, no errors.\r\n",
];
int line = 0;
long reads = 0;

// static size_t payload_source(char *ptr, size_t size, size_t nmemb, void *userp): one line per call
ReadCallback payloadSource = buffer =>
{
    if (line == payload.Length)
    {
        return 0;                                                        // no more lines: the end of the message
    }

    byte[] bytes = Encoding.ASCII.GetBytes(payload[line]);
    if (bytes.Length > buffer.Length)
    {
        return 0;
    }

    bytes.CopyTo(buffer);                                                // memcpy(ptr, data, len)
    line++;
    reads++;
    return bytes.Length;
};

await using CurlLibrary library = CurlGlobal.Init(CurlGlobalFlags.Default); // curl_global_init(CURL_GLOBAL_DEFAULT)
using CurlEasy curl = library.EasyInit();                               // curl = curl_easy_init()
CurlSlist? recipients = CurlSlist.Append(null, args[3]);                 // recipients = curl_slist_append(NULL, argv[4])

curl.SetOption(CurlOption.Url, args[0]);                                 // curl_easy_setopt(curl, CURLOPT_URL, argv[1])
curl.SetOption(CurlOption.Userpwd, args[1]);                             // CURLOPT_USERPWD, argv[2]
curl.SetOption(CurlOption.MailFrom, $"<{args[2]}>");                    // CURLOPT_MAIL_FROM, "<from>"
curl.SetOption(CurlOption.MailRcpt, recipients);                         // CURLOPT_MAIL_RCPT, recipients
curl.SetOption(CurlOption.Readfunction, payloadSource);                  // CURLOPT_READFUNCTION, payload_source
curl.SetOption(CurlOption.Upload, 1L);                                   // CURLOPT_UPLOAD, 1L

CurlCode res = curl.Perform();                                           // res = curl_easy_perform(curl)
CurlSlist.FreeAll(recipients);                                           // curl_slist_free_all(recipients)
if (res != CurlCode.Ok)
{
    // fprintf(stderr, "curl_easy_perform() failed: %s\n", curl_easy_strerror(res))
    Console.Error.WriteLine($"curl_easy_perform() failed: {CurlErrorText.Easy(res)}");
    return (int)res;
}

curl.GetInfo(CurlInfo.ResponseCode, out long code);                      // curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &code)
curl.GetInfo(CurlInfo.SizeUploadT, out long sent);                       // CURLINFO_SIZE_UPLOAD_T
Console.WriteLine($"sent {sent} bytes in {reads} reads, last reply {code}");
return 0;                                                                // curl_easy_cleanup and curl_global_cleanup run as the usings end
