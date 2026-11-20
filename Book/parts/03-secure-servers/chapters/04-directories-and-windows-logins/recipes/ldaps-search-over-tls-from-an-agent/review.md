<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ldaps-search-over-tls-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.
## Findings
1. One program, one server with an ldaps and an ldap listener; the cases match lines of its output. The certificate comes from `setup:`; `--cert` and `--key` take names relative to `SURL_MCP_ROOT`.
2. The program prints curl's exit code and one of two phrases from the error, not the library's wording; the `untrusted` case (exit 60 without `-k`) is `platforms: [windows]`, since only Windows was run (see the console twin's review on `--cacert`).
3. curl-mcp's `transfer` tool cannot bind to LDAP, so the `curl` tool is used.
- Platform (2026-11-19, CI run 44662810792): wrong-password and plain-refuses are [windows]; [linux, macos] twins check exits 67 and 38. CI shows the no -k bind exits 60 on Linux and macOS too; untrusted could drop its platforms.
