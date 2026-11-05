# Write the program-task writer
key: program-task-writer
kind: factory
depends: []
status: ready
## Why
ADR-0006 item 3: a failing recipe judged the program's fault becomes a task on the Curl or Surl board in that board's format, with recipe.yml and the failing output attached.
## Acceptance
- [ ] Book/build/verify.py gains --program-tasks <path-to-Curl> <path-to-Surl>, which writes one task per broken recipe judged the program's fault, in that board's Tasks/README.md format
- [ ] verified.yml records the pointer
- [ ] A decision record if the verified.yml schema changes
## Log
