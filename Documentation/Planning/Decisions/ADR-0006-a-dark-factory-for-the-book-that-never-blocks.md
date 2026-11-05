# ADR-0006: A dark factory for the book that never blocks, feeds the program boards, and is built as policy on a runtime-neutral harness

## Status

Accepted, 2026-11-05.

## Context

The book is to be written and maintained for twenty years by an unattended production line, the way Curl and Surl are built, with the author setting direction and never being interrupted. The Curl and Surl factories stop a shift when a task needs a human decision; the author wants a factory that keeps running. The agents inside it will be much stronger in one or two release cycles, before the book is written in earnest, so the factory is designed for those agents, not today's. When a recipe exposes a fault or a gap in either program, the factory should hand the program's own factory the work.

## Decision

1. **Never block.** Every class of decision the factory may face has a written default in `Factory/Policy/Defaults.md`. When a task reaches such a decision, the lane applies the default, records in the task that it did and why, marks the result `decision-pending`, and appends the question to `Factory/Decisions-Inbox.md`. The author answers the inbox whenever he likes; each answer becomes a revision task. A task that cannot proceed even on a default is moved to `Parked` with the reason and the lane takes the next ready task. The board never stalls on one card.
2. **Proposed until the author decides; published only on his line.** A recipe that passes lint, verify and review is `proposed`. It appears in editions marked as such. It becomes `published` only when the author's accept is written in `Book/editorial/log.md`, by him or by the factory copying his answer from the inbox. The factory never writes an accept he did not give. This keeps the record of human authorship on which ADR-0003 rests.
3. **Program issues become program tasks.** When verification fails and the lane judges the fault to be in Surl or Curl, it writes a task onto that program's board (`Tasks/Backlog` in that repository, in that board's own format), attaching the recipe's `recipe.yml` as the reproduction and the failing case's output, and marks the recipe `broken` with a pointer to the task. A recipe that cannot be written because a program lacks the feature does the same. When the program releases, the book's verify run closes the loop.
4. **Policy over procedure; runtime-neutral.** The factory is a set of plain-text standards (the policy defaults, the gates, the definition of done, the lane roles) and a thin runner. Tasks are Markdown files; the board is folders; gates are scripts; the agent that works a task is a configured command, not a named product. Any agent runtime that can read a folder and run a shell can be the lane. Agents may improve their own instructions within the gates, and the review lane is always a separate run that has not seen the work being written.
5. **The existing DarkFactory is not the base.** The book's factory is a new design in `Factory/` in this repository. The Curl and Surl boards migrate onto it when the programs migrate into Surly; until then they run as they do today and the book's factory writes onto their boards in their format.
6. **First job: build and prove itself, then wait.** The factory's first tasks are its own: the runner, the gates wired in, the inbox, and a handful of recipes drafted from the Surl README to prove the loop end to end. It then runs small until released binaries and stronger agents exist; writing the book in earnest is a later shift.

## Consequences

- The author's only standing duties are the decisions inbox and the editorial log, both answered in batches at his convenience.
- Quality rests on the gates and the review lane, which scale with the agents; the runner is deliberately too thin to matter.
- Three boards exist until migration; cross-board writes are the only coupling.
