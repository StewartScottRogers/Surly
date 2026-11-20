<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-recover-from-a-dropped-connection
Verdict: drafted, ready for review
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 5 of 5 cases pass on windows.
## Findings
1. Drop rules are at stage=command so verify's readiness connection does not spend times=1 (accept-stage behaviour claimed from surl --aihelp testing).
2. Exit 56 (reset) is Windows-observed; http-inject-a-fault makes the same assumption for Linux and macOS. If CI sees 52 (empty reply), the retry-alone and no-retry cases need it.
3. Program finding: with -sS and --retry-all-errors the Curl port prints the first attempt's 'curl: (56) Recv failure' to stderr even though the command exits 0. Not claimed; check against upstream curl.
4. The 'retry counted' half of the task is curl's %{num_retries}; the server-side count is in the C# and agent twins (verify cannot read a server's log).
