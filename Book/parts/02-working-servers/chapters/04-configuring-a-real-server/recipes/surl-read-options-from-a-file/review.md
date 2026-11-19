<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-read-options-from-a-file

Verdict: accept with fixes

Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 8 of 8 cases pass on windows.

## Findings

1. Needs the author: the listen URLs are on the command line, not in `surl.cfg`, because verify.py substitutes port placeholders in commands but writes fixtures verbatim. The Discussion says so, and proves `url =` lines indirectly (no url: exit 2 `no URL specified`; same file plus `url = http://127.0.0.1:0/`: exit 37 at the missing directory). A schema feature for placeholders in fixtures would let the file hold its own `url =` lines.
2. Fixed: dropped claims with no case: `:` as a name/value separator, an option after `-K` overriding the file (observed while probing, `surl -K a.cfg --directory site` served, but a serving surl cannot be a passing case), and "surl reads no config file it is not given".
3. Fixed: the unknown-option case matches the first line only; the refusal has no `(2)` in it (`surl: typo.cfg:1: option --directroy: is unknown`), unlike the start-time refusals (`surl: (2) ...`). Same shape as curl's own option errors; noted, not a defect.
4. `URL` is not spelled out; Part 1 does not spell it out either.
5. <<http-log-in-with-digest>> and <<http-listen-on-several-urls>> point at recipes not yet written.
