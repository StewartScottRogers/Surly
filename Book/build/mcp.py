#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
"""The smallest Model Context Protocol (MCP) client verify needs: start surl-mcp or
curl-mcp, do the initialize handshake, and read the version the host reports (curl-mcp
has no --version). Recipes themselves drive the hosts from C# on the official MCP SDK
(ADR-0008 item 7, amended 2026-11-19). Standard library only."""
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


    def close(self):
        try:
            self.proc.stdin.close()
            self.proc.wait(5)
        except (OSError, subprocess.TimeoutExpired):
            self.proc.kill()
            self.proc.wait()

