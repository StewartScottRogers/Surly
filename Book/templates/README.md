# Templates

To add a recipe: copy `recipe-template/` to `parts/<part>/chapters/<chapter>/recipes/<key>/`, rename every `KEY` to the key (the folder name), fill `recipe.yml` (one case per claim the Discussion will make; exactly one with `role: solution`), write the four sections in `recipe.adoc`, then run lint, verify and build (see `../build/README.md`). The build refuses a recipe whose `key` differs from its folder name, and lint refuses one whose Discussion claims are not cases.

| Template | For a recipe whose subject is | Cases |
| --- | --- | --- |
| `recipe-template/` | `surl` and `curl` on the console | `run:`, a shell line |
| `recipe-template-csharp/` | `Surl.Api`, `Curl.Api` or `Libcurl` from .NET code (ADR-0008) | `program:`, a C# file-based app beside `recipe.yml`, run with `dotnet run` |

For a C# recipe, also: rename `program.cs` to what it does (`fetch.cs`), list every public type it uses in `api-types` and explain each in the Discussion, put it in a chapter of its own inside its tier (a `Libcurl` recipe only in `advanced`), and run `python3 build/pin.py <package> <version>` rather than typing a version: the `VERSION` placeholders become the book's pins. MCP recipes (`mcp:` cases) are decided in ADR-0008 but not built yet.
