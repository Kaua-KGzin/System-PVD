# Design Notes

A **Design Note** records a decision that affects this codebase — the *why* and *what we gave up*. Code shows what we did; git shows when. Neither carries the alternatives we rejected or the constraint that forced our hand. That is what lives here.

## Layout

Every note's path encodes two things — `{lifecycle}/{class}/yyyy-mm-dd-topic-title.md`:

**Lifecycle** is the status, and a note moves folders as that changes:

- `proposed/` — reviewed before building; not built yet.
- `implemented/` — shipped. Kept factually current: when a later change renames a file or flips a default the note names, fix those facts in the same change. Never edit the *decision* itself.
- `rejected/` — considered and declined. Keep only while the rationale prevents a tempting mistake; otherwise delete it.

**Class** is the kind of decision:

| Class | What it covers |
|---|---|
| `feature` | A new user-facing capability. |
| `bug-fix` | Corrects a defect, or closes a gap found after the fact. |
| `simplification` | Removes code or surface area without adding capability. |
| `architecture` | A structural decision about shipped source — layering, contracts between projects. |
| `process` | Tooling, workflow, gates, deployment — around the code, not in it. |
| `testing` | Test strategy and infrastructure. |

The date in the filename is when the topic was **first proposed**, not when it shipped. Cross-reference other notes with relative markdown links so they survive moves between folders.

There is no index file. Browse the tree or grep it.

## When to write one

Write or update a note when a change alters behavior, a contract shared across projects, the database schema, a security property, process or tooling, or anything else a maintainer may reasonably revisit and ask "why is it like this?".

Updating the note that already owns the decision satisfies this — do not create a duplicate. A purely local edit with no change to behavior or rationale needs nothing.

A note is never edited into a *different* decision. Supersede it with a new note and cross-link both.

## The file format

The first three lines are exactly:

```markdown
# Design Note: <title>

Status: <status>
```

`Status:` is one of `proposed`, `implemented`, or `rejected — <why, in one line>`. No dates, no parentheticals — the filename holds the date and git holds the rest. The rejection reason is the one status that carries content, because the verdict is what readers come for.

The body opens with `## Problem` — the motivation, written so it stands on its own without the solution. Then:

- **`proposed/`** — `## Problem`, `## Proposal`, `## Alternatives`, `## Risks`.
- **`implemented/`** — `## Problem`, `## Decision`, `## Alternatives rejected`, `## Consequences`.
- **`rejected/`** — `## Problem`, `## What was proposed`, `## Why it was declined`.

Bespoke technical sections (schemas, wire formats, topology) go free-form between the required ones.

Write the alternatives honestly. A note that lists only the option we chose has recorded nothing — the reader already knows what we chose, they are here to find out what else was on the table.

---

This convention is adapted, and deliberately simplified, from the Agent Notes convention in the
MIT-licensed [deepseek-harness](https://github.com/deepseek-ai/deepseek-harness).
Dropped as overhead for a project this size: bilingual note pairs, sidecar consistency hashes,
the frozen `archived/` tree, and the CI format-verification gate.
