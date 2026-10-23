---
uid: figma-ui-reference
---

# UI Authoring settings reference

Use the **UI Authoring** page in **Project Settings** to provide and verify the Figma personal access token that Assistant uses to import Figma design data from Figma projects.

After you save and verify a valid Figma access token, Assistant can access Figma project links that you provide in the **Assistant** window and generate Unity UI assets from those designs.

For instructions on how to connect Assistant to Figma and generate UI from a Figma design, refer to [Create UI from a Figma design](xref:figma-ui).

To access these settings, in the Unity Editor, select **Edit** > **Project Settings** > **AI** > **UI**.

The **UI Authoring** page shows the following settings:

| **Setting** | **Description** |
| ----------- | --------------- |
| **Figma Access Token** | Stores the Figma personal access token that Assistant uses to connect to your Figma projects. Enter a valid token to allow Assistant to import design assets and screen data from Figma project links. |
| **Verify & Save** | Verifies the Figma access token and saves it to the current project settings. After verification succeeds, Assistant can use the token to access Figma project data. |

## Additional resources

- [Create UI from a Figma design](xref:figma-ui)
- [Work with assets in Unity Editor](xref:assets-landing)