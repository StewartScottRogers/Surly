# Done

| Kind | Done means |
|---|---|
| recipe | All gates passed; the recipe's status is `proposed`; its line is in the Decisions-Inbox for the author's accept |
| revision | The author's answer or the reviewer's findings are applied; gates passed; the recipe's status is whatever the author's last line in the editorial log says |
| verify | Every recipe has a current `verified.yml` for these binaries on this platform; every `broken` one has a task; the book is rebuilt |
| program-task | Written on the target board; pointer recorded |
| factory | Gates passed; the runner still runs a shift on the existing board |

`published` is not a state the factory reaches. Only the author's accept in `Book/editorial/log.md` moves a recipe from `proposed` to `published`, and the next build shows it.
