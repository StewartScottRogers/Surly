<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ldap-provision-a-user-and-bind-as-it-in-csharp

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Surl.Api, Curl.Api, surl-mcp and curl-mcp of the same commits: 8 of 8 windows cases pass; 2 cases are linux and macos only, left to CI.

## Findings

1. Same stand-in as the console recipe: Directory.AddEntriesAsync and Accounts.Add play the job.
2. Records read with WaitForEventAsync (10 s bound); the directory seed is found by Store = Directory and Name = bob's DN.
