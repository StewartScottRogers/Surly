<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mcp-start-an-mcp-server-as-a-program
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surly.Surl.Api 1.0.0, Surly.Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 10 of 10 cases pass on windows.
## Findings
1. New recipe; overlaps mcp-call-a-tool's over-stdio case but is about the stdio transport itself: what curl starts, the one-message-per-line session fed to surl by hand, exit 0 at end of input, no login, the option gates, and the start-time refusals (another listen URL, --log-file -, --fault).
2. The raw session case uses a fixture session.jsonl and `<` redirection, which reads the same in cmd.exe and sh.
3. No ports: no servers: block; every case starts its own surl. Linux and macOS not run locally; the cases depend on no platform behaviour except the program-not-found text, matched only up to the program's name.
