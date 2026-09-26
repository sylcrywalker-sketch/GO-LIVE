using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoLive.Viewers
{
    // The authored permanent community: a small set of recognizable viewers. Ids are persistent references
    // (relationships, memories and promises point at them); never rename or reuse one.
    [CreateAssetMenu(fileName = "ViewerCommunity", menuName = "GO! LIVE/Viewers/Viewer Community")]
    public sealed class ViewerCommunityCatalog : ScriptableObject
    {
        [SerializeField] private ViewerProfile[] profiles = Array.Empty<ViewerProfile>();

        public IReadOnlyList<ViewerProfile> Profiles => profiles ?? Array.Empty<ViewerProfile>();
        public string ValidationError => Validate(Profiles);

        public ViewerProfile Find(string id)
        {
            foreach (ViewerProfile profile in Profiles)
                if (profile.Id == id) return profile;
            return null;
        }

        public static string Validate(IReadOnlyList<ViewerProfile> profiles)
        {
            if (profiles == null || profiles.Count == 0) return "The viewer community has no profiles.";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ViewerProfile profile in profiles)
            {
                if (profile == null) return "The viewer community contains an empty profile.";
                string error = profile.Validate();
                if (error != null) return error;
                if (!ids.Add(profile.Id)) return "Duplicate viewer id " + profile.Id + ".";
                if (!names.Add(profile.DisplayName)) return "Duplicate viewer name " + profile.DisplayName + ".";
            }
            return null;
        }
    }
}
