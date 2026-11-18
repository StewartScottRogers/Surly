<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Chapter list

Status: **accepted** by the author, 2026-11-18. Part 1 is written first and reviewed before Part 2; Part 5 keeps all twenty recipes and is written last.

Rules it follows: parts are the difficulty tiers (ADR-0004, `AI.md`); chapters are named for the task, not the protocol (`Factory/Policy/Defaults.md`); keys start with the protocol and name the task; C# and MCP recipes are recipes of their own in chapters of their own inside each tier (ADR-0008); `Libcurl` only in `advanced`. Every recipe is drawn from something `surl`, `Surl.Api` or `surl-mcp` documents today (the Surl README, `Surl.Api/README.md`, `Surl.Mcp/README.md`).

Surface: **con** console (`surl` + `curl`), **c#** C# program, **mcp** MCP tool calls.

## Part 1: Foundations (one listener, one request, no login, no encryption)

### 1. Serving files
| Key | Surface | Task |
|---|---|---|
| `http-serve-a-directory` | con | Serve a folder over HTTP (written) |
| `http-serve-an-empty-store` | con | Run with no `--directory`: everything is 404 until something is stored |
| `http-show-the-response-headers` | con | See status and headers (`-i`, `-I`) |
| `http-hide-and-show-dot-files` | con | Dot files are never served; `--serve-dot-files`; `.surl` never |
| `http-choose-the-http-version` | con | HTTP/1.0, 1.1, and HTTP/2 prior knowledge on `http` |

### 2. Fetching over the simpler protocols
| Key | Surface | Task |
|---|---|---|
| `ftp-download-anonymously` | con | `--allow-anonymous` for a test, and why it is not the default |
| `tftp-download-a-file` | con | TFTP read |
| `dict-look-up-a-word` | con | DICT |
| `gopher-fetch-a-menu` | con | Gopher |
| `ws-receive-a-file-as-a-message` | con | WebSocket upgrade; 404 and exit 22 on a missing path |

### 3. Watching and asking surl
| Key | Surface | Task |
|---|---|---|
| `http-log-every-exchange` | con | Levels: `-s`, default, `-v`, `--log-level`, `--log-file` |
| `http-trace-every-byte` | con | `--trace-ascii -`, `--trace-time` |
| `surl-ask-for-help-as-an-agent` | con | `--aihelp`, topics, `exit-codes` |
| `surl-read-the-exit-codes` | con | Refused scheme (1), missing certificate (58), path in use (124) |

### 4. Serving and fetching from C#
| Key | Surface | Task |
|---|---|---|
| `http-serve-and-fetch-in-process` | c# | `Surl.Api` serves, `Curl.Api` fetches, no processes |
| `http-fetch-from-a-surl-process` | c# | `Curl.Api` as the client of a `surl` process |
| `http-record-what-the-client-sent` | c# | `RecordExchanges`, `server.Exchanges` |
| `http-catch-a-refused-start` | c# | `SurlServerStartException` and `SurlExitCode` |

### 5. Serving and fetching from an agent
| Key | Surface | Task |
|---|---|---|
| `http-serve-and-fetch-from-an-agent` | mcp | `start_server`, then `transfer` |
| `http-run-curl-from-an-agent` | mcp | curl-mcp's `curl` tool: arguments, exit code, output |
| `http-see-what-the-client-sent-from-an-agent` | mcp | `get_exchanges` |
| `http-stop-a-server-from-an-agent` | mcp | `list_servers`, `stop_server`, exit code |

## Part 2: Working servers (uploads, listings, several listeners, state)

### 6. Accepting uploads and listing folders
| Key | Surface | Task |
|---|---|---|
| `http-accept-uploads` | con | `--allow-uploads`, `-T`; why non-GET needs an account |
| `http-list-a-directory` | con | `--list-directories` |
| `ftp-list-and-upload` | con | FTP listing and `-T` |
| `http-keep-files-across-restarts` | con | `--directory` persistence, `.surl/` |
| `http-refuse-a-second-server-on-one-folder` | con | Exit 124 |

### 7. Running a mail server
| Key | Surface | Task |
|---|---|---|
| `smtp-send-and-read-it-over-imap` | con | One store, three listeners, `--allow-anonymous` |
| `pop3-retrieve-and-delete-a-message` | con | `RETR`, `-X 'DELE 1'` |
| `imap-search-a-mailbox` | con | `?SUBJECT`, `;UID=` |
| `imap-append-and-flag-a-message` | con | `-T` to a mailbox, `STORE`, `EXPUNGE` |

### 8. One server, many protocols
| Key | Surface | Task |
|---|---|---|
| `ftp-upload-and-fetch-over-http` | con | One content store across protocols |
| `mqtt-publish-and-subscribe` | con | MQTT on the shared message bus |
| `http-listen-on-several-urls` | con | Several listen URLs, one process |

### 9. Configuring a real server
| Key | Surface | Task |
|---|---|---|
| `surl-read-options-from-a-file` | con | `-K surl.cfg` |
| `smtp-serve-a-mail-domain` | con | `--mail-domain`, `--mailbox`, relaying denied |
| `http-share-a-folder-read-only` | con | `--share`, `--share-access` |
| `mqtt-grant-a-topic` | con | `--topic`, `--topic-access` |

### 10. Driving a stand-in from C#
| Key | Surface | Task |
|---|---|---|
| `smtp-seed-and-read-mail-in-process` | c# | Seeding the mail store |
| `http-reset-and-snapshot-in-process` | c# | Reset, snapshot, restore between tests |
| `http-wait-for-an-upload-in-process` | c# | Event queries and waits |

### 11. Driving a stand-in from an agent
| Key | Surface | Task |
|---|---|---|
| `http-put-and-delete-files-from-an-agent` | mcp | `put_file`, `delete_file` |
| `smtp-seed-mail-from-an-agent` | mcp | `seed_mail`, then IMAP through `curl` |
| `http-wait-for-an-event-from-an-agent` | mcp | `wait_for_event`, `get_events` |
| `http-reset-and-snapshot-from-an-agent` | mcp | `reset_server`, `snapshot_server`, `restore_server` |
| `mqtt-publish-from-an-agent` | mcp | `publish_message` |

## Part 3: Secure servers (accounts, TLS, host keys, certificates)

### 12. Requiring a login
| Key | Surface | Task |
|---|---|---|
| `http-require-a-login` | con | `--user`; everything needs a login once one account exists (linked from recipe 1) |
| `http-keep-passwords-out-of-the-process-list` | con | `--user-file` |
| `http-log-in-with-digest` | con | `--digest`, MD5 and SHA-256 |
| `http-accept-a-bearer-token` | con | Empty user name holds a token, `--oauth2-bearer` |
| `http-refuse-a-password-in-clear` | con | 403; `--allow-plaintext-auth` |

### 13. Serving over TLS
| Key | Surface | Task |
|---|---|---|
| `https-serve-with-a-self-signed-certificate` | con | `--self-signed`, `-k` (linked from recipe 1) |
| `https-serve-with-your-own-certificate` | con | `--cert`, `--key`, `--cacert`; exit 58 without |
| `ftp-protect-the-login-with-tls` | con | `AUTH TLS`, `--ssl-reqd`; and `ftps` |
| `smtp-require-starttls` | con | STARTTLS / STLS, `smtps`, `imaps`, `pop3s` |
| `wss-log-in-over-tls` | con | Basic over `wss` |

### 14. Serving over SSH
| Key | Surface | Task |
|---|---|---|
| `sftp-serve-with-a-throwaway-host-key` | con | `--throwaway-hostkey`, `-k` |
| `scp-pin-the-host-key` | con | `--hostkey`, `-v`, `--hostpubsha256` |
| `sftp-log-in-with-a-public-key` | con | `--authorized-keys` |
| `sftp-upload-and-rename` | con | `--allow-uploads`, `-Q` |

### 15. Directories and Windows logins
| Key | Surface | Task |
|---|---|---|
| `ldap-search-a-directory` | con | LDIF directory, simple bind, `--allow-plaintext-auth` |
| `ldap-bind-with-ntlm` | con | `--auth ntlm` |
| `smb-read-a-share` | con | `--auth ntlmv1` |
| `http-log-in-with-ntlm` | con | NTLM and Negotiate |

### 16. Logins and TLS from code and agents
| Key | Surface | Task |
|---|---|---|
| `https-serve-self-signed-in-process` | c# | `SelfSigned`, `Accounts`, `Curl.Api` TLS options |
| `http-add-an-account-from-an-agent` | mcp | `add_account`, `remove_account` |

## Part 4: Advanced (edge cases, interoperability, Libcurl)

### 17. Newer HTTP
| Key | Surface | Task |
|---|---|---|
| `https-serve-http2` | con | ALPN `h2` |
| `http-upgrade-to-http2` | con | `Upgrade: h2c` |
| `https-serve-http3` | con | `--http3` over QUIC |

### 18. Limits and faults
| Key | Surface | Task |
|---|---|---|
| `http-limit-connections` | con | `--max-connections`, `--max-connections-per-address` |
| `http-time-out-an-idle-client` | con | `--idle-timeout`, `--max-time` |
| `http-limit-upload-size` | con | `--max-filesize` |
| `http-inject-a-fault` | con | `--fault` |
| `http-inject-a-fault-from-an-agent` | mcp | `add_fault`, `list_faults`, `remove_fault` |

### 19. Protocols for programs, not the curl tool
| Key | Surface | Task |
|---|---|---|
| `ws-echo-messages-with-libcurl` | c# | `--ws-echo`, `Libcurl` `curl_ws_send` / `curl_ws_recv` |
| `rtsp-play-a-stream-with-libcurl` | c# | `DESCRIBE`, `SETUP`, `PLAY` |
| `http-port-a-c-program-with-libcurl` | c# | Easy handle, one call for one call |

### 20. Interoperability
| Key | Surface | Task |
|---|---|---|
| `sftp-offer-weak-algorithms-for-old-clients` | con | `--allow-weak-ssh-algorithms` |
| `scp-serve-a-host-certificate` | con | `--hostcert` |
| `rtsp-ask-what-a-server-offers` | con | `OPTIONS *`, digest on the same connection |

## Part 5: New protocols (extended; move forward once released and verified on three platforms)

### 21. Object stores
`s3-put-and-get-an-object`, `az-put-and-get-a-blob`, `gs-put-and-get-an-object`, `oci-push-and-pull-an-image` (con)

### 22. Messaging
`mqtt5-publish-and-subscribe`, `amqp-send-and-receive`, `kafka-produce-and-consume`, `nats-publish-and-subscribe`, `stomp-send-to-a-queue`, `xmpp-send-a-message` (con)

### 23. Remote calls and streams
`grpc-call-a-method`, `signalr-receive-a-message`, `socketio-receive-an-event`, `webtransport-open-a-stream`, `mcp-call-a-tool`, `coap-get-a-resource` (con)

### 24. Lookup and data
`dns-look-up-a-name`, `redis-set-and-get-a-key` (con); `redis-seed-keys-from-an-agent`, `dns-add-a-zone-from-an-agent` (mcp)

## Totals

| Part | Chapters | Recipes | con | c# | mcp |
|---|---|---|---|---|---|
| 1 Foundations | 5 | 22 | 14 | 4 | 4 |
| 2 Working servers | 6 | 24 | 16 | 3 | 5 |
| 3 Secure servers | 5 | 20 | 18 | 1 | 1 |
| 4 Advanced | 4 | 14 | 10 | 3 | 1 |
| 5 New protocols | 4 | 20 | 18 | 0 | 2 |
| **Book** | **24** | **100** | **76** | **11** | **13** |

## Decided with it (2026-11-18)

1. `Factory/Policy/Defaults.md` now follows ADR-0008 item 3: only `Libcurl` recipes are advanced-only; `Surl.Api` and `Curl.Api` recipes go by difficulty.
2. Part 5 keeps all twenty recipes, written last.
3. Part 1 is written and checked first, then reviewed by the author before Part 2.
