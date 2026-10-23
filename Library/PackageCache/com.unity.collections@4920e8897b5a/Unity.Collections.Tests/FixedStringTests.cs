using System;
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Text;

// change this to change the core type under test
using FixedStringN = Unity.Collections.FixedString128Bytes;

namespace Unity.Collections.Tests
{
    internal static class FixedStringTestUtils
    {
        internal unsafe static void Junk<T>(ref this T fs)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            var bytes = fs.GetUnsafePtr();
            var cap = fs.Capacity;
            // Match MSVC stack init pattern
            UnsafeUtility.MemSet(bytes, 0xcc, cap);
        }

        internal unsafe static void AssertNullTerminated<T>(this T fs)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            Assert.AreEqual(0, fs.GetUnsafePtr()[fs.Length]);
        }
    }

    internal class FixedStringTests
    {
        [Test]
        public void FixedStringFormat()
        {
            Assert.AreEqual("1 0", FixedString.Format("{0} {1}", 1, 0));
            Assert.AreEqual("0.1 1.2", FixedString.Format("{0} {1}", 0.1f, 1.2f));
            Assert.AreEqual("error 500 in line 350: bubbly", FixedString.Format("error {0} in line {1}: {2}", 500, 350, "bubbly"));
        }

        [Test]
        public void FixedStringNFormatExtension1Params()
        {
            FixedStringN aa = default;
            aa.Junk();
            FixedStringN format = "{0}";
            FixedString32Bytes arg0 = "a";
            Assert.AreEqual(FormatError.None, aa.AppendFormat(format, arg0));
            Assert.AreEqual("a", aa);
            aa.AssertNullTerminated();
        }


        [Test]
        public void FixedStringNFormatExtension2Params()
        {
            FixedStringN aa = default;
            aa.Junk();
            FixedStringN format = "{0} {1}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            Assert.AreEqual(FormatError.None, aa.AppendFormat(format, arg0, arg1));
            Assert.AreEqual("a b", aa);
            aa.AssertNullTerminated();
        }


        [Test]
        public void FixedStringNFormatExtension3Params()
        {
            FixedStringN aa = default;
            aa.Junk();
            FixedStringN format = "{0} {1} {2}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            Assert.AreEqual(FormatError.None, aa.AppendFormat(format, arg0, arg1, arg2));
            Assert.AreEqual("a b c", aa);
            aa.AssertNullTerminated();
        }


        [Test]
        public void FixedStringNFormatExtension4Params()
        {
            FixedStringN aa = default; 
            aa.Junk();
            FixedStringN format = "{0} {1} {2} {3}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            Assert.AreEqual(FormatError.None, aa.AppendFormat(format, arg0, arg1, arg2, arg3));
            Assert.AreEqual("a b c d", aa);
            aa.AssertNullTerminated();
        }


        [Test]
        public void FixedStringNFormatExtension5Params()
        {
            FixedStringN aa = default;
            aa.Junk();
            FixedStringN format = "{0} {1} {2} {3} {4}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            FixedString32Bytes arg4 = "e";
            Assert.AreEqual(FormatError.None, aa.AppendFormat(format, arg0, arg1, arg2, arg3, arg4));
            Assert.AreEqual("a b c d e", aa);
            aa.AssertNullTerminated();
        }


        [Test]
        public void FixedStringNFormatExtension6Params()
        {
            FixedStringN aa = default;
            aa.Junk();
            FixedStringN format = "{0} {1} {2} {3} {4} {5}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            FixedString32Bytes arg4 = "e";
            FixedString32Bytes arg5 = "f";
            Assert.AreEqual(FormatError.None, aa.AppendFormat(format, arg0, arg1, arg2, arg3, arg4, arg5));
            Assert.AreEqual("a b c d e f", aa);
            aa.AssertNullTerminated();
        }


        [Test]
        public void FixedStringNFormatExtension7Params()
        {
            FixedStringN aa = default;
            aa.Junk();
            FixedStringN format = "{0} {1} {2} {3} {4} {5} {6}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            FixedString32Bytes arg4 = "e";
            FixedString32Bytes arg5 = "f";
            FixedString32Bytes arg6 = "g";
            Assert.AreEqual(FormatError.None, aa.AppendFormat(format, arg0, arg1, arg2, arg3, arg4, arg5, arg6));
            Assert.AreEqual("a b c d e f g", aa);
            aa.AssertNullTerminated();
        }


        [Test]
        public void FixedStringNFormatExtension8Params()
        {
            FixedStringN aa = default;
            aa.Junk();
            FixedStringN format = "{0} {1} {2} {3} {4} {5} {6} {7}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            FixedString32Bytes arg4 = "e";
            FixedString32Bytes arg5 = "f";
            FixedString32Bytes arg6 = "g";
            FixedString32Bytes arg7 = "h";
            Assert.AreEqual(FormatError.None, aa.AppendFormat(format, arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7));
            Assert.AreEqual("a b c d e f g h", aa);
            aa.AssertNullTerminated();
        }


        [Test]
        public void FixedStringNFormatExtension9Params()
        {
            FixedStringN aa = default;
            aa.Junk();
            FixedStringN format = "{0} {1} {2} {3} {4} {5} {6} {7} {8}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            FixedString32Bytes arg4 = "e";
            FixedString32Bytes arg5 = "f";
            FixedString32Bytes arg6 = "g";
            FixedString32Bytes arg7 = "h";
            FixedString32Bytes arg8 = "i";
            Assert.AreEqual(FormatError.None, aa.AppendFormat(format, arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));
            Assert.AreEqual("a b c d e f g h i", aa);
            aa.AssertNullTerminated();
        }


        [Test]
        public void FixedStringNFormatExtension10Params()
        {
            FixedStringN aa = default;
            aa.Junk();
            FixedStringN format = "{0} {1} {2} {3} {4} {5} {6} {7} {8} {9}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            FixedString32Bytes arg4 = "e";
            FixedString32Bytes arg5 = "f";
            FixedString32Bytes arg6 = "g";
            FixedString32Bytes arg7 = "h";
            FixedString32Bytes arg8 = "i";
            FixedString32Bytes arg9 = "j";
            Assert.AreEqual(FormatError.None, aa.AppendFormat(format, arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));
            Assert.AreEqual("a b c d e f g h i j", aa);
            aa.AssertNullTerminated();
        }

        [Test]
        public void FixedStringNFormatBadFormat()
        {
            FixedStringN aa = default;
            aa.Junk();
            FixedStringN format = "{10}";
            FixedString32Bytes arg0 = "a";
            Assert.AreEqual(FormatError.BadFormatSpecifier, aa.AppendFormat(format, arg0));
            format = "{0 } ";
            Assert.AreEqual(FormatError.BadFormatSpecifier, aa.AppendFormat(format, arg0));
            format = "{ 0} ";
            Assert.AreEqual(FormatError.BadFormatSpecifier, aa.AppendFormat(format, arg0));
            format = "{0a} ";
            Assert.AreEqual(FormatError.BadFormatSpecifier, aa.AppendFormat(format, arg0));
            format = "{012 ";
            Assert.AreEqual(FormatError.BadFormatSpecifier, aa.AppendFormat(format, arg0));
            format = "{0{ ";
            Assert.AreEqual(FormatError.BadFormatSpecifier, aa.AppendFormat(format, arg0));
            format = "{0{ ";
            Assert.AreEqual(FormatError.BadFormatSpecifier, aa.AppendFormat(format, arg0));
            format = "{0} } ";
            Assert.AreEqual(FormatError.BadFormatSpecifier, aa.AppendFormat(format, arg0));
            format = "{ {0} ";
            Assert.AreEqual(FormatError.BadFormatSpecifier, aa.AppendFormat(format, arg0));
            format = "{{{0}} ";
            Assert.AreEqual(FormatError.BadFormatSpecifier, aa.AppendFormat(format, arg0));
            format = "{{0} ";
            Assert.AreEqual(FormatError.BadFormatSpecifier, aa.AppendFormat(format, arg0));
            format = "{0}} ";
            Assert.AreEqual(FormatError.BadFormatSpecifier, aa.AppendFormat(format, arg0));
            aa.AssertNullTerminated();
        }

        [Test]
        public void FixedStringNFormatOverflow()
        {
            FixedString32Bytes aa = default;
            aa.Junk();
            FixedStringN format = "{0}";
            FixedStringN arg0 = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            Assert.AreEqual(FormatError.Overflow, aa.AppendFormat(format, arg0));
        }

        [Test]
        public void FixedStringNFormatBraces()
        {
            FixedStringN aa = default;
            aa.Junk();
            FixedStringN format = "{{0}}";
            FixedString32Bytes arg0 = "42";
            Assert.AreEqual(FormatError.None, aa.AppendFormat(format, arg0));
            Assert.AreEqual("{0}", aa);
            aa.AssertNullTerminated();

            aa = default;
            format = "{{{0}}}";
            arg0 = "43";
            Assert.AreEqual(FormatError.None, aa.AppendFormat(format, arg0));
            Assert.AreEqual("{43}", aa);
            aa.AssertNullTerminated();

            aa = default;
            format = "{{{0}";
            arg0 = "44";
            Assert.AreEqual(FormatError.None, aa.AppendFormat(format, arg0));
            Assert.AreEqual("{44", aa);
            aa.AssertNullTerminated();

            aa = default;
            format = "{0}}}";
            arg0 = "45";
            Assert.AreEqual(FormatError.None, aa.AppendFormat(format, arg0));
            Assert.AreEqual("45}", aa);
            aa.AssertNullTerminated();
        }

        [Test]
        public void FixedStringNAppendString()
        {
            FixedStringN aa = default;
            Assert.AreEqual(CopyError.None, aa.CopyFrom(new FixedString32Bytes("aa")));
            Assert.AreEqual("aa", aa.ToString());
            Assert.AreEqual(FormatError.None, aa.Append("bb"));
            Assert.AreEqual("aabb", aa.ToString());
        }

        [Test]
        public void FixedStringRuneWorks()
        {
            var rune = new Unicode.Rune(0xfbad);

            FixedStringN a = new FixedStringN(rune, 3);
            FixedStringN b = default;
            Assert.AreEqual(FormatError.None, b.Append(rune));
            Assert.AreEqual(FormatError.None, b.Append(rune, 2));
            Assert.AreEqual(a.ToString(), b.ToString());
        }

        [TestCase("Antidisestablishmentarianism")]
        [TestCase("⁣🌹🌻🌷🌿🌵🌾⁣")]
        public void FixedStringNCopyFromBytesWorks(String a)
        {
            FixedStringN aa = default;
            aa.Junk();

            Assert.AreEqual(CopyError.None, aa.CopyFrom(a));

            Assert.AreEqual(a, aa.ToString());
            aa.AssertNullTerminated();

            Assert.AreEqual(FormatError.None, aa.Append("tail"));
            Assert.AreEqual(a + "tail", aa.ToString());
            aa.AssertNullTerminated();
        }

        [TestCase("red")]
        [TestCase("紅色", TestName = "{m}(Chinese-Red)")]
        [TestCase("George Washington")]
        [TestCase("村上春樹", TestName = "{m}(HarukiMurakami)")]
        public void FixedStringNToStringWorks(String a)
        {
            FixedStringN aa = new FixedStringN(a);
            Assert.AreEqual(a, aa.ToString());
            aa.AssertNullTerminated();
        }

        [TestCase("monkey", "monkey")]
        [TestCase("yellow", "green")]
        [TestCase("violet", "紅色", TestName = "{m}(Violet-Chinese-Red")]
        [TestCase("绿色", "蓝色", TestName = "{m}(Chinese-Green-Blue")]
        [TestCase("靛蓝色", "紫罗兰色", TestName = "{m}(Chinese-Indigo-Violet")]
        [TestCase("James Monroe", "John Quincy Adams")]
        [TestCase("Andrew Jackson", "村上春樹", TestName = "{m}(AndrewJackson-HarukiMurakami")]
        [TestCase("三島 由紀夫", "吉本ばなな", TestName = "{m}(MishimaYukio-YoshimotoBanana")]
        public void FixedStringNEqualsWorks(String a, String b)
        {
            FixedStringN aa = new FixedStringN(a);
            FixedStringN bb = new FixedStringN(b);
            Assert.AreEqual(aa.Equals(bb), a.Equals(b));
            aa.AssertNullTerminated();
            bb.AssertNullTerminated();
        }

        [Test]
        public void FixedStringNForEach()
        {
            FixedStringN actual = "A🌕Z🌑";
            FixedList32Bytes<int> expected = default;
            expected.Add('A');
            expected.Add(0x1F315);
            expected.Add('Z');
            expected.Add(0x1F311);
            int index = 0;
            foreach (var rune in actual)
            {
                Assert.AreEqual(expected[index], rune.value);
                ++index;
            }
        }

        [Test]
        public void FixedStringNSubstring()
        {
            FixedStringN a = "This is substring.";

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.Throws<ArgumentOutOfRangeException>(() => a.Substring(-8, 9));
            Assert.Throws<ArgumentOutOfRangeException>(() => a.Substring(200, 9));
            Assert.Throws<ArgumentOutOfRangeException>(() => a.Substring(8, -9));
#endif

            {
                FixedStringN b = a.Substring(8, 9);
                Assert.IsTrue(b.Equals("substring"));
            }

            {
                FixedStringN b = a.Substring(8, 100);
                Assert.IsTrue(b.Equals("substring."));
            }
        }

        [Test]
        public void FixedStringNIndexOf()
        {
            FixedStringN a = "bookkeeper bookkeeper";
            FixedStringN b = "ookkee";
            Assert.AreEqual(1, a.IndexOf(b));
            Assert.AreEqual(-1, b.IndexOf(a));
        }

        [Test]
        public void FixedStringNLastIndexOf()
        {
            FixedStringN a = "bookkeeper bookkeeper";
            FixedStringN b = "ookkee";
            Assert.AreEqual(12, a.LastIndexOf(b));
            Assert.AreEqual(-1, b.LastIndexOf(a));
        }

        [Test]
        public void FixedStringNContains()
        {
            FixedStringN a = "bookkeeper";
            FixedStringN b = "ookkee";
            Assert.AreEqual(true, a.Contains(b));
        }

        [Test]
        public void FixedStringNComparisons()
        {
            FixedStringN a = "apple";
            FixedStringN b = "banana";
            Assert.AreEqual(false, a.Equals(b));
            Assert.AreEqual(true, !b.Equals(a));

            {
                var string40Length = new string('a', 40);
                var fixedString = new FixedString64Bytes(string40Length);
                Assert.AreEqual(true, string40Length == fixedString);
                Assert.AreEqual(true, fixedString.Equals(string40Length));
                Assert.AreEqual(true, fixedString == string40Length);
            }
        }

        [Test]
        public void FixedStringN_EqualityWithString_DoesNotThrowWhenStringExceedsCapacity()
        {
            var fixedString = new FixedString64Bytes("hello");
            var longString = new string('a', 200);

            Assert.AreEqual(false, fixedString == longString);
            Assert.AreEqual(true, fixedString != longString);
            Assert.AreEqual(false, longString == fixedString);
            Assert.AreEqual(true, longString != fixedString);
        }

        [Test]
        public void FixedStringN_EqualityWithString_HandlesNullString()
        {
            var fixedString = new FixedString64Bytes("hello");
            string nullString = null;

            Assert.AreEqual(false, fixedString == nullString);
            Assert.AreEqual(true, fixedString != nullString);
            Assert.AreEqual(false, nullString == fixedString);
            Assert.AreEqual(true, nullString != fixedString);

            var emptyFixed = new FixedString64Bytes();
            Assert.AreEqual(true, emptyFixed == nullString);
            Assert.AreEqual(false, emptyFixed != nullString);
            Assert.AreEqual(true, nullString == emptyFixed);
            Assert.AreEqual(false, nullString != emptyFixed);
        }

        [Test]
        public void FixedStringN_EqualityWithString_HandlesEmbeddedNulls()
        {
            var fixedString = new FixedString64Bytes("A\0BB");

            Assert.AreEqual(false, fixedString == "A\0C");
            Assert.AreEqual(true, fixedString != "A\0C");
            Assert.AreEqual(true, fixedString == "A\0BB");
            Assert.AreEqual(false, fixedString != "A\0BB");
            Assert.AreEqual(false, fixedString == "A\0");
            Assert.AreEqual(false, "A\0C" == fixedString);
            Assert.AreEqual(true, "A\0C" != fixedString);
            Assert.AreEqual(true, "A\0BB" == fixedString);
        }

        [Test]
        public void FixedStringN_EqualityWithString_MalformedUtf8IsNotEqualToReplacementCharacter()
        {
            var fixedString = new FixedString32Bytes("x");
            fixedString[0] = 0xFF;

            Assert.AreEqual(false, fixedString == "\uFFFD");
            Assert.AreEqual(true, fixedString != "\uFFFD");
            Assert.AreEqual(false, "\uFFFD" == fixedString);
            Assert.AreEqual(true, "\uFFFD" != fixedString);
        }

        [Test]
        public void FixedStringNSizeOf()
        {
            Assert.AreEqual(UnsafeUtility.SizeOf<FixedStringN>(), 128);
        }

        [TestCase("red", new byte[] { 3, 0, 114, 101, 100, 0 }, TestName = "{m}(red)")]
        [TestCase("紅色", new byte[] { 6, 0, 231, 180, 133, 232, 137, 178, 0 }, TestName = "{m}(Chinese-Red)")]
        [TestCase("црвена", new byte[] { 12, 0, 209, 134, 209, 128, 208, 178, 208, 181, 208, 189, 208, 176, 0 }, TestName = "{m}(Serbian-Red)")]
        [TestCase("George Washington", new byte[] { 17, 0, 71, 101, 111, 114, 103, 101, 32, 87, 97, 115, 104, 105, 110, 103, 116, 111, 110, 0 }, TestName = "{m}(George Washington)")]
        [TestCase("村上春樹", new byte[] { 12, 0, 230, 157, 145, 228, 184, 138, 230, 152, 165, 230, 168, 185, 0 }, TestName = "{m}(HarukiMurakami)")]
        [TestCase("🌕🌖🌗🌘🌑🌒🌓🌔", new byte[] { 32, 0, 240, 159, 140, 149, 240, 159, 140, 150, 240, 159, 140, 151, 240, 159, 140, 152, 240, 159, 140, 145, 240, 159, 140, 146, 240, 159, 140, 147, 240, 159, 140, 148, 0 }, TestName = "{m}(MoonPhases)")]
        [TestCase("𝒞𝒯𝒮𝒟𝒳𝒩𝒫𝒢", new byte[] { 32, 0, 240, 157, 146, 158, 240, 157, 146, 175, 240, 157, 146, 174, 240, 157, 146, 159, 240, 157, 146, 179, 240, 157, 146, 169, 240, 157, 146, 171, 240, 157, 146, 162, 0 }, TestName = "{m}(Cursive)")]
        [TestCase("로마는 하루아침에 이루어진 것이 아니다", new byte[] { 55, 0, 235, 161, 156, 235, 167, 136, 235, 138, 148, 32, 237, 149, 152, 235, 163, 168, 236, 149, 132, 236, 185, 168, 236, 151, 144, 32, 236, 157, 180, 235, 163, 168, 236, 150, 180, 236, 167, 132, 32, 234, 178, 131, 236, 157, 180, 32, 236, 149, 132, 235, 139, 136, 235, 139, 164, 0 }, TestName = "{m}(Korean - Rome was not made overnight)")]
        [TestCase("Лако ти је плитку воду замутити и будалу наљутити", new byte[] { 90, 0, 208, 155, 208, 176, 208, 186, 208, 190, 32, 209, 130, 208, 184, 32, 209, 152, 208, 181, 32, 208, 191, 208, 187, 208, 184, 209, 130, 208, 186, 209, 131, 32, 208, 178, 208, 190, 208, 180, 209, 131, 32, 208, 183, 208, 176, 208, 188, 209, 131, 209, 130, 208, 184, 209, 130, 208, 184, 32, 208, 184, 32, 208, 177, 209, 131, 208, 180, 208, 176, 208, 187, 209, 131, 32, 208, 189, 208, 176, 209, 153, 209, 131, 209, 130, 208, 184, 209, 130, 208, 184, 0 }, TestName = "{m}(Serbian-Proverb)")]
        [TestCase("Үнэн үг хэлсэн хүнд ноёд өстэй, үхэр унасан хүнд ноход өстэй.", new byte[] { 110, 0, 210, 174, 208, 189, 209, 141, 208, 189, 32, 210, 175, 208, 179, 32, 209, 133, 209, 141, 208, 187, 209, 129, 209, 141, 208, 189, 32, 209, 133, 210, 175, 208, 189, 208, 180, 32, 208, 189, 208, 190, 209, 145, 208, 180, 32, 211, 169, 209, 129, 209, 130, 209, 141, 208, 185, 44, 32, 210, 175, 209, 133, 209, 141, 209, 128, 32, 209, 131, 208, 189, 208, 176, 209, 129, 208, 176, 208, 189, 32, 209, 133, 210, 175, 208, 189, 208, 180, 32, 208, 189, 208, 190, 209, 133, 208, 190, 208, 180, 32, 211, 169, 209, 129, 209, 130, 209, 141, 208, 185, 46, 0 }, TestName = "{m}(Mongolian-Proverb1)")]
        unsafe public void FixedStringNLayout(String a, byte[] expected)
        {
            fixed (byte* expectedBytes = expected)
            {
                FixedStringN actual = a;
                byte* actualBytes = (byte*)&actual;
                Assert.AreEqual(0, UnsafeUtility.MemCmp(expectedBytes, actualBytes, expected.Length));
            }
        }

        [TestCase("red", 'r', 'd')]
        [TestCase("紅色", '紅', '色')]
        [TestCase("црвена", 'ц', 'а')]
        [TestCase("George Washington", 'G', 'n')]
        [TestCase("村上春樹", '村', '樹')]
        [TestCase("로마는 하루아침에 이루어진 것이 아니다", '로', '다')]
        [TestCase("Лако ти је плитку воду замутити и будалу наљутити", 'Л', 'и')]
        [TestCase("Үнэн үг хэлсэн хүнд ноёд өстэй, үхэр унасан хүнд ноход өстэй.", 'Ү', '.')]
        public void FixedStringStartsEndsWithChar(String a, char starts, char ends)
        {
            FixedStringN actual = a;
            Assert.True(actual.StartsWith(starts));
            Assert.True(actual.EndsWith(ends));
        }

        [TestCase("red", "r", "d")]
        [TestCase("紅色", "紅", "色")]
        [TestCase("црвена", "црв", "ена")]
        [TestCase("George Washington", "George", "Washington")]
        [TestCase("村上春樹", "村上", "春樹")]
        [TestCase("🌕🌖🌗🌘🌑🌒🌓🌔", "🌕🌖🌗", "🌒🌓🌔")]
        [TestCase("𝒞𝒯𝒮𝒟𝒳𝒩𝒫𝒢", "𝒞𝒯𝒮", "𝒩𝒫𝒢")]
        [TestCase("로마는 하루아침에 이루어진 것이 아니다", "로마는", "아니다")]
        [TestCase("Лако ти је плитку воду замутити и будалу наљутити", "Лако", "наљутити")]
        [TestCase("Үнэн үг хэлсэн хүнд ноёд өстэй, үхэр унасан хүнд ноход өстэй.", "Үнэн", "өстэй.")]
        public void FixedStringStartsEndsWithString(String a, String starts, String ends)
        {
            FixedStringN actual = a;
            Assert.True(actual.StartsWith((FixedStringN)starts));
            Assert.True(actual.EndsWith((FixedStringN)ends));
        }

        [TestCase("red  ", ' ', "red  ", "red", "red")]
        [TestCase("  red  ", ' ', "red  ", "  red", "red")]
        [TestCase("       ", ' ', "", "", "")]
        public void FixedStringTrimStart(String a, char trim, String expectedStart, String expectedEnd, String expected)
        {
            FixedStringN actual = a;
            Assert.AreEqual(expectedStart, actual.TrimStart());
            Assert.AreEqual(expectedEnd, actual.TrimEnd());
            Assert.AreEqual(expected, actual.Trim());
        }

        [TestCase("  red  ", "ed  ", "  red", "ed")]
        [TestCase("црвена", "црвена", "црвена", "црвена")]
        [TestCase("       ", "", "", "")]
        public void FixedStringTrimStartWithRunes(String a, String expectedStart, String expectedEnd, String expected)
        {
            FixedStringN actual = a;
            Assert.AreEqual(expectedStart, actual.TrimStart(new Unicode.Rune[]{ ' ', 'r'}));
            Assert.AreEqual(expectedEnd, actual.TrimEnd(new Unicode.Rune[] { ' ', 'r' }));
            Assert.AreEqual(expected, actual.Trim(new Unicode.Rune[] { ' ', 'r' }));
        }

        [TestCase("Red", "red", "RED")]
        [TestCase("црвена", "црвена", "црвена")]
        [TestCase("       ", "       ", "       ")]
        public void FixedStringToLowerUpperAscii(String a, String expectedLower, String expectedUpped)
        {
            FixedStringN actual = a;
            Assert.AreEqual(expectedLower, actual.ToLowerAscii());
            Assert.AreEqual(expectedUpped, actual.ToUpperAscii());
        }
    }
}
