<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smb-upload-to-a-share-in-csharp

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 3 of 3 cases pass on windows.

## Findings

1. Nearest honest recipe: Curl.Api 0.2.0 cannot upload over SMB (PUT to smb:// is sent as a download) and Libcurl 0.2.0 stops with code 55, SMB upload needs to know the size up front, so the upload is an HTTP PUT on a second listener and SMB is the read-back. Program finding, see the fact sheet smb.md.
