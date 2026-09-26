using UnityEngine;

namespace GoLive.Viewers
{
    // The authored Viewer Core configuration: one asset, assigned explicitly to the desktop runtime.
    [CreateAssetMenu(fileName = "ViewerCore", menuName = "GO! LIVE/Viewers/Viewer Core")]
    public sealed class ViewerCoreConfig : ScriptableObject
    {
        [SerializeField] private ReactionTuning reactions = new();

        public ReactionTuning Reactions => reactions;
        public string ValidationError => reactions == null ? "Reaction tuning values are missing." : reactions.Validate();
    }
}
