#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Joins the parts in the order given and compares the result with the original.
# Usage: sh check.sh <part>...
cat "$@" > joined
echo "joined: $(wc -c < joined | tr -d " ") bytes"
if cmp -s joined site/big.bin; then echo "identical"; else echo "differs"; exit 1; fi
