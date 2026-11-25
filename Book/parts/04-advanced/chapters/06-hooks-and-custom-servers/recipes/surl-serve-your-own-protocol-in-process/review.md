<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-serve-your-own-protocol-in-process
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 6 of 6 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Protocol 'echo' is the recipe's own scheme (SurlProtocolServer). Recording, events, accept-stage faults and the idle timeout come from Surl with no server code; --max-line reaches the server as ExchangeContext.Limits and the server enforces it.
3. Command-stage faults (context.Faults.FindAsync) were probed (the rule is returned; the server must act on it) but not claimed.
