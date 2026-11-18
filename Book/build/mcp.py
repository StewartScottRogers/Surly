#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
"""The smallest Model Context Protocol (MCP) client verify needs (ADR-0008 item 7):
start a host on standard input and output, do the initialize handshake, call
tools, and stop it. Messages are JSON-RPC 2.0, one per line. Nothing but the
standard library, so verify keeps its one dependency (PyYAML)."""
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
        value = result.get("structuredContent")
        if value is None:
            try:
                value = json.loads(text)
            except ValueError:
                value = text
        return bool(result.get("isError")), value, text

    def close(self):
        try:
            self.proc.stdin.close()
            self.proc.wait(5)
        except (OSError, subprocess.TimeoutExpired):
            self.proc.kill()
            self.proc.wait()


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
