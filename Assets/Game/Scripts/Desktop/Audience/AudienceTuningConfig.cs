using UnityEngine;

namespace GoLive.Desktop
{
    // The authored production audience tuning: one asset, assigned explicitly to the desktop runtime.
    // The simulation reads it and never writes it.
    [CreateAssetMenu(fileName = "AudienceTuning", menuName = "GO! LIVE/Desktop/Audience Tuning")]
    public sealed class AudienceTuningConfig : ScriptableObject
    {
        [SerializeField] private AudienceTuning tuning = new();

        public AudienceTuning Tuning => tuning;
        public string ValidationError => tuning == null ? "Audience tuning values are missing." : tuning.Validate();
    }
}
