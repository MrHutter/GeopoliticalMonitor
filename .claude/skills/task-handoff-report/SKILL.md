---
name: task-handoff-report
description: "End-of-task handoff report for multi-tool repos: exact files changed and why, exact commands and results, final git status/diff, open risks. Use when finishing a coding task."
---

# Task handoff report

Close out a coding task so the owner (or the next coding tool) can verify it without trusting your summary. Another tool will read the repo itself; this report must match what it finds.

## Hard rules

- Do not commit, push, stash, reset, or clean unless the owner explicitly asked in this session. Say plainly that nothing was committed or pushed.
- Report only what you actually did and observed. Never write "tests pass" without the command and its output from this session.
- Separate your changes from pre-existing dirty files. If a file was already modified before you started, say so and do not claim its changes.
- Never include secrets, API keys, tokens, or credential values in the report, even if they appeared in output.

## Gather evidence first

Run, in this session, just before writing the report:

- `git status --porcelain=v1 -b`
- `git diff --stat` and `git diff --cached --stat`
- `git ls-files --others --exclude-standard`
- `git log -1 --format="%H %s"`
- `git diff --check`
- The exact test/build/lint commands the task or AGENTS.md requires, capturing pass/fail counts.

If a required check could not be run (for example Unity Play Mode or a manual UI check that only the owner can do), list it under Owner verification rather than implying it passed.

## Report format

```
## Handoff — <task name>

HEAD: <full SHA> <subject> (unchanged | new commits: <list>)
Committed/pushed: No  (or exactly what, if the owner asked)

Scope owned: <files/areas this task owned>
Scope not touched: <areas explicitly left alone>

Files changed by this task:
| File | Change | Why |
|---|---|---|

Pre-existing changes left untouched: <files | none>
New untracked files: <files | none>

Commands run and results:
| Command | Result |
|---|---|
| <exact command> | <e.g. 530 passed, 0 failed, 0 warnings> |

git diff --check: <clean | issues>

Behaviour change: <what now works differently, in plain terms>
Deviations from the task: <none | what and why>
Known risks / follow-ups: <none | list>

Owner verification still needed:
- <manual/Unity/Play Mode/UI checks, with the exact launch command>
```

Keep prose short. The tables are the report.
