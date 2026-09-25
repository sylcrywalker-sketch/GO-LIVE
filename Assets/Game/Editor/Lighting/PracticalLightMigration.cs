using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GoLive.Editor.Lighting
{
    // Moves PracticalLight from its old data (a list of Lights whose intensity when play started was taken as the lamp's
    // full level) to explicit bindings (Light + full intensity) without changing how any lamp looks: each full intensity
    // is the intensity its Light was authored with, per-instance overrides in scenes and prefabs included. It works on
    // the serialized text because the old field no longer exists in the class. Idempotent; on any problem it changes
    // nothing and reports what it could not migrate. Delete it once no branch has lamps in the old format.
    internal static class PracticalLightMigration
    {
        public const string ScriptGuid = "8f25d0aad501444ab12301827b40b35d";

        public sealed class Result
        {
            public Dictionary<string, string> ChangedFiles { get; } = new(StringComparer.Ordinal);
            public List<string> Errors { get; } = new();
            public List<string> Changes { get; } = new();
            public bool Succeeded => Errors.Count == 0;
        }

        private sealed class Lamp
        {
            public string FileId;
            public List<string> LightIds = new();
        }

        private const int MonoBehaviourClass = 114;
        private const int LightClass = 108;
        private const int PrefabInstanceClass = 1001;

        private static readonly Regex Header = new(@"(?m)^--- !u!(\d+) &(-?\d+)( stripped)?[^\n]*\n");
        private static readonly Regex Script = new(@"(?m)^  m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32}), type: 3\}$");
        private static readonly Regex LegacyLights = new(@"(?m)^  lights:\n((?:  - \{fileID: -?\d+\}\n)+)");
        private static readonly Regex BoundLights = new(@"(?m)^  lights:\n((?:  - light: \{fileID: -?\d+\}\n    fullIntensity: [^\n]*\n)+)");
        private static readonly Regex EmptyLights = new(@"(?m)^  lights: \[\]\n");
        private static readonly Regex FileId = new(@"fileID: (-?\d+)");
        private static readonly Regex Intensity = new(@"(?m)^  m_Intensity: ([^\n]+)$");
        private static readonly Regex SourcePrefab = new(@"(?m)^  m_SourcePrefab: \{fileID: -?\d+, guid: ([0-9a-f]{32}), type: \d+\}$");
        private static readonly Regex Modification = new(@"(?m)^    - target: \{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: \d+\}\n      propertyPath: ([^\n]+)\n      value: ?([^\n]*)\n      objectReference: \{[^\n]*\}\n");

        private readonly struct Document
        {
            public int ClassId { get; }
            public string FileId { get; }
            public bool Stripped { get; }
            public int BodyStart { get; }
            public int BodyEnd { get; }

            public Document(int classId, string fileId, bool stripped, int bodyStart, int bodyEnd)
            {
                ClassId = classId;
                FileId = fileId;
                Stripped = stripped;
                BodyStart = bodyStart;
                BodyEnd = bodyEnd;
            }
        }

        // files: asset path → serialized text of every scene and prefab to consider; guids: asset path → asset GUID.
        public static Result Migrate(IReadOnlyDictionary<string, string> files, IReadOnlyDictionary<string, string> guids)
        {
            Result result = new();
            Dictionary<string, string> texts = new(StringComparer.Ordinal);
            Dictionary<string, List<Lamp>> lampsByGuid = new(StringComparer.Ordinal);

            foreach (KeyValuePair<string, string> file in files)
            {
                string text = MigrateLamps(file.Key, file.Value, result, out List<Lamp> lamps);
                texts[file.Key] = text;

                if (lamps.Count > 0 && guids.TryGetValue(file.Key, out string guid))
                    lampsByGuid[guid] = lamps;
            }

            foreach (KeyValuePair<string, string> file in files)
                texts[file.Key] = MigrateInstanceOverrides(file.Key, texts[file.Key], lampsByGuid, result);

            if (!result.Succeeded)
            {
                result.Changes.Clear();
                return result;
            }

            foreach (KeyValuePair<string, string> file in files)
            {
                if (!string.Equals(texts[file.Key], file.Value, StringComparison.Ordinal))
                    result.ChangedFiles[file.Key] = texts[file.Key];
            }

            return result;
        }

        // The lamps defined in this file: their old Light list becomes bindings with each Light's authored intensity.
        private static string MigrateLamps(string path, string text, Result result, out List<Lamp> lamps)
        {
            lamps = new List<Lamp>();

            List<Document> documents = Split(text);
            StringBuilder output = new(text.Length + 256);
            int copied = 0;

            foreach (Document document in documents)
            {
                if (document.ClassId != MonoBehaviourClass || document.Stripped)
                    continue;

                string body = text.Substring(document.BodyStart, document.BodyEnd - document.BodyStart);
                Match script = Script.Match(body);

                if (!script.Success || script.Groups[1].Value != ScriptGuid)
                    continue;

                Lamp lamp = new() { FileId = document.FileId };
                lamps.Add(lamp);

                Match bound = BoundLights.Match(body);

                if (bound.Success)
                {
                    foreach (Match light in FileId.Matches(bound.Groups[1].Value))
                        lamp.LightIds.Add(light.Groups[1].Value);

                    continue;
                }

                if (EmptyLights.IsMatch(body))
                    continue;

                Match legacy = LegacyLights.Match(body);

                if (!legacy.Success)
                {
                    result.Errors.Add($"{path}: PracticalLight &{document.FileId} has a 'lights' list in an unknown format.");
                    continue;
                }

                StringBuilder bindings = new("  lights:\n");

                foreach (Match entry in FileId.Matches(legacy.Groups[1].Value))
                {
                    string lightId = entry.Groups[1].Value;

                    if (!TryReadIntensity(text, documents, lightId, out string intensity))
                    {
                        result.Errors.Add($"{path}: PracticalLight &{document.FileId} lists Light &{lightId}, which is not a Light with an intensity in this file.");
                        continue;
                    }

                    lamp.LightIds.Add(lightId);
                    bindings.Append("  - light: {fileID: ").Append(lightId).Append("}\n");
                    bindings.Append("    fullIntensity: ").Append(intensity).Append('\n');
                    result.Changes.Add($"{path}: PracticalLight &{document.FileId}: Light &{lightId} full intensity {intensity}");
                }

                int start = document.BodyStart + legacy.Index;
                output.Append(text, copied, start - copied).Append(bindings);
                copied = start + legacy.Length;
            }

            output.Append(text, copied, text.Length - copied);
            return output.ToString();
        }

        // Scenes and prefabs that override a lamp's Light intensity per instance: the same value becomes that instance's
        // full intensity, which is what the lamp used at runtime before.
        private static string MigrateInstanceOverrides(string path, string text, IReadOnlyDictionary<string, List<Lamp>> lampsByGuid, Result result)
        {
            List<Document> documents = Split(text);
            StringBuilder output = new(text.Length + 256);
            int copied = 0;

            foreach (Document document in documents)
            {
                if (document.ClassId != PrefabInstanceClass)
                    continue;

                string body = text.Substring(document.BodyStart, document.BodyEnd - document.BodyStart);
                Match source = SourcePrefab.Match(body);

                if (!source.Success || !lampsByGuid.TryGetValue(source.Groups[1].Value, out List<Lamp> lamps))
                    continue;

                string sourceGuid = source.Groups[1].Value;
                List<(string Target, string Path, string Value, int End)> modifications = new();

                foreach (Match modification in Modification.Matches(body))
                {
                    if (modification.Groups[2].Value != sourceGuid)
                        continue;

                    modifications.Add((modification.Groups[1].Value, modification.Groups[3].Value, modification.Groups[4].Value, modification.Index + modification.Length));
                }

                List<(string Target, string Path, string Value)> additions = new();

                foreach ((string target, string propertyPath, string value, _) in modifications)
                {
                    foreach (Lamp lamp in lamps)
                    {
                        if (target == lamp.FileId && propertyPath.StartsWith("lights.", StringComparison.Ordinal) && !propertyPath.EndsWith(".fullIntensity", StringComparison.Ordinal))
                            result.Errors.Add($"{path}: prefab instance &{document.FileId} overrides the old light list of its lamp ({propertyPath}); migrate it by hand.");

                        if (propertyPath != "m_Intensity")
                            continue;

                        int index = lamp.LightIds.IndexOf(target);

                        if (index < 0)
                            continue;

                        string fullIntensityPath = $"lights.Array.data[{index.ToString(CultureInfo.InvariantCulture)}].fullIntensity";

                        if (modifications.Exists(existing => existing.Target == lamp.FileId && existing.Path == fullIntensityPath))
                            continue;

                        additions.Add((lamp.FileId, fullIntensityPath, value));
                        result.Changes.Add($"{path}: prefab instance &{document.FileId}: {fullIntensityPath} = {value} (its Light's intensity override)");
                    }
                }

                if (additions.Count == 0)
                    continue;

                StringBuilder patched = new(body);

                // Insert back to front so earlier positions stay valid; each goes where Unity sorts it (target, then path).
                List<(int Position, string Text)> inserts = new();

                foreach ((string target, string propertyPath, string value) in additions)
                {
                    int position = FindSortedPosition(body, modifications, target, propertyPath);
                    string entry = $"    - target: {{fileID: {target}, guid: {sourceGuid}, type: 3}}\n      propertyPath: {propertyPath}\n      value: {value}\n      objectReference: {{fileID: 0}}\n";
                    inserts.Add((position, entry));
                }

                inserts.Sort((left, right) => right.Position.CompareTo(left.Position));

                foreach ((int position, string entry) in inserts)
                    patched.Insert(position, entry);

                output.Append(text, copied, document.BodyStart - copied).Append(patched);
                copied = document.BodyEnd;
            }

            output.Append(text, copied, text.Length - copied);
            return output.ToString();
        }

        private static int FindSortedPosition(string body, List<(string Target, string Path, string Value, int End)> modifications, string target, string propertyPath)
        {
            long targetId = long.Parse(target, CultureInfo.InvariantCulture);
            int position = -1;

            foreach ((string existingTarget, string existingPath, _, int end) in modifications)
            {
                long existingId = long.Parse(existingTarget, CultureInfo.InvariantCulture);
                int order = existingId != targetId ? existingId.CompareTo(targetId) : string.CompareOrdinal(existingPath, propertyPath);

                if (order < 0)
                    position = end;
            }

            if (position >= 0)
                return position;

            int list = body.IndexOf("    m_Modifications:\n", StringComparison.Ordinal);

            if (list < 0)
                throw new InvalidOperationException("Prefab instance without a modification list.");

            return list + "    m_Modifications:\n".Length;
        }

        private static bool TryReadIntensity(string text, List<Document> documents, string lightId, out string intensity)
        {
            intensity = null;

            foreach (Document document in documents)
            {
                if (document.FileId != lightId || document.ClassId != LightClass || document.Stripped)
                    continue;

                Match match = Intensity.Match(text, document.BodyStart, document.BodyEnd - document.BodyStart);

                if (!match.Success)
                    return false;

                intensity = match.Groups[1].Value.Trim();
                return float.TryParse(intensity, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && value >= 0f;
            }

            return false;
        }

        private static List<Document> Split(string text)
        {
            List<Document> documents = new();
            MatchCollection headers = Header.Matches(text);

            for (int i = 0; i < headers.Count; i++)
            {
                Match header = headers[i];
                int end = i + 1 < headers.Count ? headers[i + 1].Index : text.Length;

                documents.Add(new Document(
                    int.Parse(header.Groups[1].Value, CultureInfo.InvariantCulture),
                    header.Groups[2].Value,
                    header.Groups[3].Success,
                    header.Index + header.Length,
                    end));
            }

            return documents;
        }
    }
}
