using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Unity.GraphAuthoring.Editor.ProviderSystem;
using Unity.GraphAuthoring.Editor.ProviderSystem.Hints;
using Version = Unity.GraphAuthoring.Editor.ProviderSystem.Hints.Version;

namespace Unity.GraphAuthoring.Editor.Tests
{
    // Shared base for ProviderLibrary-focused fixtures: common GUIDs and the
    // ShaderFunction factories used by the versioned-coexistence and
    // conflict-resolution suites.
    abstract class ProviderLibraryTestFixtureBase
    {
        protected static readonly GUID GuidA = new GUID("aaaaaaaa000000000000000000000000");
        protected static readonly GUID GuidB = new GUID("bbbbbbbb000000000000000000000000");
        protected static readonly GUID GuidC = new GUID("cccccccc000000000000000000000000");

        // ReflectedFunctionProvider reads its ProviderKey from the sg:ProviderKey hint —
        // tests must declare it explicitly or TryAdd's validation will reject the provider.
        protected static ShaderFunction FuncWithVersion(string name, string version) =>
            new ShaderFunction(name, null, null, new ShaderType("void"), "",
                new IStrongHint[] { new ProviderKey($"{name}()"), new Version(version) });

        protected static ShaderFunction FuncNoVersion(string name) =>
            new ShaderFunction(name, null, null, new ShaderType("void"), "",
                new IStrongHint[] { new ProviderKey($"{name}()") });
    }

    // Minimal IDefinition for StubProvider — ProvidersByVersion<T> filters by IProvider<T>,
    // so StubProvider needs to be IProvider<something>; the stub definition satisfies that
    // without bringing in IShaderFunction's machinery.
    class StubDefinition : IDefinition
    {
        public bool IsValid => true;
        public string Name { get; }
        internal StubDefinition(string name) { Name = name; }
    }

    // Minimal IProvider<IDefinition> stub — no asset loading required. The registration tests
    // pass real GUIDs and exercise asset-flow code paths (m_providersByAsset tracking,
    // ClearByAssetID, conflict reporting against asset paths).
    class StubProvider : IProvider<IDefinition>
    {
        public string ProviderKey { get; }
        public GUID AssetID { get; }
        public IDefinition Definition { get; }
        public IProvider Clone() => this;
        internal StubProvider(string key, GUID assetId)
        {
            ProviderKey = key;
            AssetID = assetId;
            Definition = new StubDefinition(key);
        }
    }

    // Test helpers for ProviderLibrary fixtures.
    static class ProviderLibraryTestExtensions
    {
        internal static List<IProvider<T>> ToList<T>(this IEnumerable<IProvider<T>> source)
            where T : IDefinition
        {
            var list = new List<IProvider<T>>();
            foreach (var item in source) list.Add(item);
            return list;
        }
    }

    [TestFixture]
    class ProviderLibraryTests : ProviderLibraryTestFixtureBase
    {
        // ── Registration ────────────────────────────────────────────────────────

        [Test]
        public void TryAdd_NewKey_SucceedsAndIsRetrievable()
        {
            var lib = new ProviderLibrary();
            var p = new StubProvider("FuncA()", GuidA);

            Assert.IsTrue(lib.TryAdd(p));
            Assert.IsTrue(lib.TryGet<IDefinition>("FuncA()", out var found));
            Assert.AreSame(p, found);
        }

        [Test]
        public void TryAdd_DuplicateKey_ReturnsFalse_BothSuppressed()
        {
            var lib = new ProviderLibrary();
            lib.TryAdd(new StubProvider("FuncA()", GuidA));
            var result = lib.TryAdd(new StubProvider("FuncA()", GuidB));

            Assert.IsFalse(result);
            Assert.IsFalse(lib.TryGet<IDefinition>("FuncA()", out _));
            Assert.IsEmpty(lib.ProvidersByVersion<IDefinition>("FuncA()").ToList());
        }

        [Test]
        public void TryAdd_ThirdRegistrationOfConflictedKey_StaysConflicted()
        {
            var lib = new ProviderLibrary();
            lib.TryAdd(new StubProvider("FuncA()", GuidA));
            lib.TryAdd(new StubProvider("FuncA()", GuidB));
            lib.TryAdd(new StubProvider("FuncA()", GuidC));

            Assert.IsFalse(lib.TryGet<IDefinition>("FuncA()", out _));
        }

        [Test]
        public void TryAdd_MultipleDistinctKeys_AllRetrievable()
        {
            var lib = new ProviderLibrary();
            var pA = new StubProvider("FuncA()", GuidA);
            var pB = new StubProvider("FuncB()", GuidA);
            var pC = new StubProvider("FuncC()", GuidB);
            lib.TryAdd(pA); lib.TryAdd(pB); lib.TryAdd(pC);

            Assert.IsTrue(lib.TryGet<IDefinition>("FuncA()", out var a)); Assert.AreSame(pA, a);
            Assert.IsTrue(lib.TryGet<IDefinition>("FuncB()", out var b)); Assert.AreSame(pB, b);
            Assert.IsTrue(lib.TryGet<IDefinition>("FuncC()", out var c)); Assert.AreSame(pC, c);
        }

        // ── Clear ────────────────────────────────────────────────────────────────

        [Test]
        public void Clear_RemovesAllProvidersFromAsset()
        {
            var lib = new ProviderLibrary();
            lib.TryAdd(new StubProvider("FuncA()", GuidA));
            lib.TryAdd(new StubProvider("FuncB()", GuidA));
            lib.TryAdd(new StubProvider("FuncC()", GuidB));

            lib.ClearByAssetID(GuidA);

            Assert.IsFalse(lib.TryGet<IDefinition>("FuncA()", out _));
            Assert.IsFalse(lib.TryGet<IDefinition>("FuncB()", out _));
            Assert.IsTrue(lib.TryGet<IDefinition>("FuncC()", out _));
        }

        [Test]
        public void Clear_OneOfConflictingAssets_SurvivorRetrievable()
        {
            var lib = new ProviderLibrary();
            var survivor = new StubProvider("FuncA()", GuidB);
            lib.TryAdd(new StubProvider("FuncA()", GuidA));
            lib.TryAdd(survivor);

            Assert.IsFalse(lib.TryGet<IDefinition>("FuncA()", out _), "Expected conflict while both present.");

            lib.ClearByAssetID(GuidA);

            Assert.IsTrue(lib.TryGet<IDefinition>("FuncA()", out var found));
            Assert.AreSame(survivor, found);
        }

        [Test]
        public void Clear_BothConflictingAssets_RemovesKeyEntirely()
        {
            var lib = new ProviderLibrary();
            lib.TryAdd(new StubProvider("FuncA()", GuidA));
            lib.TryAdd(new StubProvider("FuncA()", GuidB));

            lib.ClearByAssetID(GuidA);
            lib.ClearByAssetID(GuidB);

            Assert.IsFalse(lib.TryGet<IDefinition>("FuncA()", out _));
            Assert.IsEmpty(lib.ProvidersByVersion<IDefinition>("FuncA()").ToList());
        }

        [Test]
        public void Clear_UnknownAsset_DoesNotThrow()
        {
            var lib = new ProviderLibrary();
            Assert.DoesNotThrow(() => lib.ClearByAssetID(GuidA));
        }

        [Test]
        public void Clear_ThenReAdd_NewProviderIsRetrievable()
        {
            var lib = new ProviderLibrary();
            lib.TryAdd(new StubProvider("FuncA()", GuidA));
            lib.ClearByAssetID(GuidA);

            var fresh = new StubProvider("FuncA()", GuidB);
            Assert.IsTrue(lib.TryAdd(fresh));
            Assert.IsTrue(lib.TryGet<IDefinition>("FuncA()", out var found));
            Assert.AreSame(fresh, found);
        }

        // ── Conflict suppression across keys ────────────────────────────────────

        [Test]
        public void TryAdd_ConflictedKey_DoesNotMaskSiblingKey()
        {
            var lib = new ProviderLibrary();
            var unique = new StubProvider("FuncB()", GuidB);
            lib.TryAdd(new StubProvider("FuncA()", GuidA));
            lib.TryAdd(new StubProvider("FuncA()", GuidC));
            lib.TryAdd(unique);

            Assert.IsFalse(lib.TryGet<IDefinition>("FuncA()", out _), "FuncA conflict suppresses the key.");
            Assert.IsTrue(lib.TryGet<IDefinition>("FuncB()", out var found), "FuncB is unrelated and stays retrievable.");
            Assert.AreSame(unique, found);
        }

    }

    [TestFixture]
    class ProviderLibraryProvidersByVersionTests : ProviderLibraryTestFixtureBase
    {
        [Test]
        public void ProvidersByVersion_UnknownKey_ReturnsEmpty()
        {
            var lib = new ProviderLibrary();
            Assert.IsEmpty(lib.ProvidersByVersion<IShaderFunction>("Missing()").ToList());
        }

        [Test]
        public void ProvidersByVersion_SingleRegistration_ReturnsIt()
        {
            var lib = new ProviderLibrary();
            var p = new ReflectedFunctionProvider(GuidA, FuncWithVersion("FuncA", "1.0"));
            lib.TryAdd(p);

            var results = lib.ProvidersByVersion<IShaderFunction>("FuncA()").ToList();
            Assert.AreEqual(1, results.Count);
            Assert.AreSame(p, results[0]);
            Assert.AreEqual("1.0", results[0].Version);
        }

        [Test]
        public void ProvidersByVersion_ConflictedKey_ReturnsEmpty()
        {
            var lib = new ProviderLibrary();
            // No sg:Version hints — same key without versions is a true conflict.
            var pA = new ReflectedFunctionProvider(GuidA, FuncNoVersion("FuncA"));
            var pB = new ReflectedFunctionProvider(GuidB, FuncNoVersion("FuncA"));
            lib.TryAdd(pA);
            lib.TryAdd(pB);

            // Conflicted keys are suppressed consistently across all library query surfaces.
            Assert.IsFalse(lib.TryGet<IShaderFunction>("FuncA()", out _));
            Assert.IsEmpty(lib.ProvidersByVersion<IShaderFunction>("FuncA()").ToList());
        }

        [Test]
        public void ProvidersByVersion_VersionedKey_ReturnsAll_NewestFirst()
        {
            var lib = new ProviderLibrary();
            var pA = new ReflectedFunctionProvider(GuidA, FuncWithVersion("FuncA", "1.0"));
            var pB = new ReflectedFunctionProvider(GuidB, FuncWithVersion("FuncA", "2.0"));
            lib.TryAdd(pA);
            lib.TryAdd(pB);

            var results = lib.ProvidersByVersion<IShaderFunction>("FuncA()").ToList();
            Assert.AreEqual(2, results.Count);
            Assert.AreSame(pB, results[0], "Newest version should sort first.");
            Assert.AreEqual("2.0", results[0].Version);
            Assert.AreEqual("1.0", results[1].Version);
        }

        [Test]
        public void ProvidersByVersion_ProviderWithoutVersionHint_ReportsKUnversioned()
        {
            var lib = new ProviderLibrary();
            var p = new ReflectedFunctionProvider(GuidA, FuncNoVersion("FuncA"));
            lib.TryAdd(p);

            var results = lib.ProvidersByVersion<IShaderFunction>("FuncA()").ToList();
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(Version.kUnversioned, results[0].Version);
        }

    }

    [TestFixture]
    class ProviderLibraryVersionTests : ProviderLibraryTestFixtureBase
    {
        // ── TryAdd — versioned coexistence ────────────────────────────────────
        // Basic "two distinct versions coexist, newest sorts first" coverage lives in
        // ProvidersByVersion_VersionedKey_ReturnsAll_NewestFirst above. This fixture
        // covers cases that go beyond that: three versions, unversioned coexistence,
        // same-version conflict, partial conflicts, and per-version Clear scoping.

        [Test]
        public void TryAdd_ThreeDistinctVersions_LatestSortsFirst()
        {
            var lib = new ProviderLibrary();
            var p1  = new ReflectedFunctionProvider(GuidA, FuncWithVersion("FuncA", "1.0"));
            var p15 = new ReflectedFunctionProvider(GuidB, FuncWithVersion("FuncA", "1.5"));
            var p2  = new ReflectedFunctionProvider(GuidC, FuncWithVersion("FuncA", "2.0"));
            lib.TryAdd(p1); lib.TryAdd(p15); lib.TryAdd(p2);

            var byVersion = lib.ProvidersByVersion<IShaderFunction>("FuncA()").ToList();
            Assert.AreEqual(3, byVersion.Count);
            Assert.AreSame(p2, byVersion[0], "Expected the 2.0 provider as the latest.");
        }

        [Test]
        public void TryAdd_SameKey_SameVersion_IsConflict()
        {
            var lib = new ProviderLibrary();
            lib.TryAdd(new ReflectedFunctionProvider(GuidA, FuncWithVersion("FuncA", "1.0")));
            var result = lib.TryAdd(new ReflectedFunctionProvider(GuidB, FuncWithVersion("FuncA", "1.0")));

            Assert.IsFalse(result);
            Assert.IsFalse(lib.TryGet<IShaderFunction>("FuncA()", out _));
        }

        [Test]
        public void TryAdd_SameKey_OneWithoutVersion_Coexists()
        {
            // Unversioned providers participate in versioned coexistence — they sort as the
            // oldest possible "version" (kUnversioned), so a versioned sibling shadows them
            // as the newest. No conflict.
            var lib = new ProviderLibrary();
            var versioned   = new ReflectedFunctionProvider(GuidA, FuncWithVersion("FuncA", "1.0"));
            var unversioned = new ReflectedFunctionProvider(GuidB, FuncNoVersion("FuncA"));

            Assert.IsTrue(lib.TryAdd(versioned));
            Assert.IsTrue(lib.TryAdd(unversioned));

            // The versioned provider sorts first as the latest.
            var byVersion = lib.ProvidersByVersion<IShaderFunction>("FuncA()").ToList();
            Assert.AreEqual(2, byVersion.Count);
            Assert.AreSame(versioned, byVersion[0]);
        }

        // ── Clear — versioned key cleanup ─────────────────────────────────────

        [Test]
        public void Clear_VersionedKey_SingleSurvivor_IsNoLongerVersioned()
        {
            var lib = new ProviderLibrary();
            var p1 = new ReflectedFunctionProvider(GuidA, FuncWithVersion("FuncA", "1.0"));
            var p2 = new ReflectedFunctionProvider(GuidB, FuncWithVersion("FuncA", "2.0"));
            lib.TryAdd(p1);
            lib.TryAdd(p2);

            lib.ClearByAssetID(GuidB); // remove the 2.0 provider

            // Single survivor — still retrievable, no longer versioned (just a normal single entry).
            Assert.IsTrue(lib.TryGet<IShaderFunction>("FuncA()", out var found));
            Assert.AreSame(p1, found);
            Assert.AreEqual(1, lib.ProvidersByVersion<IShaderFunction>("FuncA()").ToList().Count);
        }

        // ── Per-version conflict scoping (TryAdd + Clear) ─────────────────────
        //
        // Conflict scoping is per-version: a collision on one version of a key must not
        // suppress other versions, and Clear must re-evaluate conflict state on the
        // remaining buckets without false resolution or lingering conflict.

        [Test]
        public void TryAdd_PartialConflict_OnlyAffectedVersionSuppressed()
        {
            var lib = new ProviderLibrary();
            var pA = new ReflectedFunctionProvider(GuidA, FuncWithVersion("FuncA", "1.0"));
            var pB = new ReflectedFunctionProvider(GuidB, FuncWithVersion("FuncA", "1.0"));
            var pC = new ReflectedFunctionProvider(GuidC, FuncWithVersion("FuncA", "2.0"));

            lib.TryAdd(pA);
            lib.TryAdd(pB);   // collides with pA on v1.0
            Assert.IsTrue(lib.TryAdd(pC), "v2.0 should register cleanly — unrelated to the v1.0 collision.");

            // ProvidersByVersion skips the conflicted v1.0 bucket; pC (v2.0) is the only entry.
            var byVersion = lib.ProvidersByVersion<IShaderFunction>("FuncA()").ToList();
            Assert.AreEqual(1, byVersion.Count);
            Assert.AreSame(pC, byVersion[0]);
            Assert.AreEqual("2.0", byVersion[0].Version);
        }

        [Test]
        public void Clear_PartialConflict_ResolvesOnlyAffectedVersion()
        {
            var lib = new ProviderLibrary();
            var pA = new ReflectedFunctionProvider(GuidA, FuncWithVersion("FuncA", "1.0"));
            var pB = new ReflectedFunctionProvider(GuidB, FuncWithVersion("FuncA", "1.0"));
            var pC = new ReflectedFunctionProvider(GuidC, FuncWithVersion("FuncA", "2.0"));
            lib.TryAdd(pA);
            lib.TryAdd(pB);
            lib.TryAdd(pC);

            // Removing one of the v1.0 collisions resolves the v1.0 conflict alone — v2.0
            // is untouched and still sorts first as the newest.
            lib.ClearByAssetID(GuidB);

            var byVersion = lib.ProvidersByVersion<IShaderFunction>("FuncA()").ToList();
            Assert.AreEqual(2, byVersion.Count);
            Assert.AreSame(pC, byVersion[0]);
            Assert.AreEqual("2.0", byVersion[0].Version);
            Assert.AreEqual("1.0", byVersion[1].Version);
        }

        // Two versioned + one unversioned coexist freely. Clearing the unversioned one leaves
        // the versioned pair intact and continues to pick the newest as the representative.
        [Test]
        public void Clear_UnversionedAmongVersioned_LeavesVersionedNewestAccessible()
        {
            var lib = new ProviderLibrary();
            var pA = new ReflectedFunctionProvider(GuidA, FuncWithVersion("FuncA", "1.0"));
            var pB = new ReflectedFunctionProvider(GuidB, FuncWithVersion("FuncA", "2.0"));
            var pC = new ReflectedFunctionProvider(GuidC, FuncNoVersion("FuncA"));

            Assert.IsTrue(lib.TryAdd(pA));
            Assert.IsTrue(lib.TryAdd(pB));
            Assert.IsTrue(lib.TryAdd(pC));

            // All three coexist — pB (v2.0) sorts first as the newest representative
            // even with pC present.
            Assert.AreSame(pB, lib.ProvidersByVersion<IShaderFunction>("FuncA()").ToList()[0]);

            lib.ClearByAssetID(GuidC);

            Assert.AreSame(pB, lib.ProvidersByVersion<IShaderFunction>("FuncA()").ToList()[0],
                "Newest versioned provider should remain accessible after clearing the unversioned one.");
        }

        // When the remaining Count > 1 providers still don't form a valid versioned set
        // (duplicate versions), the entry must stay conflicted — no false resolution.
        [Test]
        public void Clear_ConflictedEntry_DuplicateVersionsRemain_StaysConflicted()
        {
            var lib = new ProviderLibrary();
            var pA = new ReflectedFunctionProvider(GuidA, FuncWithVersion("FuncA", "1.0"));
            var pB = new ReflectedFunctionProvider(GuidB, FuncWithVersion("FuncA", "2.0"));
            var pC = new ReflectedFunctionProvider(GuidC, FuncWithVersion("FuncA", "1.0")); // same version as pA

            lib.TryAdd(pA);
            lib.TryAdd(pB); // versioned coexistence so far
            lib.TryAdd(pC); // duplicate v1.0 → conflict

            lib.ClearByAssetID(GuidB); // remove pB (v2.0), leaving pA(v1.0) and pC(v1.0) — still conflicted

            Assert.IsEmpty(lib.ProvidersByVersion<IShaderFunction>("FuncA()").ToList(), "Duplicate versions must keep the entry conflicted.");
            Assert.IsFalse(lib.TryGet<IShaderFunction>("FuncA()", out _), "TryGet must return false for a still-conflicted key.");
        }
    }

    // Reload's only job is to resolve the serialized (m_providerKey, m_sourceAssetId) pair
    // via TryResolve, or leave m_definition null on failure. Migration after a function
    // moves between assets is handled out-of-band by the ProviderLibrary.OnProvidersReloaded
    // listener in ShaderGraph, which patches the saved graph file directly — that path is
    // covered by PostProcessorIntegrationTests, not here.
    [TestFixture]
    class ReflectedFunctionProviderReloadTests
    {
        // A GUID that points to no real asset — TryResolve will always fail for it.
        static readonly GUID DeadGuid = new GUID("00000000000000000000000000000001");

        static readonly FieldInfo s_defField =
            typeof(ReflectedFunctionProvider).GetField("m_definition", BindingFlags.Instance | BindingFlags.NonPublic);

        // Constructor wiring of the Version hint into the provider's Version property is
        // covered by ProvidersByVersion_SingleRegistration_ReturnsIt and
        // ProvidersByVersion_VersionIsUnversionedWhenNotDeclared above, which exercise the
        // same behaviour through the public API rather than reflecting into private state.

        [Test]
        public void Reload_SerializedAssetDoesNotResolve_LeavesDefinitionNull()
        {
            var def = new ShaderFunction("TestFunc", null, null, new ShaderType("float"), "return 1;", null);
            var stale = new ReflectedFunctionProvider(DeadGuid, def);
            s_defField.SetValue(stale, null); // force Reload

            Assert.IsNull(stale.Definition,
                "Reload must not consult the library — it either resolves via the serialized GUID or returns null.");
        }
    }

    [TestFixture]
    class GroupKeyHintTests
    {
        // Exercises GroupKey.Initialize directly — no library needed, no logs expected.

        static GroupKey ParseGroupKey(string rawValue)
        {
            var hint = new GroupKey();
            hint.Initialize(true, rawValue, null, null, Func.kGroupKey, new Dictionary<string, string>());
            return hint;
        }

        [Test]
        public void GroupKey_OneToken_ParsesKeyOnly()
        {
            // DisplayName defaults to the group key when no third token is provided.
            var hint = ParseGroupKey("BlendOp");
            Assert.IsTrue(hint.IsResolved);
            Assert.AreEqual("BlendOp", hint.Value);
            Assert.AreEqual(0, hint.Priority);
            Assert.AreEqual("BlendOp", hint.DisplayName);
            Assert.IsNull(hint.Label);
        }

        [Test]
        public void GroupKey_TwoTokens_ParsesKeyAndPriority()
        {
            var hint = ParseGroupKey("BlendOp, 10");
            Assert.IsTrue(hint.IsResolved);
            Assert.AreEqual("BlendOp", hint.Value);
            Assert.AreEqual(10, hint.Priority);
            Assert.AreEqual("BlendOp", hint.DisplayName);
            Assert.IsNull(hint.Label);
        }

        [Test]
        public void GroupKey_ThreeTokens_ParsesKeyPriorityAndDisplayName()
        {
            var hint = ParseGroupKey("BlendOp, 10, Blend");
            Assert.IsTrue(hint.IsResolved);
            Assert.AreEqual("BlendOp", hint.Value);
            Assert.AreEqual(10, hint.Priority);
            Assert.AreEqual("Blend", hint.DisplayName);
            Assert.IsNull(hint.Label);
        }

        [Test]
        public void GroupKey_FourTokens_ParsesAllFields()
        {
            var hint = ParseGroupKey("BlendOp, 10, Blend, Blend Mode");
            Assert.IsTrue(hint.IsResolved);
            Assert.AreEqual("BlendOp", hint.Value);
            Assert.AreEqual(10, hint.Priority);
            Assert.AreEqual("Blend", hint.DisplayName);
            Assert.AreEqual("Blend Mode", hint.Label);
        }

        [Test]
        public void GroupKey_FiveTokens_ErrorsAndDoesNotResolve()
        {
            var hint = ParseGroupKey("BlendOp, 10, Blend, Blend Mode, Extra");
            Assert.IsFalse(hint.IsResolved);
            Assert.IsNotNull(hint.Message);
        }

        [Test]
        public void GroupKey_NonIntegerPriority_ErrorsAndDoesNotResolve()
        {
            var hint = ParseGroupKey("BlendOp, notanint");
            Assert.IsFalse(hint.IsResolved);
            Assert.IsNotNull(hint.Message);
        }

        [Test]
        public void GroupKey_NegativePriority_IsValid()
        {
            var hint = ParseGroupKey("BlendOp, -5");
            Assert.IsTrue(hint.IsResolved);
            Assert.AreEqual(-5, hint.Priority);
        }
    }

}
