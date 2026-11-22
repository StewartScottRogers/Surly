<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-sync-a-folder-to-a-bucket-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797: 12 of 12 cases pass on windows.
## Findings
1. The sync itself is in the program: list ETags over https with `AwsSigV4` and `HttpVersion.Http11`, hash each file, put the ones that differ. The puts are counted with `RecordExchanges` and `FindEvents` (store events with an `ExchangeId`; the seeded object has none).
2. Console's missing-source case has no library counterpart (the failure is the program's own file read); a case that a sync with nothing changed puts nothing stands in for it.
