---
description: Append an entry to docs/ai-log.md (evidence for the AI Collaboration Narrative)
argument-hint: <what happened, in a sentence or two>
allowed-tools: Read, Edit
---

Append a new entry to the end of `docs/ai-log.md` describing: $ARGUMENTS

Use today's date and this exact structure:

```markdown
### YYYY-MM-DD: <short title>
- **Phase:** design | implementation | testing | review | docs
- **I asked the AI to:** ...
- **The AI produced:** ...
- **What I accepted, changed or rejected, and why:** ...
- **How I verified it:** (test name, command run, review step)
- **Lesson:** ...
```

Write in the first person from the developer's point of view. Be factual and specific: name files,
tests and the actual defect or decision. Use only what happened in this session or what the user
stated; if a field is unknown, write "TBD" instead of inventing it. Show the entry to the user after appending.
