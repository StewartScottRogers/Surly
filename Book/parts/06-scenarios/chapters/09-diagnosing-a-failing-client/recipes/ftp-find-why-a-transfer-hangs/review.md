<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-find-why-a-transfer-hangs
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, commit d64faa4bb), Curl port 8.22.0 (Curl commit 2145a49eae), surl-mcp 1.0.0, curl-mcp, dotnet 11.0.100, ModelContextProtocol.Core: 9 of 9 cases pass on windows.
## Findings
1. Nearest honest recipe: surl cannot block a data port as a firewall would, so the hang is a `delay;stage=command;command=EPSV` rule that holds the 229; the Discussion says the rule plays the broken network.
2. A held reply is released when the client leaves (Surl ADR-0141 amendment 2), so the record shows the 229 (or the 150) sent as the exchange ended; the program times each reply with the chunks' `at`, new in this build.
3. Timing claims are bounds ("[2-9] s later", "within 1 s"), not exact values; Linux and macOS not yet run in CI.
4. `--allow-anonymous` has its refusal case (exit 67).
