<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-list-a-directory
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 10 of 10 cases pass on windows.
## Findings
1. Needs the author: the task "`--list-directories`" over HTTP cannot be done as written. surl's HTTP server answers a folder 404 with listings on or off (`surl --aihelp content`, `--aihelp http`); the listing is served by the WebSocket server. The recipe keeps the key and says so, solving the reader's problem with a `ws://` listen URL on the same surl. Title changed to "Listing a Directory Served over the Web" so it does not promise an HTTP listing. If the author wants an HTTP listing, it is a Surl feature request; the key could then move or be re-pointed.
2. `surl --aihelp content`'s example "Serve in memory, with listings" shows `surl --list-directories http://...` reached with `curl http://<port>/`, which suggests an HTTP listing the build does not give (program finding in the chapter report).
3. The `dot-names-left-out` case repeats the solution's command; kept so the claim has an id of its own (rubric 12).
4. Generated Solution block also starts the `no-listings` server (build finding, see http-accept-uploads).
