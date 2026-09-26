using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using GoLive.Desktop;
using GoLive.Viewers;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GoLive.Editor.Viewers
{
    // Development-only view of the Viewer Core: every recent decision from normalized event to shown message.
    // Reads the running desktop runtime; never part of Streamly or the player's UI.
    public sealed class ViewerCoreWindow : EditorWindow
    {
        private const string ConfigPath = "Assets/Game/Config/Viewers/ViewerCore.asset";
        private Vector2 _scroll;
        private bool _onlySpeech;
        private bool _saveAudio;
        private double _nextRepaint;

        [MenuItem("GO! LIVE/Viewer Core/Reaction Monitor")]
        public static void Open() => GetWindow<ViewerCoreWindow>("Viewer Core");

        [MenuItem("GO! LIVE/Viewer Core/Start Local Model (LM Studio)")]
        public static void StartLocalModel()
        {
            ViewerCoreConfig config = AssetDatabase.LoadAssetAtPath<ViewerCoreConfig>(ConfigPath);
            if (config == null) throw new InvalidOperationException("Missing " + ConfigPath);
            Lms("server start");
            // `lms load` of an already loaded model adds a SECOND instance (":2"): 5 GB more VRAM that evicts the one the game
            // uses (2026-09-26: 4.1 s first reply). Load only when the configured model is not loaded yet.
            if (IsLoaded(config.Model))
            {
                UnityEngine.Debug.Log($"{config.Model.Model} is already loaded; not loading a second instance.");
                return;
            }
            Lms($"load \"{config.Model.Model}\" --gpu max --context-length 4096 -y");
        }

        private static bool IsLoaded(ChatModelSettings model)
        {
            try
            {
                var endpoint = new Uri(model.Endpoint);
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                string json = client.GetStringAsync(new Uri(endpoint, "/api/v0/models/" + model.Model)).GetAwaiter().GetResult();
                return json.Contains("\"state\": \"loaded\"") || json.Contains("\"state\":\"loaded\"");
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("Could not ask LM Studio which models are loaded: " + exception.Message);
                return false;
            }
        }

        [MenuItem("GO! LIVE/Viewer Core/Stop Local Model (LM Studio)")]
        public static void StopLocalModel() => Lms("unload --all");

        private void Update()
        {
            if (!Application.isPlaying || EditorApplication.timeSinceStartup < _nextRepaint) return;
            _nextRepaint = EditorApplication.timeSinceStartup + .5;
            Repaint();
        }

        private void OnGUI()
        {
            DesktopRuntimeBehaviour runtime = Application.isPlaying ? Object.FindAnyObjectByType<DesktopRuntimeBehaviour>() : null;
            if (runtime == null || !runtime.IsReady)
            {
                EditorGUILayout.HelpBox("Enter Play Mode in GL and go LIVE to watch reactions.", MessageType.Info);
                return;
            }
            ViewerCore viewers = runtime.State.Viewers;
            ChatDirector director = viewers.Director;
            ChatDirectorStats stats = director.Stats;
            using (new EditorGUILayout.HorizontalScope())
            {
                director.ModelEnabled = EditorGUILayout.ToggleLeft("Use local model", director.ModelEnabled, GUILayout.Width(130));
                GUILayout.Label($"Model: {director.Health}   Queue: {director.QueueDepth}   Audience: {viewers.Roster.AudienceSize} " +
                                $"(named {viewers.Roster.Named.Count}, anonymous chatters {viewers.Roster.Ephemeral.Count})   Channel: {viewers.ChannelLanguage}");
                _onlySpeech = EditorGUILayout.ToggleLeft("Speech only", _onlySpeech, GUILayout.Width(100));
                using (new EditorGUI.DisabledScope(ViewerSessionRecorder.Recording))
                    _saveAudio = EditorGUILayout.ToggleLeft(new GUIContent("+ mic audio", "Also keep the microphone audio of the recorded session (local files next to the log)"),
                        _saveAudio, GUILayout.Width(90));
                bool record = EditorGUILayout.ToggleLeft("Record session", ViewerSessionRecorder.Recording, GUILayout.Width(120));
                if (record != ViewerSessionRecorder.Recording)
                {
                    if (record) ViewerSessionRecorder.Start(viewers, Object.FindAnyObjectByType<GoLive.Voice.VoiceInputBehaviour>()?.Recognition, _saveAudio);
                    else ViewerSessionRecorder.Stop();
                }
            }
            if (ViewerSessionRecorder.Recording) GUILayout.Label(ViewerSessionRecorder.Status);
            ConversationThread thread = viewers.Selector?.Thread;
            GUILayout.Label($"Warm-up {stats.WarmupSeconds:0.00}s  first {stats.FirstSeconds:0.00}s   Conversation: " +
                            (thread?.ViewerId == null ? "—" : $"{viewers.Roster.Find(thread.ViewerId)?.DisplayName ?? thread.ViewerId}, turn {thread.Turns}, {viewers.Events.Now - thread.LastAt:0}s ago"));
            GUILayout.Label($"Latency median {stats.Percentile(.5):0.00}s  p90 {stats.Percentile(.9):0.00}s  p95 {stats.Percentile(.95):0.00}s  max {stats.Percentile(1):0.00}s   " +
                            $"shown LLM {stats.ShownFromModel} / fallback {stats.ShownFromFallback}   rejected {stats.Rejected}   timeouts {stats.TimedOut}   " +
                            $"unavailable {stats.Unavailable}   stale {stats.DroppedStale}   queue-full {stats.DroppedQueueFull}   max queue {stats.MaximumQueueDepth}");
            int memoryCount = 0;
            foreach (var profile in viewers.Community.Profiles) memoryCount += viewers.Community.State(profile.Id).Memories.Summary.Count;
            GUILayout.Label($"Persistent {viewers.Community.Profiles.Count}, present {viewers.Roster.Named.Count}, memories {memoryCount}, promises {viewers.Community.Promises.Summary.Count}");
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var profile in viewers.Community.Profiles)
            {
                var state = viewers.Community.State(profile.Id);
                GUILayout.Label($"{profile.DisplayName}: {(viewers.Roster.IsWatching(profile.Id) ? "watching" : "absent")} | sentiment {state.Sentiment}, visits {state.VisitCount}, acknowledgements {state.Acknowledgements} | follow {state.HasFollowed}, sub {state.HasSubscribed}");
            }
            var entries = viewers.Log.Entries;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                ReactionLogEntry entry = entries[i];
                if (_onlySpeech && entry.EventKind != StreamEventKind.StreamerSpeech) continue;
                var line = new StringBuilder();
                line.Append($"{entry.StreamSeconds,7:0.0}s  {entry.EventKind,-17} {entry.Outcome,-9}");
                if (entry.Speech != null) line.Append($"  «{entry.Speech}» rel {entry.Relevance:0.00}");
                if (entry.ViewerName != null) line.Append($"  -> {entry.ViewerName}");
                if (entry.Source != ReactionSource.None) line.Append($"  [{entry.Source} {entry.LatencySeconds:0.00}s ~{entry.PromptCharacters / 4}tok]");
                if (!string.IsNullOrEmpty(entry.Reason)) line.Append($"  ({entry.Reason})");
                if (entry.Text != null) line.Append($"  : {entry.Text}");
                EditorGUILayout.SelectableLabel(line.ToString(), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                string detail = $"Candidates: {entry.CandidateIds ?? "—"} | Selection: {entry.SelectionReason ?? "—"} | Memories: {entry.MemoryIds ?? "—"} | Promise: {entry.PromiseId ?? "—"} | Callback candidates: {entry.CallbackCandidates ?? "—"}";
                EditorGUILayout.SelectableLabel(detail, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                EditorGUILayout.SelectableLabel($"Target: {entry.ConversationTarget ?? "—"} | Purpose: {entry.QuestionPurpose ?? "—"}",
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (!string.IsNullOrEmpty(entry.PreviousViewerLine))
                    EditorGUILayout.LabelField("Previous viewer line", entry.PreviousViewerLine, EditorStyles.wordWrappedLabel);
                if (!string.IsNullOrEmpty(entry.DirectAnswerFacts))
                    EditorGUILayout.LabelField("Answer facts", entry.DirectAnswerFacts, EditorStyles.wordWrappedLabel);
                if (!string.IsNullOrEmpty(entry.Plan))
                    EditorGUILayout.SelectableLabel("Plan: " + entry.Plan, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (!string.IsNullOrEmpty(entry.Relationship))
                    EditorGUILayout.LabelField("Relationship", entry.Relationship, EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        private static void Lms(string arguments)
        {
            string lms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".lmstudio", "bin", "lms.exe");
            if (!File.Exists(lms))
            {
                UnityEngine.Debug.LogWarning("LM Studio CLI not found at " + lms + "; start the local server manually.");
                return;
            }
            using Process process = Process.Start(new ProcessStartInfo(lms, arguments)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            });
            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit(120000);
            UnityEngine.Debug.Log($"lms {arguments}: exit {process.ExitCode}\n{output}");
        }
    }
}
