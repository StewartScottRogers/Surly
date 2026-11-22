<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-repeat-a-failure-with-a-fixed-seed
Verdict: accept with fixes
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Surl.Api, Curl.Api, surl-mcp and curl-mcp from the same commits, Windows.
## Findings
9 of 9 cases pass on Windows; none is platform-specific (the seeded draws are SHA-256 based and the same everywhere).
1. Nearest honest recipe: the plan row names HTTP and FTP, but neither draws any identifier from `--random-seed` in this build (Surl source: only S3, Azure, OCI, SignalR and other extended servers do). The recipe uses S3 over `--s3-plaintext` (HTTP), whose multipart `UploadId` is a generated name; protocols set to [s3, http]. FTP is not mentioned. Plan row protocols may want [s3, http].
2. The failure is a stand-in bug (an ID beginning with a digit), labelled as such in upload.sh.
3. surl prints no seed and has none unless given: the script draws and prints one. Seeds depend on connection order, which the recipe shows (readiness probe flips seed 3 from failing to passing).
