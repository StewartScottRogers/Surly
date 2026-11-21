#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Makes the same four symbolic links in site and in plain. MSYS asks Git Bash on Windows
# for real links; other systems ignore it.
export MSYS=winsymlinks:nativestrict
for root in site plain; do
  ln -s docs/guide.txt "$root/latest.txt"          # a file link that stays inside
  ln -s docs "$root/current"                        # a folder link that stays inside
  ln -s ../outside/secret.txt "$root/leak.txt"      # a link that leaves the folder
  ln -s leak.txt "$root/hop.txt"                    # a link inside to a link that leaves
done
