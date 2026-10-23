---
uid: ai-installation-troubleshooting
---

# Troubleshooting Assistant installation and upgrade errors

Solve an issue with script compilation errors that occur after you upgrade the Unity Editor to version 6000.3.10f1 or later on a project where Assistant was installed on an earlier Editor version.

Before Unity Editor version 6000.3.10f1, installing Assistant through the **AI** button also installed the Generators packages. From version 6000.3.10f1 onward, the **AI** button installs only Assistant. If you installed Assistant on an Editor version earlier than 6000.3.10f1 and then upgrade to 6000.3.10f1 or later, the project keeps references to the Generators packages, which causes compilation errors.

## Symptoms

After you upgrade the Unity Editor to version 6000.3.10f1 or later on a project that already has Assistant installed, you might encounter script compilation errors in the **Console** window.

## Cause

Unity Editor versions earlier than 6000.3.10f1 install the `com.unity.ai.generators` and `com.unity.2d.enhancers` packages alongside Assistant when you select the **AI** button. From version 6000.3.10f1 onward, only Assistant installs. When you upgrade an existing project past this version, the project manifest still references the Generators packages. These packages are no longer compatible with the new Assistant installation, which causes the compilation errors.

## Resolution

To resolve the compilation errors, follow these steps:

1. Open `Packages/manifest.json` in your project.
1. Remove the `com.unity.ai.generators` and `com.unity.2d.enhancers` entries from the manifest, then save the file.
1. Close the Unity Editor.
1. Delete the `Library` folder from your project.
1. Reopen the project in the Unity Editor.

   Unity regenerates the `Library` folder and recompiles your scripts.

## Additional resources

- [Troubleshooting issues with Assistant](xref:troubleshoot-landing)
- [Manage Assistant](xref:manage-assistant)
