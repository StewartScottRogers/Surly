<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: curl-check-what-a-build-supports
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.
## Findings
1. Feature lists differ by platform, so the only feature gate that runs is IPv6 ([::1] loopback), assumed present on Linux and macOS CI; the missing-feature skip uses a made-up name (NoSuchFeature) rather than a platform-dependent one. Check the IPv6 case on CI.
2. Counts of protocols and features are matched as numbers only.
3. Overlaps surl-check-what-curl-supports-from-an-agent (fields of version); this one is the skip-or-run scenario.
