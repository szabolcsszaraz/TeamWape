using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Unity.AI.Mesh.Windows
{
    static class GlTFastInstaller
    {
        const string k_GlTFastPackage = "com.unity.cloud.gltfast";

        static ListRequest s_ListRequest;
        static AddRequest s_AddRequest;
        static bool s_IsInstalled;
        static bool s_HasListed;
        static bool s_IsInstalling;
        static bool s_InstallPending;

        public static bool IsInstalled => s_IsInstalled;
        public static bool IsInstalling => s_IsInstalling;

        public static event Action OnStateChanged;

        static GlTFastInstaller()
        {
            // Kick off an initial detection list so callers can query IsInstalled.
            RefreshInstallState();
        }

        public static void RefreshInstallState()
        {
            if (Application.isBatchMode)
                return;

            if (s_ListRequest != null && !s_ListRequest.IsCompleted)
                return;

            s_ListRequest = Client.List(offlineMode: true, includeIndirectDependencies: true);
            EditorApplication.update += ListProgress;
        }

        public static void InstallGlTFastIfNeeded()
        {
            if (Application.isBatchMode)
                return;

            if (s_AddRequest != null && !s_AddRequest.IsCompleted)
                return;

            if (s_ListRequest != null && !s_ListRequest.IsCompleted)
            {
                SetInstalling(true);
                s_InstallPending = true;
                return;
            }

            SetInstalling(true);
            s_ListRequest = Client.List(offlineMode: true, includeIndirectDependencies: true);
            EditorApplication.update += ListProgressAndInstall;
        }

        static void ListProgress()
        {
            if (!s_ListRequest.IsCompleted)
                return;

            EditorApplication.update -= ListProgress;
            UpdateInstallStateFromListResult();

            if (s_InstallPending)
            {
                s_InstallPending = false;
                s_ListRequest = Client.List(offlineMode: true, includeIndirectDependencies: true);
                EditorApplication.update += ListProgressAndInstall;
            }
        }

        static void ListProgressAndInstall()
        {
            if (!s_ListRequest.IsCompleted)
                return;

            EditorApplication.update -= ListProgressAndInstall;
            UpdateInstallStateFromListResult();

            if (s_IsInstalled)
            {
                SetInstalling(false);
                return;
            }

            s_AddRequest = Client.Add(k_GlTFastPackage);
            EditorApplication.update += AddProgress;
        }

        static void AddProgress()
        {
            if (!s_AddRequest.IsCompleted)
                return;

            EditorApplication.update -= AddProgress;

            if (s_AddRequest.Status == StatusCode.Success)
            {
                SetInstalled(true);
            }
            else if (s_AddRequest.Error != null)
            {
                Debug.LogError($"Failed to install {k_GlTFastPackage}: {s_AddRequest.Error.message}");
            }

            SetInstalling(false);
        }

        static void UpdateInstallStateFromListResult()
        {
            if (s_ListRequest.Status != StatusCode.Success)
                return;

            var found = false;
            foreach (var package in s_ListRequest.Result)
            {
                if (package.name == k_GlTFastPackage)
                {
                    found = true;
                    break;
                }
            }

            s_HasListed = true;
            SetInstalled(found);
        }

        static void SetInstalled(bool value)
        {
            if (s_HasListed && s_IsInstalled == value)
                return;

            s_IsInstalled = value;
            try { OnStateChanged?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        static void SetInstalling(bool value)
        {
            if (s_IsInstalling == value)
                return;

            s_IsInstalling = value;
            try { OnStateChanged?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
