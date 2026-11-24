<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: oci-list-tags-with-a-registry-token
Verdict: drafted, ready for review
Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 8 of 8 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. The tag list needs a token scoped to the repository: no scope or another repository's scope is 403 DENIED; the challenge carries scope="repository:demo/app:pull".
3. tags.sh takes QUERY then SCOPE; the scope case passes n=100 as a harmless query so the scope can follow.
