<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-time-out-an-idle-client

Verdict: accept with fixes

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 6 of 6 cases pass on windows.

## Findings

1. Timing is deterministic by margin: `--idle-timeout 1` against a three-second gap (`curl --rate 1/3s`), and `--max-time 3` against fetches at 0, 2 and 4 seconds (one second either side). `%{num_connects}` shows the close as a new connection, so no claim reads a clock.
2. An HTTP exchange is a connection, so `--max-time` limits a kept-alive connection, not one request; the `max-time-busy` case shows it on a connection that is never idle.
3. `max-time-mid-request` uses a delay fault to keep a request unanswered past `--max-time 1`; curl exits 52 with no output.
4. Removed in drafting: the reason for the 120-second default (above libcurl's connection-cache age) is in Surl's ADR-0006, not its README; it is not a bibliography source, so the sentence went.
5. Not claimed: `--idle-timeout` does not cut a request that the server itself is holding with a delay fault (probed: `--idle-timeout 1` with `delay;stage=command;ms=3000` answers 200 after 3.7 s). Reasonable, since the client is waiting on the server, but ADR-0006 words idle as "no byte moves in either direction"; left out of the book and reported to the lead.
6. Updated 2026-11-19 for Surl 763754785 and Curl b9ca725077: Surl decided finding 5 and states it in `--aihelp limits` (the idle clock stops while a `delay` fault holds the server); the `defaults` pattern follows the new wording, and a fifth server, `held`, cases the rule (`idle-during-delay`, 200), stated in the Discussion as kept on purpose.
