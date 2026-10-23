using System;
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Text;
using Unity.Burst;

// change this to change the core type under test
using FixedStringN = Unity.Collections.FixedString128Bytes;

namespace FixedStringTests
{
    [BurstCompile]
    internal class FixedStringBurstTests
    {
        [BurstCompile]
        static int BurstAppendFn(ref FixedStringN fs, in FixedString32Bytes other)
        {
            fs.Append(in other);
            return fs.Length;
        }

        delegate int BurstAppendDelegate(ref FixedStringN a, in FixedString32Bytes b);

        [Test]
        public void TestBurstAppend()
        {
            var fp = BurstCompiler.CompileFunctionPointer<BurstAppendDelegate>(BurstAppendFn);
            var invoke = fp.Invoke;

            FixedStringN a = new FixedStringN("Hello ");
            FixedString32Bytes b = new FixedString32Bytes("World");

            var len = invoke(ref a, b);
            Assert.AreEqual(11, len);
            Assert.AreEqual("Hello World", a.ToString());
        }

        const int kMatchesLiteral = 1 << 0;
        const int kDiffersFromLiteral = 1 << 1;
        const int kMatchesNull = 1 << 2;
        const int kMatchesOverCapacityLiteral = 1 << 3;
        const int kWasBurstCompiled = 1 << 4;

        [BurstDiscard]
        static void ClearIfManaged(ref bool wasBurstCompiled) => wasBurstCompiled = false;

        [BurstCompile(CompileSynchronously = true)]
        static int BurstCompareWithManagedStringFn(ref FixedString32Bytes fs)
        {
            var wasBurstCompiled = true;
            ClearIfManaged(ref wasBurstCompiled);

            var flags = 0;
            if (fs == "Hello")
                flags |= kMatchesLiteral;
            if (fs != "World")
                flags |= kDiffersFromLiteral;
            if (fs == null)
                flags |= kMatchesNull;
            if (fs == "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
                flags |= kMatchesOverCapacityLiteral;
            if (wasBurstCompiled)
                flags |= kWasBurstCompiled;
            return flags;
        }

        delegate int BurstCompareWithManagedStringDelegate(ref FixedString32Bytes fs);

        [Test]
        public void TestBurstCompareWithManagedString()
        {
            var fp = BurstCompiler.CompileFunctionPointer<BurstCompareWithManagedStringDelegate>(BurstCompareWithManagedStringFn);
            var invoke = fp.Invoke;

            var hello = new FixedString32Bytes("Hello");
            Assert.AreEqual(kMatchesLiteral | kDiffersFromLiteral | kWasBurstCompiled, invoke(ref hello));

            var empty = new FixedString32Bytes();
            Assert.AreEqual(kDiffersFromLiteral | kMatchesNull | kWasBurstCompiled, invoke(ref empty));
        }
    }
}
