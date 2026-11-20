<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Fact sheets

One file per protocol (`http.md`, `smtp.md`, `kafka.md`, ...): what a recipe writer needs to know
about surl's server and curl's client for that protocol, probed once and kept here, so the next
writer reads half a page instead of the READMEs and `--aihelp` again. They are notes for writers,
not book text, and no recipe cites them.

A sheet is true for the builds named at its top. When verify breaks a recipe after the programs
change, check its sheet first; whoever fixes the recipe fixes the sheet.

## Format

```
# <protocol> facts
Checked against: surl <version> (Surl <commit>), curl (Curl <commit>), <date>, <platform>.

## Listen URLs and readiness
Schemes, default ports, TCP or UDP (ready: port or none), what a listen URL may not contain.

## Server options that matter
Option: what it does here; default; whether it loosens security (and the start-up warning).

## Secure by default
What surl refuses without a loosening option: the reply (status, code, error) and curl's exit code.

## Client: curl
Options that matter, the URL shape, what curl prints, exit codes that recipes check.

## MCP
surl-mcp tools for this protocol with argument names; curl-mcp tool use; what the answers hold.

## Gotchas
Things that cost a writer a probe: races, platform differences, CRLF, ordering, quirks kept on purpose.

## Recipes
Keys of the recipes that rely on this sheet.
```
