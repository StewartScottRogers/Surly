<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-reset-keys-directory-and-zones

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 4 of 4 cases pass on windows (C#).

## Findings

1. Fitness (4): SnapshotAsync/RestoreAsync is the one call that puts keys, directory and zones back to a baseline; ResetAsync empties them (their contents come from calls, not options), and the recipe says both.
2. Truth (8): each store is read through its own client (Curl.Api over redis, ldap, dns), not through Surl.Api; the reset-empties, snapshot-reused and no-restore cases are in recipe.yml.
3. LDAP: the empty-tree search is claimed as curl error 39 only (the wording is the platform's); Linux and macOS not run.
