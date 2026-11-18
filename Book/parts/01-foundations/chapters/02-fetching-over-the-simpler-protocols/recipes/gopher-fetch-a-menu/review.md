<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: gopher-fetch-a-menu
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit 3c5b39474), Curl port 8.22.0 (Curl commit b140f6d88c): 6 of 6 cases pass on windows.
## Findings
1. Rubric 3/tier: the menu needs `--list-directories`, which the chapter list places in Part 2 (listings). Kept, because a Gopher menu is a listing and the key is fixed; the author may prefer this recipe in working-servers.
2. Rubric 4/7: "without `--list-directories` a folder gets the error menu" is not cased (one server configuration per recipe); the Discussion says only that the option is off by default, cited to <<surl-readme>>. Probed by hand: without it, `curl gopher://.../` prints the type 3 error menu, exit 0.
3. Menu lines carry the run's port, so the Solution and `fetch-a-submenu` use `stdout-matches`; the Discussion shows the menu as a plain listing.
4. Sources wanted for the bibliography: RFC 1436 (Gopher).
5. The generated Solution block prints `Listening on http://127.0.0.1:8080/` for this non-HTTP listener: build.py hard-codes the scheme. Needs a build fix (lead); not editable from the chapter.
6. Verified after the byte-exact fixture fix; unaffected.
