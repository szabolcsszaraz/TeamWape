using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Tests
{
    class BufferedRTHandleSystemTests
    {
        [Test, NUnit.Framework.Property("Jira", "UUM-139463")]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void BuffersSwapIntoCorrectPositions(int bufferCount)
        {
            const int BufferId = 10;

            RTHandle Allocator(RTHandleSystem rtHandleSystem, int frameIndex)
            {
                return rtHandleSystem.Alloc(
                    Vector2.one, 1,
                    colorFormat: GraphicsFormat.R8G8B8A8_UNorm,
                    dimension: TextureDimension.Tex2D,
                    name: $"Frame Buffer {frameIndex}");
            }

            using var rtHandleSys = new Rendering.BufferedRTHandleSystem();

            // Allocate buffers
            rtHandleSys.AllocBuffer(BufferId, Allocator, bufferCount);

            // Record order of buffers before swap
            var preBufferOrder = new RTHandle[bufferCount];
            for (var i = 0; i < bufferCount; ++i)
            {
                preBufferOrder[i] = rtHandleSys.GetFrameRT(BufferId, i);
            }

            // Perform swap
            rtHandleSys.SwapAndSetReferenceSize(256, 256);

            // Record order of buffers after swap
            var postBufferOrder = new RTHandle[bufferCount];
            for (var i = 0; i < bufferCount; ++i)
            {
                postBufferOrder[i] = rtHandleSys.GetFrameRT(BufferId, i);
            }

            // Compare before/after buffer orders
            for (var i = 0; i < bufferCount - 1; ++i)
            {
                // First length-1 elements
                Assert.AreEqual(preBufferOrder[i], postBufferOrder[i + 1]);
            }
            // Last element
            Assert.AreEqual(preBufferOrder[bufferCount - 1], postBufferOrder[0]);
        }
    }
}
