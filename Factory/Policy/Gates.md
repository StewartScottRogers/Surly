# Gates: what a task must pass

A task is not done until every gate for its kind passes. The runner runs them; a lane may run them early as often as it likes.

| Kind | Gates, in order |
|---|---|
| recipe, revision | `python3 Book/build/lint.py` clean (forward references allowed); `python3 Book/build/verify.py <key>` run, with the result recorded whatever it is; `python3 Book/build/build.py` clean; a `review.md` beside the recipe written by a review lane that did not write the recipe, verdict `accept`; the copyright line in every new file; a Log entry naming every default taken |
| verify | `verify.py` run against the named binaries on this platform; every `broken` recipe has either a fix task or a program task on the board |
| program-task | The task file exists on the target board in that board's format, with `recipe.yml` and the failing output attached, and the recipe's `verified.yml` points at it |
| factory | The change is to `Factory/` or `Book/build/` only; every existing task still parses; `lint.py`, `verify.py` and `build.py` still run on the existing recipes; a decision record if the schema or a policy changed |

What no gate can pass for a lane: writing the author's accept in the editorial log, changing `LICENSE*`, `RIGHTS.md` or `book.yml`, or anything irreversible outside this repository.
