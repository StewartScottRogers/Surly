# Plan proposal A: Parts 1 and 2

300 rows: 80 console, 110 C#, 110 agent. 75 triplets and 75 singles (5 console, 35 C#, 35 agent).

**For the lead:** each triplet stays together in its task chapter. ADR-0008 item 2 puts C# and MCP recipes in chapters of their own. Singles use the existing from-C# and from-an-agent chapters.

## New chapters

Part 1:
- 06 Fetching Part of a File: ranges, resuming, If-Range.
- 07 Downloading Many Files: output paths, globbing, reuse, parallel.
- 08 Finding Out Why a Fetch Failed: --fail, exit codes, retries, taken ports.
- 09 Choosing Where surl Listens: port 0, IPv6, Unix sockets.

Part 2:
- 07 Managing Files over FTP: folders, rename, delete, append, resume upload.
- 08 Routing Mail for a Domain: aliases, catch-all, CSV, quotas, shared folders.
- 09 Keeping Mailboxes in Order: TOP/UIDL, draining a maildrop, folders, STATUS, UID sync.
- 10 Retained Messages and Queues: retained topics, wildcards, persistence.
- 11 Testing an App Against a Stand-in: what an app mailed, uploaded, left behind or published.
- 12 Diagnosing from What Was Recorded: who uploaded, which client misbehaved, SMTP dialogues.

## Candidate asks

1. `--control` is parsed but not served, so no control-endpoint recipe is planned.
2. HTTP answers only `If-Range`. Without `If-Modified-Since` and ETags, `curl -z` recipes are blocked.
3. HTTP serves no index page, because a directory is always 404.
4. Check before writing: can Curl.Api use Unix sockets and set TFTP block sizes? Two twins depend on it.
5. Surl.Api has no `-K` property, so config files go through `AdditionalArguments`; this needs confirming.
6. Agents cannot make symbolic links or hold concurrent POP3 sessions, so those two tasks are console singles.
7. curl cannot set MQTT RETAIN, so retained-message recipes rely on `--topic ;retain`.
