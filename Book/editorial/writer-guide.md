<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Writer guide

For an agent writing a batch of recipes. One page on purpose: the method is `../AI.md`, the
rubric `../REVIEW.md`, the decisions in `../../Documentation/Planning/Decisions/`. Read those
three when this page sends you there, not before.

## Your batch
The lead names 8 to 10 recipe keys from `plan.yml`. Each row gives the title, part, chapter,
surface (console, csharp, mcp), protocols and the reader's task. Write only those. Others write
other keys at the same time: touch only your recipes' folders and the fact sheets you add to.

## Read, in this order, and nothing else unless you need it
1. `facts/<protocol>.md` for each protocol in your batch: what surl and curl do, their options,
   refusals, exit codes and MCP tool arguments, already probed. If a sheet is missing or wrong,
   probe, then fix or add to it (format: `facts/README.md`) so the next writer does not probe again.
2. One finished recipe of your surface that the lead names, as the model of length and voice.
3. `../AI.md` "Rules that do not bend", and `../REVIEW.md`.
Do not read the Surl or Curl READMEs or `--aihelp` end to end; look up one thing, then record it.

## Tools (run from `Book/`, after the environment line the lead gives you)
- `python3 build/plan.py scaffold <key> ...` makes each folder from its template.
- `python3 build/probe.py "<surl args with {port}>" "<curl command with {port}>"` tries a claim.
- `python3 build/tools.py surl-mcp|curl-mcp [tool]` prints MCP tools and their argument names.
- `python3 build/verify.py -q --no-write <key> ...` runs your cases; prints only what broke.
- `python3 build/lint.py <key> ...` checks shape for your recipes only.
- Your recipe's book text: `cd build && python3 -c "import build,recipes; [build.write_recipe_files(r,s) for _,_,r,s in recipes.recipes({'<key>'})]"`, then read its `.generated/solution.adoc`.

## Rules that catch most mistakes
- Every claim in the Discussion is a case that passes; at least two cases; a claim no case can
  show is left out or cited. Probe before you claim.
- A loosening option (`--allow-anonymous`, `--self-signed`, `--allow-plaintext-auth`, `--auth`, ...)
  gets a case without it (the refusal) and with it, on two servers and ports if needed.
- Certificates and keys come from `setup:` (openssl, ssh-keygen), never fixtures. Seed stores
  with `seed:`; test a restart with `restart:`. Ports are `{{port}}`..`{{port9}}`; null is `{{null}}`.
- C# and MCP programs: one short file, `#:package` lines exactly as scaffolded; MCP programs start
  hosts by name with `StdioClientTransport` and roots in `SURL_MCP_ROOT` / `CURL_MCP_ROOT`.
- Prose: plain, direct, dry; spell out each acronym once per recipe; `` `name`{apos}s `` not
  `` `name`'s ``; wrap `=>` and `<=` in `` `+...+` ``; no "simply", "just" or exclamation marks.
- Behaviour that differs by platform (WinLDAP, NTLM, SSPI, the TLS library) gets `platforms:` on its
  case and is stated as such; read `facts/platforms.md` first. Only Windows runs locally; CI runs Linux and macOS.
- A console recipe may run a short `sh` script (a fixture shown with `include::`) when one command will not
  do; its Solution says it needs a POSIX shell (Git Bash on Windows). Prefer single commands.
- Timing: claim ranges or orderings that hold on a loaded machine and a slow CI runner. The first Curl.Api request in a
  process takes about 4 s (one-time set-up, Curl BL-5635): make one untimed warm-up request before any timed case.
- Never stop processes you did not start: no `taskkill /IM`, `pkill` or `killall` by name, since other writers' servers run beside yours; stop your own by its process ID; never `timeout N` a surl; never write `> NUL` in Git Bash.

## Done means
`verify.py -q --no-write` passes, `lint.py` is clean for your keys, each recipe has a short
`review.md` (verdict, the build checked against, findings in one line each), and the plan row's
status is `drafted`. Then report in under 150 words: per key, cases passing; program findings
(command, observed, expected); fact sheets you added or changed.
