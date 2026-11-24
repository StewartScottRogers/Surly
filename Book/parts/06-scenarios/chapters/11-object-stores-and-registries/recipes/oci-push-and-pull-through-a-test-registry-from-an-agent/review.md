<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: oci-push-and-pull-through-a-test-registry-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 5 of 5 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Three digests compared: manifest.json, index.json's tag (read_file), and the pulled standardOutput. After a re-tag index.json keeps the old manifest with no annotations.
3. put_file over a blob makes the registry answer 404 BLOB_UNKNOWN.
