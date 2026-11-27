# ADR-0010: The book follows the nuget.org packages, not the repositories

## Status

Accepted, 2026-11-27, by the author ("approved": "We want to align the book to the NuGet.org packages and not the repos. The book is about the NuGet packages that anyone can download."). Completes ADR-0008 item 6 (one pinned version per package); amends ADR-0005 item 4 (what verify runs against).

## Context

Until now the book was pinned to Surl and Curl commits (`Book/build/pins.yml`), built or downloaded those, and packed the libraries from source at `@*`. A reader has none of that: they have .NET and nuget.org. A recipe that passed against a local build proved nothing about the package the reader can install. From 2026-11-27 Curl publishes `Surly.Curl.Api`, `Surly.Libcurl` and the `Surly.Curl.Mcp` tool to nuget.org on every `v*` tag (first: 0.4.0-preview.5); Surl's `Surly.Surl.Api` and `Surly.Surl.Mcp` follow from 0.1.12. The command-line programs are not .NET tools yet: `surl` ships as native binaries on GitHub releases only, and `curl` becomes the tool `Surly.Curl` (Curl BL-6078).

## Decision

1. **The subject is the published package.** Every recipe runs against what a reader can download, at the version the book names.
2. **`Book/build/packages.yml` pins the versions**, exact, never `*`, once a package is on nuget.org; `build/pin.py` rewrites it and every `#:package` line together, and lint holds them equal. A C# or agent program names its package at that version (`#:package Surly.Curl.Api@0.4.0-preview.5`), so a reader can copy it and run it. The MCP SDK is pinned the same way.
3. **verify restores from nuget.org.** A package pinned in `packages.yml` is never packed from source or taken from a local feed; `build/fetch_pins.py` packs or downloads only what nuget.org does not carry yet, and says so.
4. **Agent recipes start the hosts as a reader does.** A host package pinned in `packages.yml` (`Surly.Curl.Mcp`, later `Surly.Surl.Mcp`) is installed from nuget.org for verify, and the generated `mcp.json` starts it with `dnx <package>@<version> --yes`. A host not published yet stays a command on `PATH`.
5. **Console recipes use the programs' release binaries** until the program is a .NET tool on nuget.org; then they use the tool at its pinned version, and this item is amended.
6. **A re-pin follows nuget.org.** A new version of a pinned package on nuget.org is the trigger; `pins.yml` keeps each program's commit only as the record of where the binaries came from.

## Consequences

- Curl's side is done in one change: 323 package lines pinned to 0.4.0-preview.5, `curl-mcp` from nuget.org, the MCP SDK at 2.4.0. Surl's side waits for 0.1.12 on nuget.org; until then `Surly.Surl.Api` comes from Surl's GitHub release.
- The book points readers at pre-release packages while Curl is in preview. Pinned exact versions make that explicit rather than silent.
- A package that nuget.org refuses or delists breaks the recipes that use it; that is a finding for the program, as a regression is.
