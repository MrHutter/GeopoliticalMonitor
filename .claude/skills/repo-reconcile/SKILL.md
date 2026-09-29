---
name: repo-reconcile
description: "Read-only repo reconciliation before any task: reads AGENTS.md/CLAUDE.md, reports branch, HEAD, dirty/staged/untracked state, remotes, and checkpoint/feature presence."
---

# Repo reconciliation (read-only)

Establish the true current state of the repository before any implementation. This pass changes nothing.

## Hard rules

- Read-only. Do not edit, create, delete, format, build, or run tests unless the owner explicitly asks in this session.
- Do not run any Git command that changes state: no `fetch`, `pull`, `checkout`, `switch`, `reset`, `stash` (push/pop/drop), `clean`, `restore`, `add`, `commit`, `push`, `rebase`, `merge`, `tag`, or config changes. `git stash list` is fine.
- Treat uncommitted changes as someone else's work in progress (the owner or another coding tool). Report them. Never discard or "tidy" them.
- Report facts you observed. Where something could not be checked, say so. Do not guess.

## Steps

1. **Instructions.** Read root `AGENTS.md` and `CLAUDE.md`, then find and read every nested `AGENTS.md`. Note any rule that constrains this session. Note contradictions between them.
2. **Checkpoint source.** Find the most recent accepted checkpoint the repo documents (a migration handoff doc under `docs/`, a checkpoint section in AGENTS.md/CLAUDE.md, or similar). Record its expected HEAD SHA and expected features. If a referenced doc path does not exist, report the mismatch and where the file actually is.
3. **Git state.** Run:
   - `git rev-parse --abbrev-ref HEAD`
   - `git log -1 --format="%H %s"`
   - `git log --oneline -10`
   - `git status --porcelain=v1 -b`
   - `git diff --stat` and `git diff --cached --stat`
   - `git ls-files --others --exclude-standard`
   - `git remote -v`
   - `git rev-parse --abbrev-ref --symbolic-full-name "@{u}"` (no upstream is a valid result)
   - `git stash list`
4. **Compare against the checkpoint.** Is HEAD the expected commit, a descendant of it (`git merge-base --is-ancestor <expected> HEAD`), or something else? List commits after the checkpoint.
5. **Expected features.** For each feature the checkpoint lists, confirm it is present by locating its defining files, types, or tests. Search; do not build. Mark each Present / Missing / Could not confirm, with the file path that proves it.
6. **Owner or other-tool changes.** Summarize dirty, staged, and untracked files by area. Flag any that overlap the task the owner described, if one was given.

## Report format

```
## Repo reconciliation — <repo name>

Instructions read: <list of AGENTS.md/CLAUDE.md paths>
Rules that constrain this session: <short list>
Instruction conflicts or broken references: <none | list>

Branch: <branch>   Upstream: <upstream | none>
HEAD: <full SHA> <subject>
Checkpoint: <expected SHA> from <doc path> → <matches | N commits ahead | diverged | not found>
Commits since checkpoint: <oneline list | none>
Remotes: <list | none>
Stashes: <list | none>

Working tree:
- Staged: <files | none>
- Modified: <files | none>
- Untracked: <files | none>
Overlap with the planned task: <none | files and why>

Expected features:
| Feature | Status | Evidence |
|---|---|---|

Ready to proceed: <yes | no — reason>
Questions for the owner: <none | list>
```

Stop after the report. Wait for the owner before any implementation.
