<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: oci-push-and-pull-through-a-test-registry
Verdict: drafted, ready for review
Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 7 of 7 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. deploy.sh compares the pulled manifest's SHA-256 (openssl dgst -r) with the build's digest and reads Docker-Content-Digest with -D, since -w %header{docker-content-digest} prints nothing on oci:// (reported to Curl).
3. Each oci:// curl is 1.5-4 s on a loaded machine; the heavy cases carry timeout: 120. A blob file changed on disk is answered 404 BLOB_UNKNOWN.
