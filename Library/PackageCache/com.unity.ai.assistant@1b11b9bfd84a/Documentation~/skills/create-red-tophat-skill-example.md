---
uid: create-red-tophat-skill-example
---

# Example: Create a skill that generates and places a red top hat

Create a skill that generates a red top hat, places it on a target object, fits a collider, and validates the result with a screenshot.

This example shows how a skill can combine skill instructions, generated assets, static utility function calls, and validation steps into a single workflow. It demonstrates how to confirm a target object first, call project-specific APIs by fully qualified name, and guide Assistant through a repeatable sequence.

The example also shows how to separate workflow logic from API details. The main `SKILL.md` file defines the workflow, while the [HatUtils API reference](xref:hatutils-api-reference) contains the method signatures and output data used by the skill.

> [!NOTE]
> This example is illustrative only. Skill instructions don't need to follow this exact structure or phrasing as long as they clearly describe the intended workflow.

## Prerequisites

Before you start, make sure you:

- Install and set up [Assistant](xref:install-assistant).
- [Create a skill folder](xref:skills-filesystem) in a scanned location.
- Add the supporting [static utility functions](xref:static-utility-functions) in an Editor assembly.

## Create the example skill

To create the example skill:

1. Create the `skills/create-red-tophat/` folder.
2. Add a `SKILL.md` file with the following content:

   ```md
   ---
   name: create-red-tophat
   description: Generates a red top hat 3D asset using AI generation and places it on a target object, matching the hat's XZ bounds to the target's and positioning it at the top. Use when the user asks to create a top hat, place a hat on a character, or dress up an object.
   ---

   For the full C# API used in this skill, see `resources/hatutils-api-reference.md`.

   ## Critical Rules

   - Confirm the target object BEFORE invoking asset generation.
   - NEVER skip Section 3 (Validate).

   ## 1. Identify Target Object

   Determine the target before generating the hat.

   ## Path A: Use Selected Object

   Follow ONLY if the user refers to the current selection and an object is selected.
   1. Note the selected object's instance ID from the selection context.

   ## Path B: Use Named Object

   Follow ONLY if the user specifies an object by name.
   1. Invoke a C# script calling `TestProject.Scripts.HatUtils.FindHatPlacements("<name>")`.
   2. Use the first matching result as the target. Note its `GameObjectInstanceId`.

   ## Path C: No Target Specified

   Follow ONLY if no existing target is specified.
   1. Invoke a C# script calling `TestProject.Scripts.HatUtils.FindHatPlacements()` to list available objects.
   2. Ask the user to pick a target from the results. Note the chosen target's `GameObjectInstanceId`.

   IMPORTANT: Follow exactly one path. NEVER mix steps from different paths.

   ## 2. Create and Place Hat

   After the target is confirmed, perform these steps in order:

   1. Generate: Use the generation model best suited for text-to-3D and prompt it to "Create a sparkling red top hat". Note the generated asset's instance ID.
   2. Place: Invoke a C# script calling `TestProject.Scripts.HatUtils.PlaceHatOnTarget(hatAssetInstanceId, targetInstanceId)` with the asset and target instance IDs. Note the returned `GameObjectInstanceId` of the placed hat.
   3. Fit collider: Invoke a C# script calling `TestProject.Scripts.HatUtils.FitHatCollider(hatInstanceId)` with the hat's `GameObjectInstanceId` from the previous step. This adds a new CapsuleCollider sized to the hat's mesh bounds.

   ## 3. Validate

   Take a screenshot to visually verify the hat is correctly positioned on the target.
   If placement looks wrong, adjust and re-validate.
   Do not validate more than 3 times — ask the user for input instead.

   ## 4. Final Confirmation

   Inform the user:

   "I generated a red top hat." Then state the asset path to the created prefab.
   "I have put the top hat on the target object." Then state its name.

   ## Reminders

   Before reporting completion, verify:
   - The hat is parented to the correct target.
   - A screenshot was captured for validation.
   - If the hat appears off-center or floating, check world-space bounds and pivot offset.
   ```

3. Create the `skills/create-red-tophat/resources/` folder.
4. Create the file `skills/create-red-tophat/resources/hatutils-api-reference.md`.
5. Add the API details described in [HatUtils API reference](xref:hatutils-api-reference).

## Additional resources

- [HatUtils API reference](xref:hatutils-api-reference)
- [Manage Skills page reference](xref:skills-reference)