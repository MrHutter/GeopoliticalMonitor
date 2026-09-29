---
name: coding-task-brief
description: "Write a self-contained, paste-ready coding task for Claude Code or Codex with owned/off-limits files, acceptance tests, and handoff requirements. Use when preparing or revising a coding task."
---

# Coding task brief

Produce one task a coding tool can execute without seeing this conversation. Surgical scope, testable result.

## Before writing

- Base the task on the current repo state and the project's design authority, not on memory of an older checkpoint. If the current HEAD or dirty state is unknown, make step 1 of the task a read-only reconciliation pass (see the repo-reconcile skill) and make the rest conditional on it.
- Keep scope narrow: one outcome per task. If the request covers several independent changes, propose splitting it and say how.
- Correctness and tests come before UI polish. Do not invent redesigns the owner did not ask for.
- Never put secrets or API keys in the task, and never ask the owner to paste one into chat or tool output.

## Output rules

- Deliver the entire task as ONE self-contained, paste-ready code block. When revising, output the whole revised task again, never a diff or "change step 3 to…".
- If the task itself contains triple-backtick code blocks, wrap the whole task in a four-backtick outer fence.
- Outside the block, add at most two lines: what the task does and anything the owner must decide first.

## Task template

```
# Task: <short imperative title>

## Context
<Why this is needed, in 2–4 sentences. The design rule or authority it follows.>

## Before starting
1. Read root and nested AGENTS.md (and CLAUDE.md) and follow them throughout.
2. Inspect the full working tree (not just HEAD): git status, staged, untracked.
   Treat existing uncommitted changes as owner/other-tool work. Never reset,
   stash, clean, discard, or overwrite them.
3. Confirm HEAD is <expected SHA or "the latest accepted checkpoint">; if not,
   stop and report.

## Scope
Owns (may change): <files/areas>
Must not touch: <files/areas>
If the task cannot be done within this scope, stop and report instead of
expanding it.

## Required change
<Precise behaviour to implement. Numbered steps if order matters.>

## Constraints
- <design/architecture/security rules that apply>
- No speculative fixes outside the stated problem.
- Do not commit or push.

## Tests
- Add/update: <specific tests and what they must prove>
- Run: <exact commands> — all must pass with 0 warnings/errors.
- Also run: git diff --check

## Acceptance criteria
- <observable, checkable statements>

## Finish with a handoff report
- Exact files changed and why
- Exact commands run with results (pass/fail counts)
- Final git status and diff stat; confirm nothing committed or pushed
- Anything the owner must verify manually, with the exact current
  PowerShell launch command for manual testing
```

Drop sections that genuinely do not apply; never leave placeholders in the final task.
