using UnityEngine;

namespace GoLive.Voice
{
    // The authored production voice activity (VAD) settings: one asset, assigned explicitly to the voice input
    // bridge. Recognition reads it and never writes it.
    [CreateAssetMenu(fileName = "VoiceActivity", menuName = "GO! LIVE/Voice/Voice Activity")]
    public sealed class VoiceActivityConfig : ScriptableObject
    {
        [SerializeField] private VoiceActivitySettings settings = new();

        public VoiceActivitySettings Settings => settings;
        public string ValidationError => settings == null ? "Voice activity setting values are missing." : settings.Validate();
    }
}
