using System;
using NUnit.Framework;
using Unity.Profiling;

namespace GoLive.Tests
{
    public sealed class ViewerAllocationDiagnosticsTests
    {
        [Test]
        public void NativeProfilerCalibratesAKnownManagedAllocation()
        {
            using var recorder = new ProfilerRecorder(ProfilerCategory.Memory, "GC.Alloc", 1,
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame |
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            Assert.That(recorder.Valid, Is.True);
            long before = GC.GetAllocatedBytesForCurrentThread();
            recorder.Start();
            var retained = new byte[65536];
            retained[0] = 1;
            recorder.Stop();
            long managedDelta = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(retained);
            Assert.That(recorder.Count, Is.GreaterThan(0));
            var sample = recorder.GetSample(0);
            TestContext.WriteLine($"Known allocation payload=65536; managed API delta={managedDelta}; native unit={recorder.UnitType}; value={sample.Value}; allocation count={sample.Count}");
            Assert.That(sample.Count, Is.GreaterThan(0), "Native profiler must see the calibration allocation.");
        }

        [Test]
        public void NativeProfilerCountsOnlyAllocationsInsideEachResetScope()
        {
            using var recorder = new ProfilerRecorder(ProfilerCategory.Memory, "GC.Alloc", 1,
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame |
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            Assert.That(recorder.Valid, Is.True);
            // Warm up native start/stop and array allocation before asserting exact scope counts.
            recorder.Start(); var warmup = new byte[16]; recorder.Stop(); GC.KeepAlive(warmup);
            recorder.Reset();
            var outside = new byte[128];
            recorder.Start();
            var first = new byte[256]; var second = new byte[512];
            recorder.Stop();
            Assert.That(recorder.GetSample(0).Count, Is.EqualTo(2));
            recorder.Reset(); recorder.Start(); recorder.Stop();
            Assert.That(recorder.Count == 0 ? 0 : recorder.GetSample(0).Count, Is.Zero,
                "The next idle scope must not retain previous allocations or include allocations outside the scope.");
            GC.KeepAlive(outside); GC.KeepAlive(first); GC.KeepAlive(second);
        }
    }
}
