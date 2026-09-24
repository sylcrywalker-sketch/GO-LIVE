using System.Collections.Generic;
using GoLive.Editor.Lighting;
using NUnit.Framework;

namespace GoLive.Tests
{
    // The one-shot move of PracticalLight to explicit full intensities keeps every lamp as bright as it was authored,
    // per prefab instance too, touches nothing else, can run again, and changes nothing when it cannot migrate a lamp.
    public sealed class PracticalLightMigrationTests
    {
        private const string LampGuid = "0123456789abcdef0123456789abcdef";
        private const string ScenePath = "Assets/Test.unity";
        private const string LampPath = "Assets/Lamp.prefab";

        private static readonly string LegacyLamp = Lf(@"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!108 &111
Light:
  m_Enabled: 0
  m_Intensity: 2.2
  m_Range: 5
--- !u!108 &222
Light:
  m_Enabled: 0
  m_Intensity: 0.5
--- !u!114 &900
MonoBehaviour:
  m_GameObject: {fileID: 1}
  m_Script: {fileID: 11500000, guid: 8f25d0aad501444ab12301827b40b35d, type: 3}
  m_EditorClassIdentifier: Assembly-CSharp::GoLive.GameTime.PracticalLight
  lights:
  - {fileID: 111}
  - {fileID: 222}
  glowingParts:
  - renderer: {fileID: 333}
    fullEmission: {r: 3.2, g: 2.5, b: 1.75, a: 1}
--- !u!114 &901
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 3}
  lights:
  - {fileID: 111}
");

        private static readonly string MigratedLampBody = Lf(@"  lights:
  - light: {fileID: 111}
    fullIntensity: 2.2
  - light: {fileID: 222}
    fullIntensity: 0.5
");

        private static readonly string Scene = Lf(@"%YAML 1.1
--- !u!1001 &5000
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {fileID: 0}
    m_Modifications:
    - target: {fileID: 1, guid: 0123456789abcdef0123456789abcdef, type: 3}
      propertyPath: m_Name
      value: Kitchen Light
      objectReference: {fileID: 0}
    - target: {fileID: 222, guid: 0123456789abcdef0123456789abcdef, type: 3}
      propertyPath: m_Intensity
      value: 0.9
      objectReference: {fileID: 0}
    m_RemovedComponents: []
  m_SourcePrefab: {fileID: 100100000, guid: 0123456789abcdef0123456789abcdef, type: 3}
--- !u!114 &5001 stripped
MonoBehaviour:
  m_CorrespondingSourceObject: {fileID: 900, guid: 0123456789abcdef0123456789abcdef, type: 3}
  m_PrefabInstance: {fileID: 5000}
  m_Script: {fileID: 11500000, guid: 8f25d0aad501444ab12301827b40b35d, type: 3}
");

        private static readonly string InstanceOverride = Lf(@"    - target: {fileID: 900, guid: 0123456789abcdef0123456789abcdef, type: 3}
      propertyPath: lights.Array.data[1].fullIntensity
      value: 0.9
      objectReference: {fileID: 0}
");

        [Test]
        public void LegacyLampGetsEachLightsAuthoredIntensity()
        {
            PracticalLightMigration.Result result = Migrate((LampPath, LegacyLamp));

            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Errors));
            Assert.That(result.ChangedFiles[LampPath], Does.Contain(MigratedLampBody));
            Assert.That(result.ChangedFiles[LampPath], Does.Contain("  glowingParts:\n  - renderer: {fileID: 333}"), "the glowing parts stay as they are");
            Assert.That(result.ChangedFiles[LampPath], Does.Contain("guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 3}\n  lights:\n  - {fileID: 111}"), "other scripts' fields are left alone");
        }

        [Test]
        public void AnInstanceIntensityOverrideBecomesThatInstancesFullIntensity()
        {
            PracticalLightMigration.Result result = Migrate((LampPath, LegacyLamp), (ScenePath, Scene));

            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Errors));

            string scene = result.ChangedFiles[ScenePath];
            Assert.That(scene, Does.Contain(InstanceOverride));
            Assert.That(scene.IndexOf("propertyPath: m_Intensity"), Is.LessThan(scene.IndexOf("lights.Array.data[1].fullIntensity")), "sorted by target (&222 before &900) like Unity writes it");
            Assert.That(scene.IndexOf("lights.Array.data[1].fullIntensity"), Is.LessThan(scene.IndexOf("m_RemovedComponents")));
            Assert.That(scene, Does.Contain("propertyPath: m_Intensity\n      value: 0.9"), "the Light keeps its own override");
        }

        [Test]
        public void RunningAgainChangesNothing()
        {
            PracticalLightMigration.Result first = Migrate((LampPath, LegacyLamp), (ScenePath, Scene));
            PracticalLightMigration.Result second = Migrate((LampPath, first.ChangedFiles[LampPath]), (ScenePath, first.ChangedFiles[ScenePath]));

            Assert.That(second.Succeeded, Is.True);
            Assert.That(second.ChangedFiles, Is.Empty);
        }

        // A scene kept from before the migration (say, after a merge) next to an already migrated lamp prefab.
        [Test]
        public void AnOldSceneIsMigratedAgainstAnAlreadyMigratedLamp()
        {
            string migratedLamp = Migrate((LampPath, LegacyLamp)).ChangedFiles[LampPath];

            PracticalLightMigration.Result result = Migrate((LampPath, migratedLamp), (ScenePath, Scene));

            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Errors));
            Assert.That(result.ChangedFiles.Keys, Is.EquivalentTo(new[] { ScenePath }));
            Assert.That(result.ChangedFiles[ScenePath], Does.Contain(InstanceOverride));
        }

        [Test]
        public void ALampListingALightItCannotFindChangesNothing()
        {
            string broken = LegacyLamp.Replace("  - {fileID: 222}\n  glowingParts", "  - {fileID: 999}\n  glowingParts");

            PracticalLightMigration.Result result = Migrate((LampPath, broken), (ScenePath, Scene));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors[0], Does.Contain("&999"));
            Assert.That(result.ChangedFiles, Is.Empty);
        }

        [Test]
        public void AnInstanceOverridingTheOldListChangesNothing()
        {
            string overridesList = Scene.Replace("    m_RemovedComponents: []", Lf(@"    - target: {fileID: 900, guid: 0123456789abcdef0123456789abcdef, type: 3}
      propertyPath: lights.Array.data[0]
      value: 
      objectReference: {fileID: 444}
    m_RemovedComponents: []"));

            PracticalLightMigration.Result result = Migrate((LampPath, LegacyLamp), (ScenePath, overridesList));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors[0], Does.Contain("lights.Array.data[0]"));
            Assert.That(result.ChangedFiles, Is.Empty);
        }

        // The sources may be checked out with CRLF; Unity writes scenes and prefabs with LF.
        private static string Lf(string text)
        {
            return text.Replace("\r\n", "\n");
        }

        private static PracticalLightMigration.Result Migrate(params (string Path, string Text)[] files)
        {
            Dictionary<string, string> texts = new();
            Dictionary<string, string> guids = new() { [LampPath] = LampGuid, [ScenePath] = "fedcba9876543210fedcba9876543210" };

            foreach ((string path, string text) in files)
                texts[path] = text;

            return PracticalLightMigration.Migrate(texts, guids);
        }
    }
}
