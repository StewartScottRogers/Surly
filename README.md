# Surly

Surly is the monorepo for two matched programs and the book that describes them:

- **Curl** - a drop-in replacement for the curl command-line tool, in C# on .NET 10. Today at [StewartScottRogers/Curl](https://github.com/StewartScottRogers/Curl).
- **Surl** ("Server URL") - the server-side mate of curl: for every request curl can make, Surl is the server that answers it. Today at [StewartScottRogers/Surl](https://github.com/StewartScottRogers/Surl).
- **Surly** - a cookbook, in the Problem / Solution / Discussion form, in which every recipe pairs a `surl` command that listens with a `curl` command that talks to it.

## Layout

| Folder | Holds |
|---|---|
| `Curl/` | The Curl solution, once migrated here with its history |
| `Surl/` | The Surl solution, once migrated here with its history |
| `Book/` | The Surly cookbook |
| `Documentation/Planning/Decisions/` | Architecture decision records for the monorepo itself |

`Surly.slnx` at the root will reference both solutions' projects once they are migrated.

## Status

Skeleton only. The two solutions have not been migrated and the book has not been started. Decisions still open are listed in `Documentation/Planning/Decisions/README.md`.
