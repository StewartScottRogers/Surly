# Templates

To add a recipe: copy `recipe-template/` to `parts/<part>/chapters/<chapter>/recipes/<key>/`, rename every `KEY` to the key (the folder name), fill `recipe.yml` (one case per claim the Discussion will make; exactly one with `role: solution`), write the four sections in `recipe.adoc`, then run lint, verify and build (see `../build/README.md`). The build refuses a recipe whose `key` differs from its folder name, and lint refuses one whose Discussion claims are not cases.

| Template | For a recipe whose subject is | Cases |
| --- | --- | --- |
| `recipe-template/` | `surl` and `curl` on the console | `run:`, a shell line |
| `recipe-template-csharp/` | `Surl.Api`, `Curl.Api` or `Libcurl` from .NET code (ADR-0008) | `program:`, a C# file-based app beside `recipe.yml`, run with `dotnet run` |
| `recipe-template-mcp/` | `surl-mcp` and `curl-mcp`, driven by an AI agent (ADR-0008) | `program:`, a C# file-based app on the official MCP SDK that starts both hosts from PATH and calls their tools |

For a C# recipe, also: rename `program.cs` to what it does (`fetch.cs`), list every public type it uses in `api-types` and explain each in the Discussion, put it in a chapter of its own inside its tier (a `Libcurl` recipe only in `advanced`), and run `python3 build/pin.py <package> <version>` rather than typing a version: the `VERSION` placeholders become the book's pins.

For an MCP recipe: start from `program.cs`, which starts both hosts by name over stdio with `StdioClientTransport` and confines them with `SURL_MCP_ROOT` and `CURL_MCP_ROOT` (an environment variable, never a path argument: the SDK runs a host through `cmd.exe` on Windows, which breaks on a path with a space). Take each tool's argument names from its input schema (`ListToolsAsync`), list every tool the programs call in `mcp-tools` and explain each in the Discussion, pin `ModelContextProtocol.Core` with `build/pin.py` like any package, and put it in a chapter of MCP recipes inside its tier. The build writes `mcp.json`, a client configuration for the two hosts, beside `run.sh` and `run.ps1`.
