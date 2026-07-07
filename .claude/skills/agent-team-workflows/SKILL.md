---
name: agent-team-workflows
description: How to run work on this repo as an orchestrated multi-agent workflow in Claude Code — roles (scout, implementer, code-reviewer, detection-calibrator, release-validator), when to spawn subagents vs stay in the main thread, mandatory review gates, and escalation rules. Use this at the START of any non-trivial task (features, bug fixes beyond one line, calibration, refactors, releases), whenever deciding how to structure a session, and whenever a smaller model (Sonnet/Haiku class) is doing the work and needs guardrails. Correctness outranks token cost on this project.
---

# Agent Team Workflows

This project is maintained across sessions by engineers and models of varying
strength. The workflow below is how a cheap session produces senior-quality output:
**separate the roles, force the gates, keep evidence.** Token cost is explicitly not
a constraint here; skipped reviews are.

Ready-made subagent definitions live in `.claude/agents/` (scout, implementer,
code-reviewer, detection-calibrator, release-validator). Invoke them with the Agent
tool or by @-mentioning; they do not inherit this conversation's context, so their
prompts must carry file paths and the specific question.

## The standard flow (feature or non-trivial fix)

```
1 SCOUT      → what exists? (read-only recon; returns a brief, not prose-dumps)
2 PLAN       → main thread: small steps, each with its validation named up front
3 IMPLEMENT  → smallest coherent diffs; one concern per commit
4 SELF-CHECK → clean-tree build + relevant corpus + invariants glance
5 REVIEW     → code-reviewer subagent with the rubric (references/review-rubric.md)
6 VALIDATE   → corpus runs / manual-matrix rows per testing-validation
7 CLOSE      → release-git-hygiene checklist; docs updated or deliberately withheld
```

Rules that make this work:

- **Scout before touching code** when the task involves any area you haven't read
  this session. The scout returns: relevant files + the 5–10 lines that matter +
  which skills apply + which invariants are nearby. This is cheaper than the main
  thread paging through the repo, and it keeps your context for the actual work.
- **The implementer and the reviewer are never the same context.** Self-review
  catches typos; it does not catch wrong mental models. Spawn `code-reviewer` fresh —
  its ignorance of your reasoning is the feature.
- **Review is a gate, not a suggestion.** A finding of severity "blocker" (rubric)
  means fix-and-re-review, not "noted".
- **Detection changes get the specialist.** Any change to detector constants or
  geometry goes through `detection-calibrator` (it enforces the one-parameter rule
  and demands corpus evidence). This is non-negotiable; it is the area with the most
  expensive regressions and the least obvious ones.
- **Releases get `release-validator`** — it walks the hygiene checklist and the SSRD
  parity build as a hostile auditor.

## Sizing guide

| Task | Workflow |
|---|---|
| Typo / comment / doc-only | main thread; build; commit (status-checked) |
| One-function bug fix | main thread implement → code-reviewer → affected matrix rows |
| Detector/cleaner tuning | scout → calibrator loop (owns steps 3–6) → reviewer on the final diff |
| Feature (new UI, new pipeline stage) | full standard flow; consider splitting implement across sequential subagents per component boundary (detection / pipeline / UI) |
| Refactor | scout (map blast radius) → implement → reviewer with extra attention to invariants 3–5 → full matrix |
| Release | full standard flow ending in release-validator + ssrd-build-publish |

## Guardrails for smaller models (Sonnet/Haiku-class sessions)

- **Skills first, repo second.** Read the owning SKILL.md before opening source; it
  encodes the why that the source doesn't. The debugging-runbook routes symptoms.
- Don't "improve" anything on the invariants list
  (testing-validation/references/invariants.md) as a side effect. If a diff touches
  one, say so explicitly and justify it, or back it out.
- **Uncertainty protocol**: state what you verified vs assumed. Numbers about the
  local v0.3.0 files (ConversationDetector constants, FixConfusableChars) come from
  session history — diff against the actual file before depending on them.
- **Escalate to the human** rather than deciding alone: anything ToS-adjacent, any
  data-migration of catalog.json, deleting user data, force-push, changing the
  manifest namespace/version scheme, or publishing.
- Evidence over adjectives, always: name the failing words, paste the rect, link the
  corpus run. "Looks good" is not a review; "tested, works" is not a test report.

## Working agreements

- Commit messages state old→new for constants, with the evidence that motivated it.
- Every session that discovers a new failure mode leaves the repo smarter: corpus
  case added, runbook row added, or skill updated — pick at least one.
- When a skill and the code disagree, the code is the truth **and the skill gets
  fixed in the same session** — a stale skill is worse than none.
