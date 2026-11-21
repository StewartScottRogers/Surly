<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-move-uploads-to-s3
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 12 of 12 cases pass on windows.
## Findings
1. One surl serves store over s3:// and https:// (two ports); HTTP PUT is 201 new / 204 replace, S3 put 200 either way. Cutover uses --auth aws-sigv4 (refuses the Basic PUT with 403).
2. Program finding, not claimed: the S3 put logs no 'stored' line (HTTP PUT logs 'PUT /x: 201, N bytes stored at ...'), so the log cannot list S3 uploads by name; the case counts S3 logins instead.
3. Program finding, not claimed: a SigV4-signed request over https with HTTP/2 (curl's default ALPN) is refused (401 or 403), while the same request with --http1.1 is accepted. Recorded in facts/s3.md.
