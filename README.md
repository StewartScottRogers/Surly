# Surly

<a href="https://stewartscottrogers.github.io/Surly/"><img src="https://stewartscottrogers.github.io/Surly/cover.png" alt="The cover of Surly: The Surl and Curl Cookbook" width="240"></a>

**Read the book online: [stewartscottrogers.github.io/Surly](https://stewartscottrogers.github.io/Surly/)**, every recipe with its checked claims, searchable, free.

Surly is the monorepo for two matched programs and the book that describes them:

- **Curl** - a drop-in replacement for the curl command-line tool, in C# on .NET 10. Today at [StewartScottRogers/Curl](https://github.com/StewartScottRogers/Curl).
- **Surl** ("Server URL") - the server-side mate of curl: for every request curl can make, Surl is the server that answers it. Today at [StewartScottRogers/Surl](https://github.com/StewartScottRogers/Surl).
- **The packages** - everything the book's recipes install, on nuget.org: [Surly packages](https://www.nuget.org/packages?q=Surly.&includeComputedFrameworks=true&prerel=true) (curl as the .NET tool `Surly.Curl`, the C# libraries, the two MCP hosts; `surl` itself is a download from its GitHub release).
- **Surly** - a cookbook, in the Problem / Solution / Discussion form, in which every recipe pairs a `surl` command that listens with a `curl` command that talks to it.

## Layout

| Folder | Holds |
|---|---|
| `Curl/` | The Curl solution, once migrated here with its history |
| `Curl.ThirdPartyReview/` | Outside fitness reviews of the Curl port against C curl, once migrated here with their history |
| `Surl/` | The Surl solution, once migrated here with its history |
| `Book/` | The Surly cookbook |
| `Factory/` | The unattended production line that writes and maintains the book |
| `Documentation/Planning/Decisions/` | Architecture decision records for the monorepo itself |

`Surly.slnx` at the root will reference both solutions' projects once they are migrated.

## Licensing

Curl and Surl are open source under their own licences. The book is copyright © 2026 Stewart Rogers: its prose under Creative Commons Attribution-NoDerivatives 4.0, its example code under Apache 2.0. See `LICENSE.md`.

## Status

Skeleton only. The two solutions have not been migrated and the book has not been started. Decisions still open are listed in `Documentation/Planning/Decisions/README.md`.
