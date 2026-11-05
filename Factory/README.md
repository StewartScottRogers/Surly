# Factory

The unattended production line that writes and maintains the book (ADR-0006). It is policy, not machinery: read the four files in `Policy/` and you know everything it will do.

| Where | What |
|---|---|
| `Policy/Defaults.md` | What the factory decides on its own, and how, when a human would otherwise be asked |
| `Policy/Gates.md` | What a task must pass before it is done |
| `Policy/Lanes.md` | The roles a lane can take and what each may touch |
| `Policy/Done.md` | The definition of done for each kind of task |
| `Decisions-Inbox.md` | Questions the author answers whenever he likes; each answer becomes a revision task |
| `Tasks/` | The board: `Backlog/`, `Doing/`, `Done/`, `Parked/`, one Markdown file per task |
| `Lanes/` | Runtime state of running lanes (not committed) |
| `factory.py` | The runner: takes the next ready task and hands it to the configured agent command |
| `factory.yml` | The agent command, lane count, shift length |

## Run a shift

```
python3 Factory/factory.py --hours 4 --lanes 2
```

The runner reads `factory.yml`, and for each lane: moves the next ready task from `Backlog/` to `Doing/`, runs the agent command with the task file and `Policy/` as its brief, runs the gates, and moves the task to `Done/` or back to `Backlog/` with the failure appended. It stops when nothing is ready or the shift ends. It never asks a question.

## Task file

```
# <title>
key: <kebab-case id>
kind: recipe | revision | verify | program-task | factory
depends: [<keys>]
status: ready | done | parked
## Why
## Acceptance
- [ ] checkable criterion
## Log
- <date> <lane>: what happened, including any default taken
```
