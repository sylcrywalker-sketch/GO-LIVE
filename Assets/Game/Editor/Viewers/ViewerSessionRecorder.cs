using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GoLive.Viewers;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GoLive.Editor.Viewers
{
    // Development-only acceptance recorder for a human LIVE session: every reaction decision and published chat line
    // (the Reaction Monitor keeps only the last 80), plus Game-view frame times and hitches. Writes JSONL under Logs/.
    public static class ViewerSessionRecorder
    {
        private const double HitchMilliseconds = 50;

        [Serializable]
        private sealed class Row
        {
            public string type;
            public double realSeconds, streamSeconds, latencySeconds, frameMilliseconds;
            public string eventKey, eventKind, speech, outcome, reason, viewerId, viewerName, source, text, plan;
            public string relationship, memoryIds, promiseId, callbackCandidates, candidateIds;
            public float relevance;
            public long intentId;
            public int frame, promptCharacters;
        }

        private static StreamWriter _writer;
        private static ViewerCore _viewers;
        private static double _started;
        private static int _lastFrame = -1;
        private static readonly List<double> _frames = new();
        private static int _hitches, _reactions, _shown;

        public static bool Recording => _writer != null;
        public static string Path { get; private set; }
        public static string Status => $"Recording {Path}: {_reactions} decisions, {_shown} shown, {_frames.Count} frames, {_hitches} hitches > {HitchMilliseconds} ms";

        [MenuItem("GO! LIVE/Viewer Core/Stop Session Recording")]
        public static void Stop()
        {
            if (_writer == null) return;
            var sorted = new List<double>(_frames);
            sorted.Sort();
            double Percentile(double p) => sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, (int)Math.Floor(p * (sorted.Count - 1) + .5))];
            Write(new Row
            {
                type = "summary", realSeconds = Time.realtimeSinceStartupAsDouble - _started, frame = sorted.Count,
                text = $"frames {sorted.Count}, median {Percentile(.5):0.00} ms, p95 {Percentile(.95):0.00} ms, p99 {Percentile(.99):0.00} ms, " +
                       $"max {Percentile(1):0.00} ms, hitches>{HitchMilliseconds}ms {_hitches}, decisions {_reactions}, shown {_shown}"
            });
            if (_viewers != null)
            {
                _viewers.Log.Added -= OnReaction;
                _viewers.Chat.Added -= OnChat;
            }
            RenderPipelineManager.endContextRendering -= OnRendered;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            _writer.Dispose();
            _writer = null;
            _viewers = null;
            Debug.Log("Viewer session recording saved: " + Path);
        }

        public static void Start(ViewerCore viewers)
        {
            if (viewers == null) throw new ArgumentNullException(nameof(viewers));
            Stop();
            string directory = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ViewerSessions");
            Directory.CreateDirectory(directory);
            Path = System.IO.Path.Combine(directory, "session-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".jsonl");
            _writer = new StreamWriter(Path, false, new UTF8Encoding(false)) { AutoFlush = true };
            _viewers = viewers;
            _started = Time.realtimeSinceStartupAsDouble;
            _frames.Clear(); _hitches = _reactions = _shown = 0; _lastFrame = -1;
            viewers.Log.Added += OnReaction;
            viewers.Chat.Added += OnChat;
            RenderPipelineManager.endContextRendering += OnRendered;
            EditorApplication.playModeStateChanged += OnPlayMode;
            Write(new Row { type = "start", text = "Viewer session recording started" });
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode) Stop();
        }

        private static void OnReaction(ReactionLogEntry entry)
        {
            _reactions++;
            if (entry.Outcome == ReactionOutcome.Shown) _shown++;
            Write(new Row
            {
                type = "reaction", streamSeconds = entry.StreamSeconds, eventKey = entry.EventKey, eventKind = entry.EventKind.ToString(),
                speech = entry.Speech, relevance = entry.Relevance, outcome = entry.Outcome.ToString(), reason = entry.Reason,
                viewerId = entry.ViewerId, viewerName = entry.ViewerName, intentId = entry.IntentId, source = entry.Source.ToString(),
                latencySeconds = entry.LatencySeconds, promptCharacters = entry.PromptCharacters, text = entry.Text, plan = entry.Plan,
                relationship = entry.Relationship, memoryIds = entry.MemoryIds, promiseId = entry.PromiseId,
                callbackCandidates = entry.CallbackCandidates, candidateIds = entry.CandidateIds
            });
        }

        private static void OnChat(StreamChatMessage message) => Write(new Row
        {
            type = "chat", streamSeconds = message.StreamSeconds, viewerId = message.ViewerId, viewerName = message.SenderName,
            text = message.Text, source = message.Source.ToString(), intentId = message.IntentId
        });

        // One sample per rendered player frame of the Game view.
        private static void OnRendered(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (!Application.isPlaying || Time.frameCount == _lastFrame) return;
            bool game = false;
            foreach (Camera camera in cameras) game |= camera != null && camera.cameraType == CameraType.Game;
            if (!game) return;
            _lastFrame = Time.frameCount;
            double milliseconds = Time.unscaledDeltaTime * 1000.0;
            _frames.Add(milliseconds);
            if (milliseconds < HitchMilliseconds) return;
            _hitches++;
            Write(new Row { type = "hitch", frame = Time.frameCount, frameMilliseconds = milliseconds, streamSeconds = _viewers?.Events.Now ?? 0 });
        }

        private static void Write(Row row)
        {
            if (_writer == null) return;
            row.realSeconds = row.realSeconds > 0 ? row.realSeconds : Time.realtimeSinceStartupAsDouble - _started;
            _writer.WriteLine(JsonUtility.ToJson(row));
        }
    }
}
