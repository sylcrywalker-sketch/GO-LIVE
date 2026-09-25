using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoLive.Desktop
{
    // Persistent ids. Append only; never reorder or renumber.
    public enum DesktopAppId { MyComputer = 0, Streamly = 1, Trich = 2, Outline = 3, Donation = 4, Hub = 5, Web = 6 }

    [Serializable]
    public sealed class DesktopAppDefinition
    {
        [SerializeField] private DesktopAppId id;
        [SerializeField] private string nameKey;
        [SerializeField] private string descriptionKey;
        [SerializeField] private Sprite icon;
        [SerializeField] private int sizeMiB;
        [SerializeField] private bool preinstalled;

        public DesktopAppId Id => id;
        public string NameKey => nameKey;
        public string DescriptionKey => descriptionKey;
        public Sprite Icon => icon;
        public int SizeMiB => sizeMiB;
        public bool Preinstalled => preinstalled;
        // These ids are persisted. An enum display-name change must not rename installed content.
        public string ContentId => id switch
        {
            DesktopAppId.MyComputer => "app.mycomputer",
            DesktopAppId.Streamly => "app.streamly",
            DesktopAppId.Trich => "app.trich",
            DesktopAppId.Outline => "app.outline",
            DesktopAppId.Donation => "app.donation",
            DesktopAppId.Hub => "app.hub",
            DesktopAppId.Web => "app.web",
            _ => null
        };

        public DesktopAppDefinition(DesktopAppId id, string nameKey, string descriptionKey, Sprite icon, int sizeMiB, bool preinstalled)
        {
            this.id = id;
            this.nameKey = nameKey;
            this.descriptionKey = descriptionKey;
            this.icon = icon;
            this.sizeMiB = sizeMiB;
            this.preinstalled = preinstalled;
        }
    }

    [CreateAssetMenu(fileName = "DesktopAppCatalog", menuName = "GO! LIVE/Desktop/App Catalog")]
    public sealed class DesktopAppCatalog : ScriptableObject
    {
        [SerializeField] private DesktopAppDefinition[] apps = Array.Empty<DesktopAppDefinition>();
        private IReadOnlyList<DesktopAppDefinition> _readOnlyApps;
        public IReadOnlyList<DesktopAppDefinition> Apps => _readOnlyApps ??= Array.AsReadOnly(apps ?? Array.Empty<DesktopAppDefinition>());
        public string ValidationError => Validate(apps);

        private void OnValidate() => _readOnlyApps = null;

        public static string Validate(IReadOnlyList<DesktopAppDefinition> definitions)
        {
            if (definitions == null || definitions.Count != Enum.GetValues(typeof(DesktopAppId)).Length)
                return "The desktop catalog must contain every app exactly once.";

            var ids = new HashSet<DesktopAppId>();
            foreach (var definition in definitions)
            {
                if (definition == null || !Enum.IsDefined(typeof(DesktopAppId), definition.Id))
                    return "The desktop catalog contains an unknown app.";
                if (!ids.Add(definition.Id))
                    return "The desktop catalog contains a duplicate app.";
                if (string.IsNullOrWhiteSpace(definition.NameKey) || string.IsNullOrWhiteSpace(definition.DescriptionKey))
                    return "Every desktop app requires name and description localization keys.";
                if (definition.SizeMiB <= 0)
                    return "Every desktop app must occupy a positive number of MiB.";
            }
            return null;
        }
    }
}
