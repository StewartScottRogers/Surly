#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# check.sh FOLDER-URL: lists the folder and fails, exit 1, naming what it finds if any .tmp file is left.
# A listing that fails is curl's exit code, so a folder that cannot be listed never passes.
names=$(curl -sS -l "$1") || exit
left=$(echo "$names" | tr -d '\r' | grep '\.tmp$' | tr '\n' ' ')
if [ -n "$left" ]; then echo "leftover: ${left% }"; exit 1; fi
echo clean
