<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-create-and-replace-a-file
Verdict: accept with findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 7 of 7 cases pass on windows.
## Findings
1. Solution makes all three uploads on one command line (three -T/URL pairs), so the 201, 204 and 409 sit together in one listing.
2. Uses an account with Digest rather than --allow-anonymous, so the only loosening is --allow-uploads; its refusal (405) is the uploads-off case on a second server.
3. Overlaps http-accept-uploads (201/204/409 cases there too); this recipe adds the replaced content, the folder path 409, the 404 after a 409 and -f 22.
