#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
"""Print each MCP host's tools, one line each: name(required*, optional) - first sentence.

  python3 build/tools.py [surl-mcp|curl-mcp] [tool ...]

The hosts are SURLY_SURL_MCP and SURLY_CURL_MCP (or on PATH). Name tools to print their full
input schema instead. Argument names here are the ones a recipe's C# program must use.
"""
import json, os, shutil, sys, tempfile
from mcp import Host


def main():
    names = [a for a in sys.argv[1:] if a in ("surl-mcp", "curl-mcp")] or ["surl-mcp", "curl-mcp"]
    wanted = [a for a in sys.argv[1:] if a not in ("surl-mcp", "curl-mcp")]
    root = tempfile.mkdtemp(prefix="surly-tools-")
    try:
        for name in names:
            exe = os.environ.get("SURLY_" + name.upper().replace("-", "_")) or shutil.which(name)
            env = dict(os.environ, CURL_MCP_ROOT=root, SURL_MCP_ROOT=root)
            host = Host([exe], cwd=root, env=env)
            tools, cursor = [], None
            while True:
                page = host.request("tools/list", {"cursor": cursor} if cursor else {})
                tools += page.get("tools", [])
                cursor = page.get("nextCursor")
                if not cursor:
                    break
            host.close()
            print(f"## {name} {host.version}: {len(tools)} tools")
            for t in tools:
                schema = t.get("inputSchema") or {}
                if wanted and t["name"] in wanted:
                    print(t["name"], json.dumps(schema, indent=1))
                elif not wanted:
                    req = set(schema.get("required") or [])
                    args = ", ".join(a + ("*" if a in req else "") for a in (schema.get("properties") or {}))
                    about = (t.get("description") or "").split(". ")[0][:110]
                    print(f"{t['name']}({args}) - {about}")
    finally:
        shutil.rmtree(root, ignore_errors=True)


if __name__ == "__main__":
    main()
