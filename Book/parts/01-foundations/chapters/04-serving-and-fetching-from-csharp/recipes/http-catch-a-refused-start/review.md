<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-catch-a-refused-start
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit 3c5b39474), Curl port 8.22.0 (Curl commit b140f6d88c), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 6 of 6 cases pass on windows.
## Findings
1. Fixed: removed "the codes follow upstream curl's numbers"; the recipe has no case comparing with curl.
2. The case same-as-surl is a `run:` case in a `csharp` recipe, so the parity claim with the console is checked rather than asserted. Lint and verify accept it; the author may prefer csharp recipes to be programs only.
3. For the author: `SurlExitCode` is in namespace `Surl.Protocol.Abstractions`, not `Surl.Api`, so the program needs a second `using`. The Surl.Api README does not mention it.
4. `Lines` items begin with the code in brackets ("(58) https://..."), as `surl`'s stderr does; the README's "the lines surl writes after surl: " is accurate but does not show it.
5. The nothing-left-bound case proves the port is free after the refusal; it cannot tell whether the http listener was ever bound before the certificate check refused the start.
6. Exit 100 for ArgumentException is the program's own number; the Discussion says so.
7. Cross-reference <<https-serve-self-signed-in-process>> points at a recipe not written yet (part 3).
8. Same package-feed note as http-serve-and-fetch-in-process. No fixtures.
