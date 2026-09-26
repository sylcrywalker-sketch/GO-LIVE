using UnityEngine;

namespace GoLive.Viewers
{
    // The authored Viewer Core configuration: one asset, assigned explicitly to the desktop runtime.
    [CreateAssetMenu(fileName = "ViewerCore", menuName = "GO! LIVE/Viewers/Viewer Core")]
    public sealed class ViewerCoreConfig : ScriptableObject
    {
        [SerializeField] private ReactionTuning reactions = new();
        [SerializeField] private ChatModelSettings model = new();
        [SerializeField] private ViewerCommunityCatalog community;

        public ReactionTuning Reactions => reactions;
        public ChatModelSettings Model => model;
        public ViewerCommunityCatalog Community => community;
        public string ValidationError => reactions == null ? "Reaction tuning values are missing."
            : model == null ? "Chat model settings are missing."
            : community == null ? "The viewer community catalog is missing."
            : reactions.Validate() ?? model.Validate() ?? community.ValidationError;
    }
}
