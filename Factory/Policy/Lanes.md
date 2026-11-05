# Lanes: the roles

A lane is one agent run on one task. The runner tells it its role; the role says what it may touch.

| Role | Takes | May change | Must not |
|---|---|---|---|
| author | recipe, revision | The recipe's folder, the bibliography, a chapter or part file it creates | Any other recipe; `verified.yml`; anything under `Factory/` |
| reviewer | a recipe another lane finished | `review.md` beside the recipe; minor fixes it lists there | The specification's cases (it may add a case it finds missing, and must say so) |
| verifier | verify | `verified.yml` of every recipe; new verify, revision and program tasks on the boards | Any prose |
| planner | the board, when `Backlog/` runs low | New tasks in `Backlog/`, from the Surl and Curl documentation and from the inbox answers | Any task in `Doing/` |
| mechanic | factory | `Factory/`, `Book/build/`, the templates; the decision records | Any recipe |

Every role reads `Book/AI.md` and `Policy/` first and writes its Log entry last. A reviewer is always a fresh run that has not seen the author's run. Roles may propose changes to their own instructions as factory tasks; they may not change them in the same run.
