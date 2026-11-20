<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ldap-search-with-a-filter-and-scope
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 11 of 11 cases pass on windows.
## Findings
1. Searches are anonymous (`-u :` against `--allow-anonymous`), so the recipe does not need `--allow-plaintext-auth`; the existing recipe covers the password bind. `-u :` works on Windows; the `strict` server shows the refusal without the option.
2. Patterns allow the OpenLDAP extra blank line (`\n?` after each entry); only Windows was run here. Linux and macOS not checked: if OpenLDAP words the anonymous-bind refusal differently from "Inappropriate Authentication", case `anonymous-refused` needs `platforms:`, as the existing recipe's identical case did not.
3. The start-up warning of `--allow-anonymous` is cited to the README.
