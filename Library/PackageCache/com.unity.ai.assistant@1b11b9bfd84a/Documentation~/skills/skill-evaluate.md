---
uid: skills-evaluate
---

# Decide whether to create a skill

Evaluate whether a new skill improves Assistant outcomes for a specific workflow.

Before you create a new skill, validate that it improves Assistant's result for the domain and scenario you want to support. A skill is most useful when it makes a repeatable workflow work better or more consistently instead of restating behavior that already works well without extra instructions.

To measure the impact of a skill, compare repeated runs of the same prompt in the same project state with and without the skill. This determines whether the added instructions produce a measurable improvement in Assistant's responses or actions.

If the workflow depends on project-specific APIs or fragile multi-step operations, you might also consider whether the skill should reference supporting files or use static utility functions.

## Prerequisites

Before you start, make sure you:

1. Install and set up [Assistant](xref:install-assistant).
2. Identify a specific workflow or domain scenario you want to improve.

## Compare results with and without the skill

To evaluate whether you need a skill:

1. Choose a specific domain and scenario you want to improve.
2. Run the same prompt in the same project state at least three to four times without the skill.
3. Run the same prompt in the same project state at least three to four times with the skill, or with the proposed additions to the skill.
4. Compare the outcomes from both sets of runs.
5. Confirm whether the skill produces an effective improvement.

A useful comparison focuses on whether the skill improves the quality or consistency of the outcome for the target workflow.

After you decide to create a skill, the next step is to add the skill files in one of the scanned locations. For more information, refer to [Create skills from the filesystem](xref:skills-filesystem).

## Additional resources

- [About skills](xref:skills-overview)
- [Use static utility functions in skills](xref:static-utility-functions)