---
uid: assistant-plan-mode
---

# Create implementation plans

Use **Plan** mode to generate, review, and approve a structured implementation plan before Assistant makes changes to your project.

**Plan** mode is useful for larger or multi-step tasks when you want to review the proposed implementation before Assistant acts on it. Assistant analyzes your request, asks clarifying questions if needed, generates a plan, and waits for approval before it switches to **Agent** mode to implement it.

Use **Plan** mode for tasks, such as creating gameplay systems, building feature workflows, or setting up multiple connected project elements. For simpler tasks, such as creating a GameObject or changing a setting, use [**Agent mode**](xref:assistant-modes#agent-mode).

## Create a plan

To create and approve a plan:

1. In the **Assistant** window, select **Plan** from the mode selector.
2. Enter a prompt that describes the task you want Assistant to perform.
3. Answer any clarification questions Assistant asks.
4. Review the generated implementation plan.
5. Do one of the following:
   - Select **Approve** to approve the plan.
   - **Deny** the plan to discard it and continue the conversation.
   - Provide feedback to update the plan.

When you approve the plan, Assistant automatically switches to the **Agent** mode and asks whether it should begin implementation. Assistant saves the generated plan in your project's `Assets/Plans` folder as a `.md` file, so you can review or edit it outside the Assistant window if needed.

## Revise a generated plan

After Assistant creates a plan, you can refine it before you approve it.

For example, you can ask Assistant to:

- Rename the plan or project title.
- Change or reorder steps.
- Update part of the proposed implementation.

Assistant updates the plan based on your feedback so you can iterate on it before implementation.

## Implement the approved plan

After you approve the plan:

1. Assistant automatically switches to **Agent** mode and asks whether it can begin implementation.
2. Confirm that Assistant can implement the plan.
3. Monitor the progress checklist as Assistant completes each step.
4. Review the completion summary when implementation finishes.

Assistant performs the approved actions in sequence and provides a summary of the completed work when implementation finishes. The implementation is the same as in the **Agent** mode, but **Plan** mode adds the review, approval, and progress workflow before implementation begins.

## Additional resources

- [Assistant modes and model tiers](xref:assistant-modes)
- [Install and configure Assistant](xref:install-config)