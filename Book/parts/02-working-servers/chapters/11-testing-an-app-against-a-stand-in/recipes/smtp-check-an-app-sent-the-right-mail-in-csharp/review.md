<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-check-an-app-sent-the-right-mail-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 7 of 7, with dotnet 11, Surl.Api 1.0.0, Curl.Api 0.2.0 cases pass on windows.
## Findings
1. Waits with WaitForEventAsync (StoreChanged, Name = owner); the never-delivered case is a 2 s bound that throws TimeoutException. ReadMessageAsync sets no flag.
2. MailDeliveredTo = true is --mail-delivered-to; the copy starts with Delivered-To before Return-Path.
