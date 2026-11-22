<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-watch-a-topic-during-a-test
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. The watcher must be MQTT 5: surl ends an MQTT 3.1.1 subscription after the retained messages (cased). curl -o writes each payload as it arrives.
2. Start and end markers on the same topic make the waits bounded and certain; the claim allows one or more start markers.
3. kill stops the background curl; exit code of the killed watcher is not claimed.
