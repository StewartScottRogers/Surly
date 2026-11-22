<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-run-beside-a-web-application-factory

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 4 of 4 cases pass on windows (C#).

## Findings

1. Fitness (3): NEAREST HONEST RECIPE, not WebApplicationFactory itself. Microsoft.AspNetCore.Mvc.Testing is not a pinned package, and in a file-based program it works only with three workarounds (#:sdk Web, a static flag to stop the factory re-running the test program, and an MvcTestingAppManifest.json for the content root; facts/config.md). The program starts a plain ASP.NET Core app in process beside SurlServer and injects the bound URLs through configuration; the Solution paragraph says so.
2. Truth (8): app and server on different ports, rates over HTTP, a mail delivered (waited for at most 10 s), two apps, no injection (502) and a refused password (wait bound 2 s) are cases.
3. Not run: the real factory's WithWebHostBuilder; the Discussion names it in one sentence and claims nothing about it.
