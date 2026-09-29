---
name: evidence-first-audit
description: "Audit-only investigation of a bug, regression, or code area: reproduce, trace, cite file:line evidence, rank findings; never change production code. Use for audits and root-cause investigations."
---

# Evidence-first audit

Find out what is actually true. Do not fix anything.

## Hard rules

- Audit only. Do not modify production code, configs, or assets. Do not "just quickly fix" something you notice.
- Temporary diagnostics (a scratch script, an extra log line, a throwaway test) are allowed only if the owner permits them. Put them outside tracked source where possible, list every one in the report, and remove them before finishing unless told to keep them.
- No speculative fixes. A hypothesis is not a finding until evidence confirms it.
- Do not commit, push, stash, reset, or clean. Leave other people's uncommitted work alone.
- Stay inside the scope the owner gave. Note out-of-scope observations briefly at the end; do not investigate them.

## Method

1. **Restate the question** in one or two sentences, with the expected behaviour and the observed behaviour.
2. **Read the rules** in AGENTS.md/CLAUDE.md and any design authority the repo names, so findings are judged against intended behaviour, not guesses.
3. **Reproduce.** Find the smallest exact steps, test, or command that shows the problem. If it cannot be reproduced, say so and what was tried.
4. **Trace.** Follow the actual code path from entry point to effect. Record each hop as `path/to/file.ext:line` with what happens there.
5. **Test hypotheses.** For each candidate cause, state what evidence would confirm or rule it out, then check. Keep ruled-out hypotheses in the report; they save the next person time.
6. **Classify** each confirmed finding by severity and confidence.

## Severity

- **Blocker:** wrong results, data loss, security exposure, or crash in normal use.
- **High:** incorrect behaviour in a real scenario, or a design-rule violation.
- **Medium:** edge-case bug, fragile code likely to break, missing test for critical behaviour.
- **Low:** clarity, dead code, minor inconsistency.

Confidence: **Confirmed** (reproduced or proven by code) or **Likely** (strong evidence, not reproduced).

## Report format

```
## Audit — <subject>

Question: <expected vs observed>
Scope: <files/areas examined>   Out of scope: <what was not examined>
Reproduction: <exact steps/command, result | not reproduced: what was tried>

Root cause: <one paragraph, or "not established">

Findings (most severe first):
### 1. <title> — <severity>, <confidence>
Evidence: <file:line references and observed output>
Impact: <what goes wrong, for whom>
Fix direction: <what would need to change, without writing the fix>

Ruled out:
- <hypothesis> — <evidence that excludes it>

Temporary diagnostics added/removed: <none | list>
Open questions for the owner: <none | list>
```

Stop after the report. Implementation happens only in a separate, explicitly approved task.
