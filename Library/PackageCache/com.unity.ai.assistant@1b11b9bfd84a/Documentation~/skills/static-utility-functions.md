---
uid: static-utility-functions
---

# Use static utility functions in skills

Use static utility functions to provide Assistant with precise and reusable access to Unity Editor capabilities.

Static utility functions are public static C# methods that Assistant calls through generated C# code. They perform one specific operation with a defined set of parameters and return structured data that Assistant can use in later steps.

Static utility functions provide the following benefits:

- Expose structured operations.
- Integrate the existing APIs into skill workflows.
- Provide a stable interface for fragile operations.
- Wrap your own utility classes, `AssetDatabase` operations, third-party package APIs, or other Editor-accessible code.
- Query domain data for Assistant to reason about.
- Perform actions directly in the Unity Editor.

Unlike [custom tools](xref:custom-tools), these functions require no registration or attributes. Assistant writes the method call from the skill instructions, so you must document the parameter names, types, and return values clearly.

This topic explains [how static utility functions work](#how-static-utility-functions-work-in-skills), how to design their inputs and outputs for skills, how to [reference Unity objects by instance ID](#reference-unity-objects-by-instance-id), and how to document and [call the functions from a skill](#call-static-utility-functions-from-a-skill).

## How static utility functions work in skills

A static utility function is a plain public static C# method in an Editor assembly. Skills instruct Assistant to run a C# script that calls the method by its fully qualified name.

Assistant can infer parameter names, types, and return values through reflection. If the API has more complex use cases or Assistant has trouble calling the methods correctly, provide a markdown API reference file under `resources/` that documents fully qualified method names, parameters, return types, and examples for specific parameterization.

This makes static utility functions a good fit for operations that need precise, repeatable behavior.

## Common use cases for static utility functions

Static utility functions are useful when a skill needs a reliable way to perform a project-specific operation. Common examples include functions that:

- Search and return custom asset metadata so that Assistant can reason about structured domain data.
- Call a validation API for a scene or prefab and return structured results that Assistant can report on.
- Construct and save a project-specific prefab or ScriptableObject.

These functions often wrap existing tooling rather than introducing new systems.

## Write utility classes for skill use

A utility class is a static C# class in an Editor assembly.

Methods return a typed output struct or class that Assistant can read and chain into subsequent calls. Include a `Message` field for confirmation or error reporting, and named ID fields that Assistant can pass to later steps.

The following example shows a utility class with a method that places a hat prefab on a target GameObject. The method returns a structured output with a success flag, message, and instance ID of the created GameObject.

```csharp
public static PlaceHatOutput PlaceHatOnTarget(int hatAssetInstanceId, int targetInstanceId, float yOffset = -0.1f)
{
    var prefab = EditorUtility.EntityIdToObject(hatAssetInstanceId) as GameObject;
    if (prefab == null)
        return new PlaceHatOutput { Message = $"No prefab found for instance ID {hatAssetInstanceId}." };

    var target = EditorUtility.EntityIdToObject(targetInstanceId) as GameObject;
    if (target == null)
        return new PlaceHatOutput { Message = $"No GameObject found for instance ID {targetInstanceId}." };

    // ... position and instantiate ...
    return new PlaceHatOutput { Success = true, Message = $"Hat placed on '{target.name}'.", GameObjectInstanceId = hat.GetInstanceID(), PrefabPath = AssetDatabase.GetAssetPath(prefab) };
}

[Serializable]
public class PlaceHatOutput
{
    public bool Success;
    public string Message;
    public int GameObjectInstanceId;    // instance ID of the placed hat; pass to FitHatCollider
    public string PrefabPath;           // asset path, for confirmation in Assistant's response
}
```
The following table lists the important design elements from the preceding example.

| Element | Purpose |
| ------- | ------- |
| Fully qualified class path | Required in skill instructions so that Assistant can locate the method. For example, `Acme.Editor.HatUtils.PlaceHatOnTarget(...)`. |
| Parameter names and types | Used by Assistant to construct the method call. Clear names help Assistant choose the correct values. |
| Return struct with named fields | Lets Assistant read values by name and chain subsequent steps. |
| Success field | Gives Assistant a consistent way to detect success or failure. |
| Message field | Gives Assistant a consistent description of the outcome or error. |

### Choose query or action methods

Static utility functions can support two main patterns:

- Query methods: Search or read the Unity Editor state and return structured data for Assistant to reason about.
- Action methods: Create, modify, or delete objects or files. Register Unity Editor changes with `Undo` so the actions remain reversible.

The following example shows an action method that fits a collider with a conditional path.

```csharp
public static FitColliderOutput FitHatCollider(int hatInstanceId, int? colliderInstanceId = null)
{
    var hat = EditorUtility.EntityIdToObject(hatInstanceId) as GameObject;
    if (hat == null)
        return new FitColliderOutput { Message = $"No GameObject found for instance ID {hatInstanceId}." };

    CapsuleCollider capsule;
    if (colliderInstanceId.HasValue)
    {
        var existingCollider = EditorUtility.EntityIdToObject(colliderInstanceId.Value) as Collider;
        if (existingCollider == null)
            return new FitColliderOutput { Message = $"No Collider found for instance ID {colliderInstanceId.Value}. Provide a valid ID, or omit to add a new CapsuleCollider." };
        capsule = existingCollider as CapsuleCollider;
        if (capsule == null)
            return new FitColliderOutput { Message = "Existing collider is not a CapsuleCollider; cannot resize." };
        Undo.RecordObject(capsule, "Fit Hat Collider");
    }
    else
    {
        capsule = Undo.AddComponent<CapsuleCollider>(hat);
    }

    // ... fit capsule to mesh bounds ...
    return new FitColliderOutput { Success = true, Message = $"CapsuleCollider fitted (height: {capsule.height:F3}, radius: {capsule.radius:F3}).", ColliderInstanceId = capsule.GetInstanceID() };
}

[Serializable]
public class FitColliderOutput
{
    public bool Success;
    public string Message;
    public int ColliderInstanceId;      // instance ID of the fitted or newly added collider
}
```

Use `Undo.RecordObject` before you modify an existing object and `Undo.AddComponent` or `Undo.RegisterCreatedObjectUndo` when you create new ones.

## Reference Unity objects by instance ID

Static utility functions can't accept `GameObject`, `Component`, or `UnityEngine.Object` parameters directly as literal values in the generated C# code. Instead, Assistant passes Unity objects by instance ID, which is a plain `int` assigned to each in-memory object.

Assistant gets these IDs from previous query calls or from selection context and then passes them into later function calls.

### ID parameter types

The following table lists the common ID parameter types for static utility functions that reference Unity objects.

| Type | Naming convention | Identifies |
| ---- | ----------------- | ---------- |
| int | `gameObjectInstanceId` | A specific GameObject in the scene. |
| int | `componentInstanceId` | A specific Component on a GameObject. |
| int | `assetInstanceId` | A project asset such as a prefab, texture, or material. |
| int? | any of the above naming conventions | An optional object ID. |
| int[] | `gameObjectInstanceIds` | Multiple objects for batch operations. |

### Common parameter and output value types

The following table lists the common parameter and output types.

| Type | Example uses |
| ---- | ------------ |
| `int` | Counts, indices, or pagination offsets (`startIndex`) for methods that return large result sets bit by bit. |
| `string` | Search queries, asset paths, object names, or file paths. |
| `float` | Numeric thresholds or offsets. For example, `yOffset` or `targetFrameTime`. |
| `bool` | Flags and toggles. For example, `includeInactive` or `recursive`. |
| `enum` | Named modes or states. |

Returned serializable structs and classes (like in these examples) with public fields using the listed types are also valid return types. Their fields must use clear human-readable names so that Assistant can identify them clearly and pass them to subsequent calls.

## Call static utility functions from a skill

The skill instructs Assistant to call the method by its fully qualified name. A `resources/` API reference file supplies the parameter and return-type details that Assistant needs to construct the call correctly.

### API reference file

Create a markdown file under `resources/` that documents each method: signature, parameter table, and return struct fields. Assistant loads it on demand when the skill instructions direct it there.

**Example skill structure**

```
skills/create-red-tophat/
├─ SKILL.md
└─ resources/
   └─ hatutils-api-reference.md    ← full signatures, parameter tables, output struct fields
```

**Skill instructions**

References the API file near the top of the skill body, then specifies the fully qualified method call in each step where Assistant might use it.

```markdown

For the full C# API used in this skill, see `resources/hatutils-api-reference.md`.

## 1. Identify Target Object

If the user names a target, execute a C# script calling
`Acme.Editor.HatUtils.FindHatPlacements("<name>")` and use the first
matching result's `GameObjectInstanceId`.

## 2. Place Hat

Execute a C# script calling `Acme.Editor.HatUtils.PlaceHatOnTarget(hatAssetInstanceId, targetInstanceId)`.
Note the returned `GameObjectInstanceId` of the placed hat.
```

In this pattern, the skill directs Assistant to call specific static utility functions instead of describing the outcome in general terms. Assistant reads the instruction, consults the API reference for parameter details, writes the corresponding C# call, runs it, and then reasons about the returned values.

## Additional resources

- [About skills](xref:skills-overview)
- [Decide whether to create a skill](xref:skills-evaluate)
- [Create custom tools](xref:custom-tools)