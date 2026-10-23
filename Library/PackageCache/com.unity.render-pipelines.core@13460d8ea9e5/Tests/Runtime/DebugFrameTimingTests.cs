using NUnit.Framework;

namespace UnityEngine.Rendering.Tests
{
    static class FrameTimingTestExtensions
    {
        public static BottleneckHistogram ToHistogram(this FrameTimeSample s)
        {
            var history = new BottleneckHistory(1);
            history.AddBottleneckFromAveragedSample(s);
            history.ComputeHistogram();
            return history.Histogram;
        }

        public static string ToDebugString(this BottleneckHistogram h) =>
            $"GPU={h.GPU}, CPU={h.CPU}, PresentLimited={h.PresentLimited}, Balanced={h.Balanced}";
    }

    class DebugFrameTimingTests
    {
        static FrameTimeSample MakeSample(
            float fullFrameTime,
            float gpu,
            float mainCpu,
            float renderCpu,
            float presentWait = 0f,
            float fps = 0f)
        {
            return new FrameTimeSample
            {
                FullFrameTime = fullFrameTime,
                GPUFrameTime = gpu,
                MainThreadCPUFrameTime = mainCpu,
                RenderThreadCPUFrameTime = renderCpu,
                MainThreadCPUPresentWaitTime = presentWait,
                FramesPerSecond = fps
            };
        }

        [Test]
        public void Histogram_ZeroGPUFrameTime_ProducesIndeterminate()
        {
            var h = MakeSample(fullFrameTime: 10, gpu: 0, mainCpu: 5, renderCpu: 5).ToHistogram();
            Assert.That(h.GPU + h.CPU + h.PresentLimited + h.Balanced, Is.EqualTo(0f),
                $"Zero GPUFrameTime must classify as Indeterminate (all buckets 0). Got {h.ToDebugString()}.");
        }

        [Test]
        public void Histogram_ZeroMainThreadCPUFrameTime_ProducesIndeterminate()
        {
            var h = MakeSample(fullFrameTime: 10, gpu: 5, mainCpu: 0, renderCpu: 5).ToHistogram();
            Assert.That(h.GPU + h.CPU + h.PresentLimited + h.Balanced, Is.EqualTo(0f),
                $"Zero MainThreadCPUFrameTime must classify as Indeterminate (all buckets 0). Got {h.ToDebugString()}.");
        }

        [Test]
        public void Histogram_GPUBound_ProducesGPU()
        {
            // GPU close to FullFrameTime, CPU times are not.
            var h = MakeSample(fullFrameTime: 10, gpu: 9, mainCpu: 2, renderCpu: 2).ToHistogram();
            Assert.That(h.GPU, Is.EqualTo(1f),
                $"GPU-dominant sample must put 100% in the GPU bucket. Got {h.ToDebugString()}.");
        }

        [Test]
        public void Histogram_MainThreadCPUBound_ProducesCPU()
        {
            var h = MakeSample(fullFrameTime: 10, gpu: 2, mainCpu: 9, renderCpu: 2).ToHistogram();
            Assert.That(h.CPU, Is.EqualTo(1f),
                $"MainThread-dominant sample must put 100% in the CPU bucket. Got {h.ToDebugString()}.");
        }

        [Test]
        public void Histogram_RenderThreadCPUBound_ProducesCPU()
        {
            var h = MakeSample(fullFrameTime: 10, gpu: 2, mainCpu: 2, renderCpu: 9).ToHistogram();
            Assert.That(h.CPU, Is.EqualTo(1f),
                $"RenderThread-dominant sample must put 100% in the CPU bucket. Got {h.ToDebugString()}.");
        }

        [Test]
        public void Histogram_BalancedWithPresentWait_ProducesPresentLimited()
        {
            var h = MakeSample(fullFrameTime: 10, gpu: 2, mainCpu: 2, renderCpu: 2, presentWait: 1f).ToHistogram();
            Assert.That(h.PresentLimited, Is.EqualTo(1f),
                $"Non-dominant sample with present wait > 0.5ms must classify as PresentLimited. Got {h.ToDebugString()}.");
        }

        [Test]
        public void Histogram_BalancedWithoutPresentWait_ProducesBalanced()
        {
            var h = MakeSample(fullFrameTime: 10, gpu: 5, mainCpu: 5, renderCpu: 5, presentWait: 0f).ToHistogram();
            Assert.That(h.Balanced, Is.EqualTo(1f),
                $"Non-dominant sample with no present wait must classify as Balanced. Got {h.ToDebugString()}.");
        }

        [Test]
        public void Histogram_MixedSamples_RatiosMatchSampleCounts()
        {
            // 2x GPU, 2x CPU, 1x PresentLimited -> 0.4 / 0.4 / 0.2 / 0
            var history = new BottleneckHistory(8);
            history.AddBottleneckFromAveragedSample(MakeSample(10, 9, 2, 2));
            history.AddBottleneckFromAveragedSample(MakeSample(10, 9, 2, 2));
            history.AddBottleneckFromAveragedSample(MakeSample(10, 2, 9, 2));
            history.AddBottleneckFromAveragedSample(MakeSample(10, 2, 9, 2));
            history.AddBottleneckFromAveragedSample(MakeSample(10, 2, 2, 2, presentWait: 1f));
            history.ComputeHistogram();
            var h = history.Histogram;

            Assert.That(h.GPU, Is.EqualTo(0.4f).Within(1e-6f), $"GPU bucket. Got {h.ToDebugString()}.");
            Assert.That(h.CPU, Is.EqualTo(0.4f).Within(1e-6f), $"CPU bucket. Got {h.ToDebugString()}.");
            Assert.That(h.PresentLimited, Is.EqualTo(0.2f).Within(1e-6f), $"PresentLimited bucket. Got {h.ToDebugString()}.");
            Assert.That(h.Balanced, Is.EqualTo(0f), $"Balanced bucket. Got {h.ToDebugString()}.");
        }

        [Test]
        public void Histogram_IndeterminateSamples_ReduceVisibleTotalBelowOne()
        {
            // 1 valid GPU sample + 4 indeterminate (GPU=0) -> visible total = 0.2.
            var history = new BottleneckHistory(8);
            history.AddBottleneckFromAveragedSample(MakeSample(10, 9, 2, 2));
            for (int i = 0; i < 4; i++)
                history.AddBottleneckFromAveragedSample(MakeSample(10, 0, 5, 5));
            history.ComputeHistogram();
            var h = history.Histogram;

            Assert.That(h.GPU, Is.EqualTo(0.2f).Within(1e-6f), $"GPU bucket. Got {h.ToDebugString()}.");
            Assert.That(h.GPU + h.CPU + h.PresentLimited + h.Balanced, Is.EqualTo(0.2f).Within(1e-6f),
                $"Indeterminate samples must be excluded from visible buckets. Got {h.ToDebugString()}.");
        }

        [Test]
        public void BottleneckHistory_DiscardOldSamples_KeepsAtMostHistorySize()
        {
            // With historySize=3, add 5 GPU-bound then 2 CPU-bound samples.
            // Retained samples should be [GPU, CPU, CPU] -> GPU=1/3, CPU=2/3.
            const int historySize = 3;
            var history = new BottleneckHistory(historySize);
            var gpuSample = MakeSample(10, 9, 2, 2);
            var cpuSample = MakeSample(10, 2, 9, 2);

            for (int i = 0; i < 5; i++)
            {
                history.DiscardOldSamples(historySize);
                history.AddBottleneckFromAveragedSample(gpuSample);
            }
            for (int i = 0; i < 2; i++)
            {
                history.DiscardOldSamples(historySize);
                history.AddBottleneckFromAveragedSample(cpuSample);
            }
            history.ComputeHistogram();
            var h = history.Histogram;

            Assert.That(h.GPU, Is.EqualTo(1f / 3f).Within(1e-6f), $"Only 3 newest samples should be retained. Got {h.ToDebugString()}.");
            Assert.That(h.CPU, Is.EqualTo(2f / 3f).Within(1e-6f), $"Only 3 newest samples should be retained. Got {h.ToDebugString()}.");
        }

        [Test]
        public void Reset_ClearsBottleneckHistogram()
        {
            var debug = new DebugFrameTiming();
            debug.m_BottleneckHistory.AddBottleneckFromAveragedSample(MakeSample(10, 9, 2, 2));
            debug.m_BottleneckHistory.ComputeHistogram();

            debug.Reset();

            var h = debug.m_BottleneckHistory.Histogram;
            Assert.That(h.GPU + h.CPU + h.PresentLimited + h.Balanced, Is.EqualTo(0f), $"Reset must zero the histogram. Got {h.ToDebugString()}.");
        }
    }
}
