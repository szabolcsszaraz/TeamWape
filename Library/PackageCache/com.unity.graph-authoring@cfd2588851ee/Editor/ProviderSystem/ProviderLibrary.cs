using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.GraphAuthoring.Editor.ProviderSystem
{
    internal class ProviderLibrary
    {
        internal static event Action<IReadOnlyList<GUID>> OnProvidersReloaded;

        internal static void NotifyProvidersReloaded(IReadOnlyList<GUID> modifiedAssetIDs)
            => OnProvidersReloaded?.Invoke(modifiedAssetIDs);

        static ProviderLibrary s_instance;

        internal static bool IsInitialized => s_instance != null;

        internal static bool TryGetInstance(out ProviderLibrary instance)
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
            {
                instance = null;
                return false;
            }
            if (s_instance is null)
            {
                s_instance = new ProviderLibrary();
                ProviderTypeCache.PopulateLibrary(s_instance);
                ShaderReflectionAssetPostProcessor.PopulateFromFiles(s_instance);
            }
            instance = s_instance;
            return true;
        }

        // Note: The provider key + version concept is being used heavily here, but is never exposed to the public.
        Dictionary<(string key, string version), IProvider> m_providers = new();
        Dictionary<(string key, string version), List<IProvider>> m_conflictedProviders = new();        
        Dictionary<string, SortedSet<string>> m_providersByVersion = new();
        Dictionary<GUID, HashSet<(string key, string version)>> m_providersByAsset = new();
        Dictionary<string, SortedSet<(int priority, (string key, string version) vk)>> m_providersByGroup = new();

        IComparer<string> m_versionComparer = null;
        IComparer<(int priority, (string key, string version) vk)> m_priorityComparer = null;

        static int CompareVersion(string a, string b)
        {
            if (a == Hints.Version.kUnversioned && b == Hints.Version.kUnversioned) return 0;
            if (a == Hints.Version.kUnversioned) return 1;
            if (b == Hints.Version.kUnversioned) return -1;

            bool ap = Version.TryParse(a, out var pa);
            bool bp = Version.TryParse(b, out var pb);
            return -((ap && bp) ? pa.CompareTo(pb) : string.Compare(a, b, StringComparison.Ordinal));
        }

        static int ComparePriority((int priority, (string key, string version) vk) a, (int priority, (string key, string version) vk) b)
        {
            var cmpPriority = -a.priority.CompareTo(b.priority);
            return cmpPriority != 0 ? cmpPriority : a.vk.key.CompareTo(b.vk.key);
        }

        internal IEnumerable<IProvider<T>> MainProviders<T>() where T : IDefinition
        {
            foreach (var group in m_providersByGroup.Values)
            {
                foreach (var gke in group)
                {
                    if (m_providers[gke.vk] is IProvider<T> typedProvider)
                    {
                        yield return typedProvider;
                        break;
                    }
                }
            }

            // Only emit the latest version iff it is ungrouped. We don't fall back to earlier ungrouped versions.
            foreach (var pkv in m_providersByVersion)
            {
                string version = null;
                foreach (var v in pkv.Value) { version = v; break; } // .First()
                var provider = m_providers[(pkv.Key, version)];
                if (provider is IProvider<T> typedProvider && provider.Group == null)
                    yield return typedProvider;
            }
        }

        internal IEnumerable<IProvider<T>> ProvidersByGroup<T>(string groupKey) where T : IDefinition
        {
            if (m_providersByGroup.TryGetValue(groupKey, out var group))
            {
                foreach (var gke in group)
                {
                    var provider = m_providers[gke.vk];
                    if (provider is IProvider<T> typedProvider)
                        yield return typedProvider;
                }
            }
        }

        internal IEnumerable<IProvider<T>> ProvidersByVersion<T>(string key) where T : IDefinition
        {
            if (m_providersByVersion.TryGetValue(key, out var entries))
                foreach (var version in entries)
                {
                    var provider = m_providers[(key, version)];
                    if (provider is IProvider<T> typedProvider)
                        yield return typedProvider;
                }
        }

        internal void ClearByAssetID(GUID assetID)
        {
            if (m_providersByAsset.Remove(assetID, out var affected))
                foreach (var vk in affected)
                    RemoveProvider(m_providers[vk]);

            List<IProvider> conflictsToRemove = new();
            foreach (var conflicts in m_conflictedProviders.Values)
                foreach (var conflict in conflicts)
                    if (conflict.AssetID == assetID)
                        conflictsToRemove.Add(conflict);

            foreach (var conflict in conflictsToRemove)
                RemoveProvider(conflict);
        }

        internal IEnumerable<GUID> GetTrackedAssetIDs() => m_providersByAsset.Keys;

        internal bool TryAdd(IProvider provider) // TODO(SVFXG-918): Improve feedback and logging for conflicts.
        {
            // provider is malformed.
            if (provider == null || string.IsNullOrEmpty(provider.ProviderKey) || string.IsNullOrEmpty(provider.Version))
            {
                return false;
            }

            var vk = (provider.ProviderKey, provider.Version);

            // Key is already conflicted, so we can just add this to the conflicted set.
            if (m_conflictedProviders.TryGetValue(vk, out var byConflict))
            {
                byConflict.Add(provider);
                return false;
            }

            // An unconflicted key already exists, so now this new one and the original are in conflict.
            if (!m_providers.TryAdd(vk, provider))
            {
                m_conflictedProviders.TryAdd(vk, new());
                m_conflictedProviders[vk].Add(provider);
                m_conflictedProviders[vk].Add(m_providers[vk]);
                RemoveProvider(m_providers[vk]);
                return false;
            }

            // Looks good, update bookkeeping.

            // Lookup by Version.
            m_providersByVersion.TryAdd(provider.ProviderKey, new(m_versionComparer ??= Comparer<string>.Create(CompareVersion)));
            m_providersByVersion[provider.ProviderKey].Add(provider.Version);

            // Lookup by Asset ID.
            if (provider.AssetID != default)
            {
                m_providersByAsset.TryAdd(provider.AssetID, new());
                m_providersByAsset[provider.AssetID].Add(vk);
            }

            // Lookup by Group Key.
            if (provider.Group != null)
            {
                m_providersByGroup.TryAdd(provider.Group, new(m_priorityComparer ??= Comparer<(int, (string, string))>.Create(ComparePriority)));
                var group = m_providersByGroup[provider.Group];

                // We only want the latest version of a provider in a group, so we need to check if one is currently in the group first.
                var currentFound = false;
                var currentVersion = Hints.Version.kUnversioned;
                var currentPriority = 0;

                foreach (var gke in group)
                    if (gke.vk.key == provider.ProviderKey)
                    {
                        currentFound = true;
                        currentVersion = gke.vk.version;
                        currentPriority = gke.priority;
                        break;
                    }

                if (!currentFound)
                {
                    group.Add((provider.GroupPriority, vk));
                }
                else if (CompareVersion(provider.Version, currentVersion) > 0)
                {
                    group.Remove((currentPriority, (provider.ProviderKey, currentVersion)));
                    group.Add((provider.GroupPriority, vk));
                }
                // else: the version we're adding is actually older than current, so we should do nothing.
            }

            return true;
        }

        internal bool TryGet<T>(string key, string version, out IProvider<T> provider) where T : IDefinition
        {
            provider = null;
            if (m_providers.TryGetValue((key, version), out var untypedProvider))
            {
                if (untypedProvider is IProvider<T> typedProvider)
                {
                    provider = typedProvider;
                    return true;
                }
            }
            return false;
        }

        internal bool TryGet<T>(string key, out IProvider<T> provider) where T : IDefinition
        {
            provider = null;
            if (m_providersByVersion.TryGetValue(key, out var byVersion))
            {
                foreach(var version in byVersion)
                {
                    if (m_providers[(key, version)] is IProvider<T> typedProvider)
                    {
                        provider = typedProvider;
                        return true;
                    }
                }
            }
            return false;
        }

        void RemoveProvider(IProvider provider)
        {
            var vk = (provider.ProviderKey, provider.Version);
            if (m_providers.Remove(vk))
            {
                if (m_providersByAsset.TryGetValue(provider.AssetID, out var byAsset))
                {
                    byAsset.Remove(vk);
                    if (byAsset.Count == 0)
                        m_providersByAsset.Remove(provider.AssetID);
                }

                if (m_providersByVersion.TryGetValue(provider.ProviderKey, out var byVersion))
                {
                    byVersion.Remove(provider.Version);
                    if (byVersion.Count == 0)
                        m_providersByVersion.Remove(provider.ProviderKey);
                }

                if (provider.Group != null && m_providersByGroup.TryGetValue(provider.Group, out var byGroup))
                {
                    byGroup.Remove((provider.GroupPriority, vk));

                    if (byVersion.Count > 0)
                    {
                        foreach(var lkg in byVersion)
                        {
                            if (m_providers.TryGetValue((provider.ProviderKey, lkg), out var nextBestProvider))
                            {
                                if (nextBestProvider.Group == provider.Group)
                                {
                                    byGroup.Add((nextBestProvider.GroupPriority, (nextBestProvider.ProviderKey, nextBestProvider.Version)));
                                    break;
                                }
                            }
                        }
                    }

                    if (byGroup.Count == 0)
                    {
                        m_providersByGroup.Remove(provider.Group);
                    }
                }
            }
            else if (m_conflictedProviders.ContainsKey(vk))
            {
                // If we remove a conflicted provider, find its index.
                for(int i = 0; i < m_conflictedProviders[vk].Count; ++i)
                {
                    var conflict = m_conflictedProviders[vk][i];
                    if (conflict == provider || conflict.AssetID == provider.AssetID)
                    {
                        // remove it.
                        m_conflictedProviders[vk].RemoveAt(i);

                        // if there's only one left it is now uncontested and can be added to the library.
                        if (m_conflictedProviders[vk].Count == 1)
                        {
                            var unconflicted = m_conflictedProviders[vk][0];
                            m_conflictedProviders.Remove(vk);
                            TryAdd(unconflicted);
                            // Conflict Resolved.
                        }
                        // Conflict still remains.
                        break;
                    }
                }
            }
        }
    }
}
