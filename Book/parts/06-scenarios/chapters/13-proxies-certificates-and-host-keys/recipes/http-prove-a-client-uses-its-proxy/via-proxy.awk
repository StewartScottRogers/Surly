{ sub(/\r$/, "") }
# A request head in origin form (GET /path) is one the origin received.
/^[0-9a-f]+: [A-Z]+ \// { open = 1; via = 0; next }
open && /^[0-9a-f]+: Via: 1\.1 surl$/ { via = 1 }
# The blank line that ends the head.
open && /^[0-9a-f]+: ?$/ { if (via) proxied++; else direct++; open = 0 }
END {
  printf "%d via the proxy, %d direct\n", proxied, direct
  exit (direct > 0)
}
