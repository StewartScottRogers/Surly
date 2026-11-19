#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
"""Model Context Protocol (MCP) clients for verify (ADR-0008 item 7).

BridgeHost, the default, drives a host through McpBridge.cs: a client built on the
official C# MCP SDK, so the hosts are judged by a real client and not by one the book
wrote. Host is the original standard-library client, kept for a machine without .NET
(SURLY_MCP_CLIENT=python). Both read each tool's input schema from tools/list, so verify
can check a recipe's arguments against it before calling (schema_problems)."""
import json, queue, subprocess, threading

PROTOCOL_VERSION = "2025-06-18"


class McpError(Exception):
    pass


class Host:
    def __init__(self, cmd, cwd, env=None, timeout=30):
        self.timeout = timeout
        self.proc = subprocess.Popen(cmd, cwd=cwd, env=env, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                     stderr=subprocess.PIPE, text=True, encoding="utf-8")
        self.lines, self.errors, self.next_id = queue.Queue(), [], 0
        threading.Thread(target=self._read, args=(self.proc.stdout, self.lines.put), daemon=True).start()
        threading.Thread(target=self._read, args=(self.proc.stderr, self._keep_error), daemon=True).start()
        reply = self.request("initialize", {"protocolVersion": PROTOCOL_VERSION, "capabilities": {},
                                            "clientInfo": {"name": "surly-verify", "version": "1"}})
        info = reply.get("serverInfo", {})
        self.name, self.version = info.get("name", "?"), info.get("version", "?")
        self._send({"jsonrpc": "2.0", "method": "notifications/initialized"})
        self.schemas = self._list_tools()

    def _list_tools(self):
        """{tool name: input schema}, following nextCursor through every page of tools/list."""
        schemas, cursor = {}, None
        while True:
            page = self.request("tools/list", {"cursor": cursor} if cursor else {})
            schemas.update({t["name"]: t.get("inputSchema") or {} for t in page.get("tools", [])})
            cursor = page.get("nextCursor")
            if not cursor:
                return schemas

    def check(self, tool, arguments):
        """Why these arguments do not fit the tool's published input schema; [] when they do."""
        if tool not in self.schemas:
            return [f"{self.name} has no tool {tool} (it has {', '.join(sorted(self.schemas))})"]
        return schema_problems(self.schemas[tool], arguments, "arguments")

    @staticmethod
    def _read(stream, put):
        for line in stream:
            put(line)
        put(None)

    def _keep_error(self, line):
        if line is not None:
            self.errors = (self.errors + [line.rstrip()])[-10:]

    def _send(self, message):
        try:
            self.proc.stdin.write(json.dumps(message) + "\n")
            self.proc.stdin.flush()
        except OSError as e:
            raise McpError(f"host closed its input: {e}; stderr: {' | '.join(self.errors)}")

    def request(self, method, params):
        self.next_id += 1
        self._send({"jsonrpc": "2.0", "id": self.next_id, "method": method, "params": params})
        while True:
            try:
                line = self.lines.get(timeout=self.timeout)
            except queue.Empty:
                raise McpError(f"{method} had no answer in {self.timeout} seconds")
            if line is None:
                raise McpError(f"host exited during {method}; stderr: {' | '.join(self.errors)}")
            message = json.loads(line)
            if message.get("id") != self.next_id:
                continue                      # a notification, or a request to the client we do not serve
            if "error" in message:
                raise McpError(f"{method}: {message['error'].get('message', message['error'])}")
            return message["result"]

    def call(self, tool, arguments):
        """Call a tool; returns (is_error, value, text). value is structuredContent when the
        host gives it, else the text content parsed as JSON when it is JSON, else the text."""
        result = self.request("tools/call", {"name": tool, "arguments": arguments})
        text = "".join(c.get("text", "") for c in result.get("content", []) if c.get("type") == "text")
        return answer(result.get("isError"), result.get("structuredContent"), text)

    def close(self):
        try:
            self.proc.stdin.close()
            self.proc.wait(5)
        except (OSError, subprocess.TimeoutExpired):
            self.proc.kill()
            self.proc.wait()


def answer(is_error, structured, text):
    """(is_error, value, text): value is structuredContent when the host gives it, else the
    text parsed as JSON when it is JSON, else the text."""
    value = structured
    if value is None:
        try:
            value = json.loads(text)
        except ValueError:
            value = text
    return bool(is_error), value, text


class BridgeHost:
    """A host driven through McpBridge.cs (the C# SDK). Same face as Host: name, version,
    schemas, check(), call(), close()."""
    def __init__(self, bridge_cmd, host_cmd, cwd, env=None, timeout=30):
        self.timeout = timeout
        self.proc = subprocess.Popen(bridge_cmd + ["--"] + host_cmd, cwd=cwd, env=env, stdin=subprocess.PIPE,
                                     stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding="utf-8")
        self.lines, self.errors = queue.Queue(), []
        threading.Thread(target=Host._read, args=(self.proc.stdout, self.lines.put), daemon=True).start()
        threading.Thread(target=Host._read, args=(self.proc.stderr, self._keep_error), daemon=True).start()
        hello = self._next("the handshake", 120)      # the first start of a .NET program is slow
        self.name, self.version = hello.get("name", "?"), hello.get("version", "?")
        self.protocol_version = hello.get("protocolVersion")
        self.schemas = hello.get("tools") or {}

    _keep_error = Host._keep_error
    check = Host.check

    def _next(self, what, timeout=None):
        try:
            line = self.lines.get(timeout=timeout or self.timeout)
        except queue.Empty:
            raise McpError(f"{what} had no answer in {timeout or self.timeout} seconds")
        if line is None:
            raise McpError(f"the bridge exited during {what}; stderr: {' | '.join(self.errors)}")
        return json.loads(line)

    def call(self, tool, arguments):
        try:
            self.proc.stdin.write(json.dumps({"tool": tool, "arguments": arguments}) + chr(10))
            self.proc.stdin.flush()
        except OSError as e:
            raise McpError(f"the bridge closed its input: {e}; stderr: {' | '.join(self.errors)}")
        reply = self._next(f"tools/call {tool}")
        if "failure" in reply:
            raise McpError(f"{tool}: {reply['failure']}")
        return answer(reply.get("isError"), reply.get("structured"), reply.get("text") or "")

    close = Host.close


JSON_TYPES = {"string": str, "boolean": bool, "object": dict, "array": list, "null": type(None)}


def type_fits(value, wanted):
    if wanted == "integer":
        return isinstance(value, int) and not isinstance(value, bool)
    if wanted == "number":
        return isinstance(value, (int, float)) and not isinstance(value, bool)
    return isinstance(value, JSON_TYPES.get(wanted, object))


def schema_problems(schema, value, where):
    """The parts of JSON Schema a tool's inputSchema uses for its arguments: type (one or a
    list), properties, required, additionalProperties: false, items and enum. A name the
    schema does not list is a problem whether or not additionalProperties is given, since a
    renamed argument is exactly what this check is for."""
    found = []
    types = schema.get("type")
    if types:
        types = types if isinstance(types, list) else [types]
        if not any(type_fits(value, t) for t in types):
            return [f"{where} is {type(value).__name__}, schema wants {' or '.join(types)}"]
    if "enum" in schema and value not in schema["enum"]:
        found.append(f"{where} is {value!r}, schema allows {schema['enum']}")
    if isinstance(value, dict) and ("properties" in schema or "required" in schema):
        props = schema.get("properties") or {}
        for name in schema.get("required") or []:
            if name not in value:
                found.append(f"{where}.{name} is required by the schema")
        for name, item in value.items():
            if name in props:
                found += schema_problems(props[name] or {}, item, f"{where}.{name}")
            elif schema.get("additionalProperties") is not True and props:
                found.append(f"{where}.{name} is not in the schema (it has {', '.join(props)})")
    if isinstance(value, list) and isinstance(schema.get("items"), dict):
        for i, item in enumerate(value):
            found += schema_problems(schema["items"], item, f"{where}[{i}]")
    return found


def contains(actual, expected):
    """True when expected is a subset of actual: every key of an expected object is in the
    actual object with a value that contains it, every item of an expected list is contained
    by some item of the actual list (in any order), and anything else is equal."""
    if isinstance(expected, dict):
        return isinstance(actual, dict) and all(k in actual and contains(actual[k], v) for k, v in expected.items())
    if isinstance(expected, list):
        return isinstance(actual, list) and all(any(contains(a, e) for a in actual) for e in expected)
    return actual == expected


def path_value(value, path):
    """bound_urls.0 -> value['bound_urls'][0]; None when the path is not there."""
    for part in str(path).split("."):
        if isinstance(value, list) and part.isdigit() and int(part) < len(value):
            value = value[int(part)]
        elif isinstance(value, dict) and part in value:
            value = value[part]
        else:
            return None
    return value
