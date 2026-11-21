#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
"""Pin a package at a version for the whole book (ADR-0008 item 6): rewrite its line in
build/packages.yml and every `#:package <name>` line in every recipe's C# programs.
Then run verify.py, since every recipe that uses the package must pass again.

Usage:  python3 build/pin.py <package> <version>     e.g. python3 build/pin.py Surl.Api 1.0.0
Exit:   0 when pinned; 1 when the package is not in packages.yml.
"""
import re, sys
from recipes import PACKAGES_FILE, pinned_packages, programs, recipes


def main():
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    name, version = sys.argv[1], sys.argv[2]
    if name not in pinned_packages():
        sys.exit(f"{name} is not in {PACKAGES_FILE.name}; add it there first")
    text = PACKAGES_FILE.read_text(encoding="utf-8")
    # Quoted, so a floating version ("*", the newest the feeds hold) stays a string in YAML.
    PACKAGES_FILE.write_text(re.sub(rf"^{re.escape(name)}:.*$", f'{name}: "{version}"', text, flags=re.M), encoding="utf-8")
    line = re.compile(rf"^#:package\s+{re.escape(name)}(?:@\S+)?[ \t]*$", re.M)
    changed = 0
    for _, _, recipe, spec in recipes():
        for program in programs(spec):
            path = recipe / program
            source = path.read_text(encoding="utf-8")
            pinned = line.sub(f"#:package {name}@{version}", source)
            if pinned != source:
                path.write_text(pinned, encoding="utf-8", newline="")   # keep the file's line endings
                changed += 1
    print(f"{name} pinned at {version}; {changed} program(s) rewritten. Now run verify.py.")


if __name__ == "__main__":
    main()
