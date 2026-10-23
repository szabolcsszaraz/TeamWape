---
uid: hatutils-api-reference
---

# HatUtils API reference

Use this example to learn how to structure a supporting API reference file for a skill.

## Example API reference file

The following example documents the `TestProject.Scripts.HatUtils` methods that a skill uses to find target objects, place a generated hat, and fit a collider. Use this pattern when you document project-specific static utility methods for skills.

An API reference like this makes Assistant more reliable because it gives Assistant clearer information about the available functions, their parameters, their return values, and the output data they provide.

> [!NOTE]
> This reference is an illustrative example of how to document utility methods for use in a skill. Your own API reference files can use a different structure as long as they clearly describe method names, parameters, return values, and output fields.

```
## HatUtils methods

The HatUtils API reference contains the following methods.

### `FindHatPlacements`

This method searches all loaded scenes for GameObjects whose name contains `filter` using a case-insensitive substring match. It returns all the matches with their instance IDs.

```csharp
public static HatPlacementOutput FindHatPlacements(string filter = "")
    ```

`FindHatPlacements` accepts the following parameter.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `filter` | string | `""` | Substring to match against GameObject names. An empty string returns all scene objects. |

This method returns a `HatPlacementOutput` value that contains the matched scene objects and status information.

### `PlaceHatOnTarget`

This method instantiates a hat prefab as a prefab instance, parents it to the target GameObject, and positions it so that the hat's lower mesh bound aligns with the target's upper mesh bound, applying an additional `yOffset`.

```csharp
public static PlaceHatOutput PlaceHatOnTarget(int hatAssetInstanceId, int targetInstanceId, float yOffset = -0.1f)


`PlaceHatOnTarget` accepts the following parameters.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `hatAssetInstanceId` | int | required | Instance ID of the hat prefab asset. |
| `targetInstanceId` | int | required | Instance ID of the target GameObject in the scene. |
| `yOffset` | float | `-0.1f` | Additional vertical offset applied after bounds alignment. Negative values lower the hat. |

This method returns a `PlaceHatOutput` object that contains the instance ID of the placed hat, the source prefab path, and status information.

### `FitHatCollider`

This method fits a CapsuleCollider to the combined renderer bounds of the hat. It converts the world-space bounds into local-space values using `lossyScale`. If `colliderInstanceId` is provided, the method resizes the existing collider. Otherwise, it adds a new CapsuleCollider to the hat GameObject.

```csharp
public static FitColliderOutput FitHatCollider(int hatInstanceId, int? colliderInstanceId = null)

`FitHatCollider` accepts the following parameters.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `hatInstanceId` | int | required | Instance ID of the hat GameObject (returned by `PlaceHatOnTarget`). |
| `colliderInstanceId` | int? | `null` | Instance ID of an existing collider to resize. Must refer to a CapsuleCollider. If omitted, adds a new collider. |

This method returns a `FitColliderOutput` object that contains the fitted collider instance ID and status information.

## HatUtils output types

The HatUtils API reference contains the following output types.

### `HatPlacementOutput`

| Field | Type | Description |
|-------|------|-------------|
| `Success` | bool | Indicates whether the operation completed successfully.<br>`true` if the function completed without error. |
| `Message` | string | Status or warning message (for example, no results found). |
| `Targets` | `List<HatTargetInfo>` | Matched scene objects. Empty if no matches are found. |

### `HatTargetInfo`

| Field | Type | Description |
|-------|------|-------------|
| `Name` | string | Name of the GameObject. |
| `GameObjectInstanceId` | int | Instance ID used as `targetInstanceId` in `PlaceHatOnTarget`. |
| `ColliderInstanceId` | int | Instance ID of the first collider on the object, or `0` if none exists. |

### `PlaceHatOutput`

| Field | Type | Description |
|-------|------|-------------|
| `Success` | bool | Indicates whether the operation completed successfully. <br>`true` if the function completed without error.|
| `Message` | string | Confirmation or error message. |
| `GameObjectInstanceId` | int | Instance ID of the placed hat GameObject. Pass to `FitHatCollider` as `hatInstanceId`. |
| `PrefabPath` | string | Project-relative asset path of the source prefab. |

### `FitColliderOutput`

| Field | Type | Description |
|-------|------|-------------|
| `Success` | bool | Indicates whether the operation completed successfully.<br>`true` if the function completed without error. |
| `Message` | string | Confirmation (with fitted dimensions) or error description. |
| `ColliderInstanceId` | int | Instance ID of the fitted or newly added CapsuleCollider. |
```

## Additional resources

- [Use static utility functions in skills](xref:static-utility-functions)
- [Example: Create a skill that generates and places a red top hat](xref:create-red-tophat-skill-example)