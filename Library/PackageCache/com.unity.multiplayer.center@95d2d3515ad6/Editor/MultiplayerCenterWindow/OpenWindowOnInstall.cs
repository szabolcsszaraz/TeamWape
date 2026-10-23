using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Unity.Multiplayer.Center.Editor
{
    // registeredPackages is raised after the domain reload, so this assembly receives its own install event.
    [InitializeOnLoad]
    static class OpenWindowOnInstall
    {
        static OpenWindowOnInstall()
        {
            if (Application.isBatchMode || IsMultiplayerPlayModeClone())
                return;

            Events.registeredPackages += OnPackagesRegistered;
        }

        static void OnPackagesRegistered(PackageRegistrationEventArgs args)
        {
            Events.registeredPackages -= OnPackagesRegistered;

            foreach (var package in args.added)
            {
                if (package.name == k_PackageName && package.isDirectDependency)
                {
                    EditorApplication.delayCall += MultiplayerCenterWindow.OpenWindow;
                    return;
                }
            }
        }

        static bool IsMultiplayerPlayModeClone()
            => Array.IndexOf(Environment.GetCommandLineArgs(), k_CloneCommandLineArgument) >= 0;

        const string k_PackageName = "com.unity.multiplayer.center";
        const string k_CloneCommandLineArgument = "--virtual-project-clone";
    }
}
