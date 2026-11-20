<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-limit-download-speed
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 4 of 4 cases pass on windows.
## Findings
1. Timings are claimed as ranges (limited >= 2 s and < 50000 B/s; unlimited < 2 s), safe on a slow runner; a slow runner only moves the limited case further into its range.
2. Program finding: `--limit-rate 25k` averages ~33500 B/s on 100000 bytes (about one second's worth passes before pacing starts); not claimed, the prose calls speed_download an average.
3. Program finding: after `--limit-rate 25k --max-time 1` (exit 28) `-w %{size_download}` and `%{speed_download}` print 0 though 51200 bytes were written to the file; upstream reports the bytes received. Not claimed.
4. Seen once, not reproduced: an unlimited curl right after a limited one exited 4294967295 with no output.
5. big.bin is made in setup with dd (100000 zero bytes), so schema 2.
