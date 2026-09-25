using System.Collections.Generic;
using System.IO;
using System.Linq;
using GoLive.Desktop;
using GoLive.Items;
using GoLive.PcBuilding;
using GoLive.Persistence;
using GoLive.Phone;
using GoLive.Player;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace GoLive.Tests
{
    public sealed class DesktopSaveIntegrationTests
    {
        private SaveTestWorld _world;
        private SceneSetup[] _previousScenes;

        [OneTimeSetUp] public void Isolate() => _previousScenes = SaveTestWorld.IsolateScene();
        [OneTimeTearDown] public void RestoreScene() => SaveTestWorld.RestoreScene(_previousScenes);
        [SetUp] public void SetUp() => _world = SaveTestWorld.Create(4000);
        [TearDown] public void TearDown() => _world?.Dispose();

        [Test]
        public void VersionSevenRoundTripPreservesAppsAccountsCodeMessagesAndDonations()
        {
            PrepareDesktop();
            string expected = DesktopJson();
            string code = _world.Desktop.State.Trich.ChannelCode;
            Assert.That(_world.TrySave(), Is.True);
            Assert.That(_world.ReadSave().Version, Is.EqualTo(7));
            Assert.That(_world.ReadSave().Desktop, Is.Not.Null);
            _world.Desktop.State.Trich.EditProfile("Changed", "", 0);
            _world.Desktop.State.Donation.Receive("later", "Viewer", 70);
            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(DesktopJson(), Is.EqualTo(expected));
            Assert.That(_world.Desktop.State.Trich.ChannelCode, Is.EqualTo(code));
            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(DesktopJson(), Is.EqualTo(expected), "repeated loads neither append history nor regenerate channel code");
        }

        [Test]
        public void VersionSixMigrationPreservesExistingWorldAndSeedsOnlySystemApps()
        {
            PrepareDesktop();
            _world.Messages.Messages.TryAddIncoming("phone", "friend", MessageContent.FromText("Saved"), _world.Clock.Clock.Current);
            Assert.That(_world.TrySave(), Is.True);
            var saved = _world.ReadSave();
            saved.Version = 6;
            string json = JsonUtility.ToJson(saved);
            json = ReplaceField(json, "Desktop", JsonUtility.ToJson(saved.Desktop), null);
            File.WriteAllText(_world.SavePath, json);
            _world.Wallet.Wallet.Restore(1);
            _world.Clock.Clock.AdvanceMinutes(90);
            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(_world.Wallet.Wallet.BalanceCents, Is.EqualTo(saved.BalanceCents));
            Assert.That(_world.Clock.Clock.Current.TotalSeconds, Is.EqualTo(saved.GameTimeSeconds));
            Assert.That(_world.Pc.CaptureSnapshot().InstalledSlots.Select(s => s.ItemInstanceId), Is.EquivalentTo(saved.PcAssembly.InstalledSlots.Select(s => s.ItemInstanceId)));
            Assert.That(_world.Messages.Messages.TotalUnreadCount, Is.EqualTo(1));
            Assert.That(_world.Desktop.State.Storage.InstalledContent.Select(c => c.AppId), Is.EquivalentTo(new[] { DesktopAppId.MyComputer, DesktopAppId.Hub, DesktopAppId.Web }));
            Assert.That(_world.Desktop.State.Outline.IsCreated, Is.False);
            Assert.That(_world.Desktop.State.Trich.IsRegistered, Is.False);
            Assert.That(_world.Desktop.State.Donation.TotalCents, Is.Zero);
            Assert.That(_world.TrySave(), Is.True);
            Assert.That(_world.ReadSave().Version, Is.EqualTo(7));
        }

        [Test]
        public void DisconnectedPhysicalDiskContentSurvivesSaveWithoutAppearingOnOtherHardware()
        {
            PrepareDesktop();
            Assert.That(_world.TrySave(), Is.True);
            var saved = _world.ReadSave();
            var disk = saved.Items.Single(item => item.InstanceId == saved.Desktop.Storage.InstalledContent[0].DriveId);
            disk.Location = ItemLocation.World;
            disk.Position = new Vector3(2, 1, 3);
            saved.PcAssembly.InstalledSlots = saved.PcAssembly.InstalledSlots.Where(s => s.ItemInstanceId != disk.InstanceId).ToArray();
            File.WriteAllText(_world.SavePath, JsonUtility.ToJson(saved));
            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(_world.Desktop.State.Storage.Drives, Is.Empty);
            Assert.That(_world.Desktop.State.Storage.IsInstalled(DesktopAppId.Streamly), Is.False);
            Assert.That(_world.Desktop.State.Storage.InstalledContent.Count, Is.EqualTo(saved.Desktop.Storage.InstalledContent.Length));
            Assert.That(_world.TrySave(), Is.True);
            Assert.That(_world.ReadSave().Desktop.Storage.InstalledContent.All(c => c.DriveId == disk.InstanceId), Is.True);
            _world.Dispose();
            _world = null;
            Assert.That(Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include).Any(item => item.AuthoredInstanceId == disk.InstanceId), Is.False,
                "the fixture owns its starter disk even after a load moves it out of the PC hierarchy");
        }

        [Test]
        public void LoadClearsTransientSessionWithoutCommittingTheInterruptedStream()
        {
            PrepareDesktop();
            _world.ConnectMicrophone();
            Assert.That(_world.TrySave(), Is.True);
            var state = _world.Desktop.State;
            Assert.That(state.Stream.Connect(state.Trich.ChannelCode), Is.Null);
            Assert.That(state.Stream.Start(_world.Pc.Capabilities, true, 5f), Is.Null);
            state.Stream.Tick(0.75f, StreamSessionTests.PrimeTime);
            state.Windows.Open(DesktopAppId.Streamly);
            _world.Desktop.Session.Sit();
            int completions = 0;
            state.Trich.Changed += () => { if (state.Trich.CompletedStreams > 0) completions++; };
            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(_world.Desktop.Session.Usage, Is.EqualTo(PcUsageState.Standing));
            Assert.That(_world.Desktop.Session.Power, Is.EqualTo(PcPowerState.Off));
            Assert.That(_world.Desktop.Session.MonitorOn, Is.False);
            Assert.That(state.Windows.Windows, Is.Empty);
            Assert.That(state.Stream.State, Is.EqualTo(StreamState.Offline));
            Assert.That(state.Trich.CompletedStreams, Is.Zero);
            Assert.That(completions, Is.Zero, "load cancellation must not publish a completed stream into persistent accounts");
        }

        [Test]
        public void SaveWhileSeatedUsesCapturedWorldPose()
        {
            var standing = new PlayerPoseSnapshot(new Vector3(4, 0, 5), Quaternion.Euler(0, 35, 0), 12);
            _world.Player.RestorePose(standing);
            Assert.That(_world.PcSession.Session.Sit(), Is.True);
            _world.Player.transform.position = new Vector3(40, 10, 50);
            Assert.That(_world.TrySave(), Is.True);
            var saved = _world.ReadSave().Player;
            Assert.That(saved.Position, Is.EqualTo(standing.Position));
            Assert.That(Quaternion.Angle(saved.Rotation, standing.Rotation), Is.LessThan(0.01f));
            Assert.That(saved.Pitch, Is.EqualTo(standing.Pitch));
        }

        [TestCase("unknown-app")]
        [TestCase("unknown-disk")]
        [TestCase("non-storage-item")]
        [TestCase("wrong-size")]
        [TestCase("duplicate-install")]
        [TestCase("mismatched-email")]
        [TestCase("negative-total")]
        [TestCase("duplicate-message")]
        [TestCase("duplicate-donation")]
        public void CorruptDesktopRejectsTheWholeSaveBeforeAnyMutation(string corruption)
        {
            PrepareDesktop();
            Assert.That(_world.TrySave(), Is.True);
            var saved = _world.ReadSave();
            var desktop = saved.Desktop;
            switch (corruption)
            {
                case "unknown-app": desktop.Storage.InstalledContent[0].ContentId = "app.unknown"; break;
                case "unknown-disk": desktop.Storage.InstalledContent[0].DriveId = "forged-id"; break;
                case "non-storage-item": desktop.Storage.InstalledContent[0].DriveId = saved.PcAssembly.InstalledSlots.First(s => s.SlotId == "cpu-0").ItemInstanceId; break;
                case "wrong-size": desktop.Storage.InstalledContent[0].SizeMiB++; break;
                case "duplicate-install": desktop.Storage.InstalledContent = new[] { desktop.Storage.InstalledContent[0], desktop.Storage.InstalledContent[0] }; break;
                case "mismatched-email": desktop.Trich.Email = "different@outline.local"; break;
                case "negative-total": desktop.Trich.TotalFollowers = -1; break;
                case "duplicate-message": desktop.Outline.Messages = new[] { desktop.Outline.Messages[0], desktop.Outline.Messages[0] }; break;
                case "duplicate-donation": desktop.Donation.History = new[] { desktop.Donation.History[0], desktop.Donation.History[0] }; break;
            }
            File.WriteAllText(_world.SavePath, JsonUtility.ToJson(saved));
            AssertRejectedWithoutMutation();
        }

        [TestCaseSource(nameof(IncompleteSections))]
        public void VersionSevenRequiresEveryDesktopSectionAndVersion(string section, string corruption)
        {
            PrepareDesktop();
            Assert.That(_world.TrySave(), Is.True);
            var saved = _world.ReadSave();
            object value = section switch
            {
                "Desktop" => saved.Desktop,
                "Storage" => saved.Desktop.Storage,
                "Outline" => saved.Desktop.Outline,
                "Trich" => saved.Desktop.Trich,
                _ => saved.Desktop.Donation
            };
            string fieldJson = JsonUtility.ToJson(value);
            string replacement = corruption switch
            {
                "missing" => null,
                "null" => "null",
                "empty" => "{}",
                "version-only" => "{\"Version\":1}",
                _ => fieldJson.Replace("\"Version\":1,", string.Empty)
            };
            File.WriteAllText(_world.SavePath, ReplaceField(JsonUtility.ToJson(saved), section, fieldJson, replacement));
            AssertRejectedWithoutMutation();
        }

        [Test]
        public void MissingAuthoredDesktopReferenceRefusesSaveAndLoad()
        {
            Assert.That(_world.TrySave(), Is.True);
            string before = File.ReadAllText(_world.SavePath);
            SaveTestWorld.SetField(_world.Save, "_desktop", null);
            LogAssert.Expect(LogType.Error, $"GameSaveController on {_world.Root.name} has incomplete configuration.");
            Assert.That(_world.TrySave(), Is.False);
            LogAssert.Expect(LogType.Error, $"GameSaveController on {_world.Root.name} has incomplete configuration.");
            Assert.That(_world.TryLoad(), Is.False);
            Assert.That(File.ReadAllText(_world.SavePath), Is.EqualTo(before));
        }

        private static IEnumerable<TestCaseData> IncompleteSections()
        {
            foreach (string section in new[] { "Desktop", "Storage", "Outline", "Trich", "Donation" })
                foreach (string corruption in new[] { "missing", "null", "empty", "version-only", "missing-version" })
                    yield return new TestCaseData(section, corruption);
        }

        private void PrepareDesktop()
        {
            var state = _world.Desktop.State;
            Assert.That(state.Storage.TryInstall(DesktopAppId.Streamly), Is.Null);
            Assert.That(state.Storage.TryInstall(DesktopAppId.Outline), Is.Null);
            Assert.That(state.Storage.TryInstall(DesktopAppId.Trich), Is.Null);
            Assert.That(state.Outline.CreateAddress("streamer"), Is.Null);
            Assert.That(state.Outline.Receive("mail-1", "test.subject", "test.body", "body"), Is.Null);
            Assert.That(state.Outline.MarkRead("mail-1"), Is.Null);
            Assert.That(state.Trich.Register(state.Outline, state.Outline.Address), Is.Null);
            Assert.That(state.Trich.EditProfile("Channel", "Description", 2), Is.Null);
            Assert.That(state.Donation.Configure("Channel", false), Is.Null);
            Assert.That(state.Donation.Receive("donation-1", "Viewer", 123), Is.Null);
        }

        private void AssertRejectedWithoutMutation()
        {
            _world.Wallet.Wallet.Restore(777);
            _world.Clock.Clock.AdvanceMinutes(5);
            _world.Root.transform.position = new Vector3(1, 2, 3);
            _world.Desktop.State.Windows.Open(DesktopAppId.Hub);
            string desktop = DesktopJson();
            string items = JsonUtility.ToJson(_world.Pc.CaptureSnapshot());
            long time = _world.Clock.Clock.Current.TotalSeconds;
            int changes = 0;
            _world.Desktop.State.Storage.Changed += () => changes++;
            LogAssert.Expect(LogType.Error, $"Save validation failed: {_world.SavePath}");
            Assert.That(_world.TryLoad(), Is.False);
            Assert.That(DesktopJson(), Is.EqualTo(desktop));
            Assert.That(JsonUtility.ToJson(_world.Pc.CaptureSnapshot()), Is.EqualTo(items));
            Assert.That(_world.Desktop.State.Windows.IsOpen(DesktopAppId.Hub), Is.True);
            Assert.That(_world.Wallet.Wallet.BalanceCents, Is.EqualTo(777));
            Assert.That(_world.Clock.Clock.Current.TotalSeconds, Is.EqualTo(time));
            Assert.That(_world.Root.transform.position, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(changes, Is.Zero);
        }

        private string DesktopJson() => JsonUtility.ToJson(_world.Desktop.State.Capture());

        private static string ReplaceField(string json, string name, string value, string replacement)
        {
            string field = "\"" + name + "\":" + value;
            Assert.That(json, Does.Contain(field));
            if (replacement != null)
                return json.Replace(field, "\"" + name + "\":" + replacement);
            return json.Contains(field + ",") ? json.Replace(field + ",", "") : json.Replace("," + field, "");
        }
    }
}
