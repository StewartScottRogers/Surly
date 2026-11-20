<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-keep-the-server-log-for-a-failed-run
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 6 of 6 cases pass on windows.
## Findings
1. Nearest honest recipe: surl redacts nothing. The trace (and the -v log) holds FTP PASS in clear and HTTP/1.1 Basic/Bearer headers verbatim, so the recipe keeps a trace filtered with awk (client bytes dropped) and proves no secret is left.
2. Program finding: with --trace-ascii given, the per-exchange log lines go to the trace file and --log-file gets only startup lines; so --log-file is not used here and the trace is presented as log and trace in one.
3. Uses POSIX sh scripts (tests.sh, ci-step.sh) shown with include::; the Solution says so.
4. Protocols are https and ftps (the plan row says http, ftp): passwords over plain http/ftp are refused unchecked by default, and TLS keeps the recipe free of loosening options.
