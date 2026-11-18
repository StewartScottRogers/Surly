<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-download-anonymously
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit 3c5b39474), Curl port 8.22.0 (Curl commit b140f6d88c): 5 of 5 cases pass on windows.
## Findings
1. Rubric 4/7: the default refusal (without `--allow-anonymous`, 530 and curl exit 67) and the start-up warning are cited to <<surl-readme>>, not cased. The schema gives one `{{port}}` and one server configuration per recipe, so a second server without the option cannot be started. Probed by hand: `surl --directory site ftp://...` then `curl -v ftp://.../hello.txt` gives `530 Login needs TLS first: send AUTH TLS`, exit 67. Needs the author: accept the citation, or a schema change (a second port placeholder) so it can be a case.
2. Fixed: added the `logs-in-as-anonymous` case so "curl logs in as anonymous" is checked, not asserted; `-v` and `-o` are now explained.
3. Sources wanted for the bibliography: RFC 959 (FTP). Not added; the brief forbids editing it.
4. The generated Solution block prints `Listening on http://127.0.0.1:8080/` for this non-HTTP listener: build.py hard-codes the scheme. Needs a build fix (lead); not editable from the chapter.
5. Fixtures were verified after the byte-exact fixture fix in verify.py; no claim depends on line-ending bytes.
