<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-run-a-fixture-in-github-actions
Verdict: accept with fixes
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Surl.Api, Curl.Api, surl-mcp and curl-mcp from the same commits, Windows.
## Findings
6 of 6 cases pass on Windows.
1. verify cannot run a workflow: tests.yml is shown as the reader's artefact, a case checks its steps (order, `if: always()` stop, `if: failure()` upload, three runner OSes), and steps.sh runs the scripts the steps run, each in its own shell, with GITHUB_ENV applied between them.
2. Not proved by any case: that the runner keeps a step's background process alive into later steps, and that the build-from-source steps work. The book's own verify.yml builds surl and curl the same way; no bibliography key exists for GitHub Actions to cite.
3. Neither program has a release yet, so the workflow builds them from source; replace with a download once released.
