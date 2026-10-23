---
uid: install-assistant
---

# Install Assistant and start a conversation

Install Assistant to use it in the Unity Editor.

## Prerequisites

Before you install Assistant:

- Use Unity 6000.0.76f1, Unity 6.3 (6000.3), or later.
- Accept the terms and conditions of the **AI** menu in the Unity Editor.

## Install Assistant using the **AI** menu (recommended)

If the **AI** menu is available in the Unity Editor toolbar, use it to install Assistant.

1. In the Unity Editor toolbar, select **AI**.
2. Review and accept the terms and conditions.
3. Select **Agree and install Unity AI**.

Unity installs the required packages, including Assistant.

## Install the package manually

If the **AI** menu isn’t available in the Unity Editor toolbar, you can install Assistant through the **Package Manager**:

1. In the main menu, go to **Window** > **Package Manager**.
2. In the **Package Manager** window, select **+** > **Install package by technical name**.
3. Enter `com.unity.ai.assistant`.
4. Select **Install**.

After installation, **Assistant** appears under **In Project** > **Packages - Unity**.

## Start a conversation

Assistant works in a conversation-based interface. You can ask questions, request suggestions, or instruct Assistant to perform actions in your project.

To begin using Assistant:

1. To launch Assistant, in the main menu, select **Window** > **AI** > **Assistant**. You can dock the **Assistant** window in your layout to keep it accessible during your workflow.
2. In the **Assistant** window, select a mode based on how you want Assistant to interact with your project:
   - **Ask** mode provides guidance and explanations.
   - **Plan** mode generates a step-by-step plan for complex tasks.
   - **Agent** mode performs actions in your project with your approval.

   For more information about modes, refer to [Assistant modes](xref:assistant-modes).
3. For each task, you also choose a model tier based on your needs for response quality and latency:
   - **Unity Default** is the standard tier.
   - **Unity Lite** is a low-latency option for everyday tasks.
   - **Unity Ultra** is a higher-capability option for complex tasks that require deeper reasoning.

   For more information about model tiers, refer to [Model tiers and capabilities](xref:assistant-modes#model-tiers-and-capabilities).
4. (Optional) Attach relevant project data using the **+** button.
5. Enter your question or instruction in the text field.
6. Submit the prompt.

   Assistant displays responses in the conversation area.

## Additional resources

* [Work with Assistant](xref:get-started)
* [Best practices for using Assistant](xref:assistant-best)
