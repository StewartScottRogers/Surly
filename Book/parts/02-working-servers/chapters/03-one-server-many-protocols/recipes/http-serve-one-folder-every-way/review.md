<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-serve-one-folder-every-way
Verdict: accept with findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 5 of 5 cases pass on windows.
## Findings
1. Solution prints DICT's whole reply; its greeting ID (<10@surl>) is 10 under verify because verify's ready probe opens one exchange per TCP listener; a reader sees another number.
2. --allow-anonymous is needed only for FTP (no account: curl's anonymous login is 530, exit 67); the refusal case runs on a second server with its own folder.
3. TFTP shares the HTTP port number (UDP vs TCP).
