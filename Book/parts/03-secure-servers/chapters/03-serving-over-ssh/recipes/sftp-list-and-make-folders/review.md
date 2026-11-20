<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-list-and-make-folders
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 11 of 11 cases pass on windows.
## Findings
1. Three servers (`main`, `closed`, `listing-only`); the two refusals each have the loosened counterpart in `main`. The cases run in order against `main`'s state: the folders `reports` and `archive` are made by earlier cases and listed by later ones.
2. Program finding, not a defect: `mkdir` needs `--allow-uploads`, not `--list-directories`; claimed and cased as it is.
3. The start-up warning of `--throwaway-hostkey` is cited to the README, not cased.
