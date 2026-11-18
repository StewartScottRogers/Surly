<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-serve-and-fetch-in-process
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit 3c5b39474), Curl port 8.22.0 (Curl commit b140f6d88c), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 cases pass on windows.
## Findings
1. Fixed: removed "nothing touches the disk" from the Discussion; no case proves it.
2. Fixed: the 22 exit code is stated as the program's choice (mirroring `curl -f`), not the library's, since `SendAsync` throws nothing for a 404 (case missing-path).
3. For the author: the local Surl.Api and Curl.Api packages in the feed list their bundled UnitLibrary projects as NuGet dependencies, so a program cannot restore them; verified against a copy of the feed with those dependency lines removed (the assemblies are inside the packages). Re-verify against the released packages.
4. Not affected by the CRLF fixture fix: this recipe has no fixtures, and it was first verified after the fix landed.
