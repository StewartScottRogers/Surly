# Defaults: what the factory decides for itself

A lane never asks a question. When it reaches one of these decisions it applies the default, writes "default taken: <which>" in the task's Log, marks the output `decision-pending`, and appends the question to `../Decisions-Inbox.md` with the task key. The author's later answer, if different, becomes a revision task. A decision not listed here is taken on the most reasonable reading and listed here afterwards by the lane, as a new row, so the list grows with the work.

| Decision | Default |
|---|---|
| Which tier a recipe belongs in | By the hardest thing it does: login or encryption → secure-servers; uploads, listings, state or several listeners → working-servers; otherwise foundations; interoperability, edge cases or library clients → advanced; a protocol released in neither program within the last year → new-protocols |
| Which chapter | The existing chapter whose task matches; if none, a new chapter named for the task, not the protocol |
| The recipe's title | A verb phrase naming the task, under eight words, no protocol acronym unexpanded |
| A failing case: program or book | The program, if upstream curl or the program's own documentation agrees with the recipe's claim; otherwise the book. Write the reasoning in the Log |
| Which program a fault belongs to | Surl if the server answered wrongly or not at all; Curl if the request or the exit code was wrong; both if unclear, one task each |
| A claim with no source | Not written. A case is written instead, and the claim follows what the case proves |
| Whether a protocol is stable enough to move forward from new-protocols | Released in both programs and verified on all three platforms against those releases |
| A reference whose licence allows archiving | Archive it. Otherwise record the key and address only |
| Something the schema cannot express | Do not improvise in the prose; park the task and write a factory task proposing the schema change |
| Conflicting instructions between files | `Book/AI.md` over `Policy/`, `Policy/` over a task, a decision record over all three; then the newest |
| Anything irreversible outside this repository (a release, a deletion, a public post) | Never by default. Park the task; inbox the question |
