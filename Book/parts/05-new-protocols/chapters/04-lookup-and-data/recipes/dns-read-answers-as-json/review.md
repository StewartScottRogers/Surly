<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: dns-read-answers-as-json

Verdict: accept with fixes

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 8 of 8 cases pass on windows.

## Findings
1. Truth: the 512-byte UDP truncation, the absence of recursion and the dig-compatible exit 0 are the Surl README's and ADR claims, cited, not cases.
2. Platforms: not marked; a query to a live server was only run on windows locally. UDP to a dead port would differ on Linux and macOS (platforms.md) but no case uses it. CI should confirm the eight cases there.
3. `;TYPE=MX` in the URL path does not work (the whole path becomes the name, REFUSED); `?type=MX` does. Only the latter is shown.
