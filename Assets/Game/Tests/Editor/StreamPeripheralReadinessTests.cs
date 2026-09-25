using System;
using System.Collections.Generic;
using GoLive.Desktop;
using GoLive.PcBuilding;
using NUnit.Framework;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    public sealed class StreamPeripheralReadinessTests
    {
        private readonly List<Object> _created = new();
        private PcCapabilities _desktop;
        private PcCapabilities _gaming;

        [SetUp]
        public void SetUp()
        {
            _desktop = StreamSessionTests.CreateCapabilities(_created, false);
            _gaming = StreamSessionTests.CreateCapabilities(_created, true);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _created) Object.DestroyImmediate(item);
            _created.Clear();
        }

        [Test]
        public void BothConnectedDevicesAndSupportedHighQualityMakeEveryCheckReady()
        {
            var devices = ConnectedDevices(true);
            var stream = ConnectedStream(devices);
            Assert.That(stream.SetQuality(StreamQuality.High), Is.Null);

            StreamReadiness result = stream.EvaluateReadiness(_gaming, true, 6);

            Assert.That(result.ChannelReady, Is.True);
            Assert.That(result.InternetReady, Is.True);
            Assert.That(result.MicrophoneReady, Is.True);
            Assert.That(result.WebcamReady, Is.True);
            Assert.That(result.QualitySupported, Is.True);
            Assert.That(result.ErrorKey, Is.Null);
            Assert.That(result.CanStart, Is.True);
            Assert.That(stream.CheckStart(_gaming, true, 6), Is.Null);
            Assert.That(stream.Start(_gaming, true, 6), Is.Null);
            Assert.That(stream.State, Is.EqualTo(StreamState.Starting));
        }

        [Test]
        public void ReadinessReportsAllMissingRequirementsEvenWhenChannelFailsFirst()
        {
            var stream = new StreamSession(DesktopAccountTests.RegisteredChannel(), new DonationAccount(), new PcPeripherals());

            StreamReadiness result = stream.EvaluateReadiness(default, true, 0);

            Assert.That(result.ChannelReady, Is.False);
            Assert.That(result.InternetReady, Is.False);
            Assert.That(result.MicrophoneReady, Is.False);
            Assert.That(result.WebcamReady, Is.False);
            Assert.That(result.QualitySupported, Is.False);
            Assert.That(result.ErrorKey, Is.EqualTo("desktop.stream.not_connected"));
            Assert.That(result.CanStart, Is.False);
        }

        [Test]
        public void MissingMicrophoneCannotEnterStartingOrEmitAStateChange()
        {
            var devices = new PcPeripherals();
            Assert.That(devices.TryConnect(PcPeripheralKind.Webcam, "camera"), Is.True);
            var stream = ConnectedStream(devices);
            int changes = 0;
            stream.Changed += () => changes++;

            StreamReadiness result = stream.EvaluateReadiness(_desktop, true, 5);

            Assert.That(result.ChannelReady, Is.True);
            Assert.That(result.InternetReady, Is.True);
            Assert.That(result.MicrophoneReady, Is.False);
            Assert.That(result.WebcamReady, Is.True, "a webcam cannot substitute for a microphone");
            Assert.That(result.QualitySupported, Is.True);
            Assert.That(result.ErrorKey, Is.EqualTo("desktop.stream.microphone_missing"));
            Assert.That(result.CanStart, Is.False);
            Assert.That(stream.CheckStart(_desktop, true, 5), Is.EqualTo(result.ErrorKey));
            Assert.That(stream.Start(_desktop, true, 5), Is.EqualTo(result.ErrorKey));
            stream.Tick(1);
            Assert.That(stream.State, Is.EqualTo(StreamState.Offline));
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void MissingOptionalWebcamAllowsANormalBroadcast()
        {
            var stream = ConnectedStream(ConnectedDevices(false));

            StreamReadiness result = stream.EvaluateReadiness(_desktop, true, 5);

            Assert.That(result.MicrophoneReady, Is.True);
            Assert.That(result.WebcamReady, Is.False);
            Assert.That(result.CanStart, Is.True);
            Assert.That(stream.Start(_desktop, true, 5), Is.Null);
            stream.Tick(.75f);
            Assert.That(stream.State, Is.EqualTo(StreamState.Live));
            Assert.That(stream.EvaluateReadiness(_desktop, true, 5).WebcamReady, Is.False);
        }

        [Test]
        public void RegisteredButUnlinkedChannelPreventsStartingWithReadyEquipment()
        {
            var stream = new StreamSession(DesktopAccountTests.RegisteredChannel(), new DonationAccount(), ConnectedDevices(true));

            StreamReadiness result = stream.EvaluateReadiness(_desktop, true, 5);

            Assert.That(result.ChannelReady, Is.False);
            Assert.That(result.InternetReady, Is.True);
            Assert.That(result.MicrophoneReady, Is.True);
            Assert.That(result.WebcamReady, Is.True);
            Assert.That(result.QualitySupported, Is.True);
            Assert.That(result.ErrorKey, Is.EqualTo("desktop.stream.not_connected"));
            Assert.That(result.CanStart, Is.False);
            Assert.That(stream.CheckStart(_desktop, true, 5), Is.EqualTo(result.ErrorKey));
            Assert.That(stream.Start(_desktop, true, 5), Is.EqualTo(result.ErrorKey));
            Assert.That(stream.State, Is.EqualTo(StreamState.Offline));
        }

        [TestCase(StreamQuality.Low, 0f, false, "desktop.stream.internet_missing")]
        [TestCase(StreamQuality.Low, .99f, false, "desktop.stream.upload_low")]
        [TestCase(StreamQuality.Low, 1f, true, null)]
        [TestCase(StreamQuality.Medium, 2.99f, false, "desktop.stream.upload_low")]
        [TestCase(StreamQuality.Medium, 3f, true, null)]
        [TestCase(StreamQuality.High, 5.99f, false, "desktop.stream.upload_low")]
        [TestCase(StreamQuality.High, 6f, true, null)]
        [TestCase(StreamQuality.Low, float.NaN, false, "desktop.stream.internet_missing")]
        [TestCase(StreamQuality.Low, float.PositiveInfinity, false, "desktop.stream.internet_missing")]
        [TestCase(StreamQuality.Low, -1f, false, "desktop.stream.internet_missing")]
        public void InternetReadinessAndStartShareSelectedQualityBoundaries(StreamQuality quality,
            float upload, bool internetReady, string error)
        {
            var stream = ConnectedStream(ConnectedDevices(false));
            Assert.That(stream.SetQuality(quality), Is.Null);

            StreamReadiness result = stream.EvaluateReadiness(_gaming, true, upload);

            Assert.That(result.InternetReady, Is.EqualTo(internetReady));
            Assert.That(result.QualitySupported, Is.True, "hardware readiness is independent of upload speed");
            Assert.That(result.ErrorKey, Is.EqualTo(error));
            Assert.That(result.CanStart, Is.EqualTo(internetReady));
            Assert.That(stream.CheckStart(_gaming, true, upload), Is.EqualTo(error));
            Assert.That(stream.Start(_gaming, true, upload), Is.EqualTo(error));
            Assert.That(stream.State, Is.EqualTo(internetReady ? StreamState.Starting : StreamState.Offline));
        }

        [Test]
        public void HighQualityWithoutGraphicsCardFailsHardwareWithInternetStillReady()
        {
            var stream = ConnectedStream(ConnectedDevices(false));
            Assert.That(stream.SetQuality(StreamQuality.High), Is.Null);

            StreamReadiness result = stream.EvaluateReadiness(_desktop, true, 6);

            Assert.That(result.InternetReady, Is.True);
            Assert.That(result.QualitySupported, Is.False);
            Assert.That(result.ErrorKey, Is.EqualTo("desktop.stream.gpu_required"));
            Assert.That(result.CanStart, Is.False);
            Assert.That(stream.CheckStart(_desktop, true, 6), Is.EqualTo(result.ErrorKey));
            Assert.That(stream.Start(_desktop, true, 6), Is.EqualTo(result.ErrorKey));
            Assert.That(stream.State, Is.EqualTo(StreamState.Offline));
        }

        [Test]
        public void RemovingLiveMicrophoneAbortsExactlyOnceIncludingReentrantRefresh()
        {
            var devices = ConnectedDevices(true);
            var stream = ConnectedStream(devices);
            var summaries = new List<StreamSummary>();
            stream.Completed += summary =>
            {
                summaries.Add(summary);
                stream.RefreshEnvironment(_desktop, true, 5);
            };
            Assert.That(stream.Start(_desktop, true, 5), Is.Null);
            stream.Tick(10.75f);
            Assert.That(stream.State, Is.EqualTo(StreamState.Live));

            Assert.That(devices.TryDisconnect(PcPeripheralKind.Microphone), Is.True);
            stream.RefreshEnvironment(_desktop, true, 5);
            stream.RefreshEnvironment(_desktop, true, 5);
            stream.Tick(10);

            Assert.That(stream.State, Is.EqualTo(StreamState.Offline));
            Assert.That(summaries.Count, Is.EqualTo(1));
            Assert.That(summaries[0].Aborted, Is.True);
            Assert.That(summaries[0].DurationSeconds, Is.EqualTo(10));
            Assert.That(stream.CheckStart(_desktop, true, 5), Is.EqualTo("desktop.stream.microphone_missing"));
        }

        [Test]
        public void RemovingMicrophoneDuringStartingCancelsWithoutPublishingASummary()
        {
            var devices = ConnectedDevices(false);
            var stream = ConnectedStream(devices);
            int completions = 0;
            stream.Completed += _ => completions++;
            Assert.That(stream.Start(_desktop, true, 5), Is.Null);
            stream.Tick(.5f);

            Assert.That(devices.TryDisconnect(PcPeripheralKind.Microphone), Is.True);
            stream.RefreshEnvironment(_desktop, true, 5);
            stream.Tick(1);

            Assert.That(stream.State, Is.EqualTo(StreamState.Offline));
            Assert.That(completions, Is.Zero);
        }

        [Test]
        public void RemovingLiveWebcamKeepsBroadcastRunningAndRefreshesObservers()
        {
            var devices = ConnectedDevices(true);
            var stream = ConnectedStream(devices);
            int completions = 0;
            stream.Completed += _ => completions++;
            Assert.That(stream.Start(_desktop, true, 5), Is.Null);
            stream.Tick(10.75f);
            int changes = 0;
            stream.Changed += () => changes++;

            Assert.That(devices.TryDisconnect(PcPeripheralKind.Webcam), Is.True);
            stream.RefreshEnvironment(_desktop, true, 5);

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(stream.State, Is.EqualTo(StreamState.Live));
            Assert.That(stream.EvaluateReadiness(_desktop, true, 5).WebcamReady, Is.False);
            stream.Tick(2);
            Assert.That(stream.DurationSeconds, Is.EqualTo(12));
            Assert.That(completions, Is.Zero);
        }

        [Test]
        public void OfflineEnvironmentRefreshPublishesTheUpdatedReadiness()
        {
            var devices = ConnectedDevices(false);
            var stream = ConnectedStream(devices);
            int changes = 0;
            StreamReadiness observed = default;
            stream.Changed += () =>
            {
                changes++;
                observed = stream.EvaluateReadiness(_desktop, true, 0);
            };

            Assert.That(devices.TryDisconnect(PcPeripheralKind.Microphone), Is.True);
            stream.RefreshEnvironment(_desktop, true, 0);

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(observed.MicrophoneReady, Is.False);
            Assert.That(observed.InternetReady, Is.False);
            Assert.That(observed.CanStart, Is.False);
            Assert.That(stream.State, Is.EqualTo(StreamState.Offline));
        }

        [Test]
        public void DeviceIdsCannotOccupyTwoConnectionsOrReplaceAnOccupiedSocket()
        {
            var devices = new PcPeripherals();
            int changes = 0;
            devices.Changed += () => changes++;
            Assert.That(devices.TryConnect(PcPeripheralKind.Microphone, "shared"), Is.True);

            Assert.That(devices.CanConnect(PcPeripheralKind.Webcam, "shared"), Is.False);
            Assert.That(devices.TryConnect(PcPeripheralKind.Webcam, "shared"), Is.False);
            Assert.That(devices.TryConnect(PcPeripheralKind.Microphone, "replacement"), Is.False);
            Assert.That(devices.GetConnectedId(PcPeripheralKind.Microphone), Is.EqualTo("shared"));
            Assert.That(devices.HasWebcam, Is.False);
            Assert.That(changes, Is.EqualTo(1));

            Assert.That(devices.TryConnect(PcPeripheralKind.Webcam, "camera"), Is.True);
            Assert.That(devices.TryDisconnect(PcPeripheralKind.Microphone), Is.True);
            Assert.That(devices.TryDisconnect(PcPeripheralKind.Microphone), Is.False);
            Assert.That(devices.HasMicrophone, Is.False);
            Assert.That(devices.GetConnectedId(PcPeripheralKind.Webcam), Is.EqualTo("camera"));
            Assert.That(changes, Is.EqualTo(3), "only successful ownership changes notify observers");
        }

        [TestCase(PcPeripheralKind.None, "device")]
        [TestCase((PcPeripheralKind)900, "device")]
        [TestCase(PcPeripheralKind.Microphone, null)]
        [TestCase(PcPeripheralKind.Microphone, "")]
        [TestCase(PcPeripheralKind.Webcam, "   ")]
        public void InvalidConnectionAttemptsDoNotChangeOwnershipOrEmitEvents(PcPeripheralKind kind, string id)
        {
            var devices = new PcPeripherals();
            int changes = 0;
            devices.Changed += () => changes++;

            Assert.That(devices.CanConnect(kind, id), Is.False);
            Assert.That(devices.TryConnect(kind, id), Is.False);
            Assert.That(devices.TryDisconnect(kind), Is.False);
            Assert.That(devices.HasMicrophone, Is.False);
            Assert.That(devices.HasWebcam, Is.False);
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void ValidSnapshotRestoresBothIdentitiesAtomicallyAndRepeatedRestoreIsSilent()
        {
            var devices = new PcPeripherals();
            var snapshot = new PcPeripheralsSnapshot { MicrophoneId = "saved-mic", WebcamId = "saved-camera" };
            var installed = new Dictionary<string, PcPeripheralKind>
            {
                { "saved-mic", PcPeripheralKind.Microphone },
                { "saved-camera", PcPeripheralKind.Webcam }
            };
            int changes = 0;
            devices.Changed += () =>
            {
                changes++;
                Assert.That(devices.GetConnectedId(PcPeripheralKind.Microphone), Is.EqualTo("saved-mic"));
                Assert.That(devices.GetConnectedId(PcPeripheralKind.Webcam), Is.EqualTo("saved-camera"));
            };

            Assert.That(devices.Validate(snapshot, installed), Is.Null);
            devices.Restore(snapshot, installed);
            devices.Restore(snapshot, installed);

            Assert.That(changes, Is.EqualTo(1));
            PcPeripheralsSnapshot capture = devices.Capture();
            Assert.That(capture.MicrophoneId, Is.EqualTo("saved-mic"));
            Assert.That(capture.WebcamId, Is.EqualTo("saved-camera"));
            capture.MicrophoneId = "changed-copy";
            snapshot.WebcamId = "changed-source";
            Assert.That(devices.GetConnectedId(PcPeripheralKind.Microphone), Is.EqualTo("saved-mic"));
            Assert.That(devices.GetConnectedId(PcPeripheralKind.Webcam), Is.EqualTo("saved-camera"));
        }

        [Test]
        public void EmptySnapshotClearsConnectionsOnlyWhenInstalledGraphIsAlsoEmpty()
        {
            var devices = ConnectedDevices(true);
            var empty = new PcPeripheralsSnapshot();
            var installed = new Dictionary<string, PcPeripheralKind>();
            int changes = 0;
            devices.Changed += () => changes++;

            Assert.That(devices.Validate(empty, installed), Is.Null);
            devices.Restore(empty, installed);
            devices.Restore(empty, installed);

            Assert.That(devices.HasMicrophone, Is.False);
            Assert.That(devices.HasWebcam, Is.False);
            Assert.That(changes, Is.EqualTo(1));
        }

        [TestCaseSource(nameof(InvalidSnapshots))]
        public void InvalidSnapshotsRejectTheWholeRestoreWithoutMutatingEitherConnection(
            PcPeripheralsSnapshot snapshot, IReadOnlyDictionary<string, PcPeripheralKind> installed)
        {
            var devices = ConnectedDevices(true);
            int changes = 0;
            devices.Changed += () => changes++;

            Assert.That(devices.Validate(snapshot, installed), Is.Not.Null);
            Assert.Throws<ArgumentException>(() => devices.Restore(snapshot, installed));

            Assert.That(devices.GetConnectedId(PcPeripheralKind.Microphone), Is.EqualTo("microphone"));
            Assert.That(devices.GetConnectedId(PcPeripheralKind.Webcam), Is.EqualTo("webcam"));
            Assert.That(changes, Is.Zero);
        }

        private static IEnumerable<TestCaseData> InvalidSnapshots()
        {
            var empty = new Dictionary<string, PcPeripheralKind>();
            yield return new TestCaseData(null, empty).SetName("PeripheralRestore_NullSnapshot");
            yield return new TestCaseData(new PcPeripheralsSnapshot(), null).SetName("PeripheralRestore_NullInstalledGraph");
            yield return new TestCaseData(new PcPeripheralsSnapshot { MicrophoneId = null }, empty).SetName("PeripheralRestore_NullMicrophoneId");
            yield return new TestCaseData(new PcPeripheralsSnapshot { WebcamId = null }, empty).SetName("PeripheralRestore_NullWebcamId");
            yield return new TestCaseData(new PcPeripheralsSnapshot { MicrophoneId = " " }, empty).SetName("PeripheralRestore_WhitespaceIdentity");
            yield return new TestCaseData(new PcPeripheralsSnapshot { MicrophoneId = "missing" }, empty).SetName("PeripheralRestore_MissingPhysicalItem");
            yield return new TestCaseData(new PcPeripheralsSnapshot { MicrophoneId = "camera" },
                new Dictionary<string, PcPeripheralKind> { { "camera", PcPeripheralKind.Webcam } }).SetName("PeripheralRestore_WebcamInMicrophoneSocket");
            yield return new TestCaseData(new PcPeripheralsSnapshot { WebcamId = "mic" },
                new Dictionary<string, PcPeripheralKind> { { "mic", PcPeripheralKind.Microphone } }).SetName("PeripheralRestore_MicrophoneInWebcamSocket");
            yield return new TestCaseData(new PcPeripheralsSnapshot { MicrophoneId = "shared", WebcamId = "shared" },
                new Dictionary<string, PcPeripheralKind> { { "shared", PcPeripheralKind.Microphone } }).SetName("PeripheralRestore_DuplicateConnectionIdentity");
            yield return new TestCaseData(new PcPeripheralsSnapshot(),
                new Dictionary<string, PcPeripheralKind> { { "orphan", PcPeripheralKind.Microphone } }).SetName("PeripheralRestore_OrphanInstalledItem");
            yield return new TestCaseData(new PcPeripheralsSnapshot { MicrophoneId = "new-mic" },
                new Dictionary<string, PcPeripheralKind>
                {
                    { "new-mic", PcPeripheralKind.Microphone },
                    { "orphan-camera", PcPeripheralKind.Webcam }
                }).SetName("PeripheralRestore_ValidConnectionPlusOrphan");
        }

        private static PcPeripherals ConnectedDevices(bool webcam)
        {
            var devices = new PcPeripherals();
            Assert.That(devices.TryConnect(PcPeripheralKind.Microphone, "microphone"), Is.True);
            if (webcam) Assert.That(devices.TryConnect(PcPeripheralKind.Webcam, "webcam"), Is.True);
            return devices;
        }

        private static StreamSession ConnectedStream(PcPeripherals devices)
        {
            var channel = DesktopAccountTests.RegisteredChannel();
            var stream = new StreamSession(channel, new DonationAccount(), devices);
            Assert.That(stream.Connect(channel.ChannelCode), Is.Null);
            return stream;
        }
    }
}
