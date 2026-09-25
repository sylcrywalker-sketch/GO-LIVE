using System;
using TMPro;
using UnityEngine;

namespace GoLive.PcBuilding
{
    // Draws the internal hardware checklist from the existing diagnostics when the workbench refreshes.
    // Owns no assembly state, subscriptions, save data or boot/performance decisions.
    [DisallowMultipleComponent]
    public sealed class PcHardwareStatusView : MonoBehaviour
    {
        [Serializable]
        private sealed class Row
        {
            public PcComponentType component;
            public TMP_Text name;
            public PcHardwareGlyph marker;
        }

        [SerializeField] private Row[] rows = Array.Empty<Row>();
        [SerializeField] private TMP_Text verdictText;
        [SerializeField] private TMP_Text explanationText;
        [SerializeField] private Color validColor = new(.46f, .88f, .43f, 1f);
        [SerializeField] private Color requiredColor = new(.97f, .3f, .27f, 1f);
        [SerializeField] private Color optionalColor = new(.95f, .74f, .3f, 1f);
        [SerializeField] private Color readyColor = new(.43f, .87f, .73f, 1f);

        private void Awake()
        {
            if (verdictText == null || explanationText == null || rows.Length != 6)
            {
                RejectConfiguration();
                return;
            }
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null || rows[i].name == null || rows[i].marker == null)
                {
                    RejectConfiguration();
                    return;
                }
            }
        }

        public void Render(PcCapabilities pc, PcWorkbenchText text)
        {
            if (!enabled) return;
            for (int i = 0; i < rows.Length; i++)
            {
                Row row = rows[i];
                row.name.text = text.ComponentName(row.component);
                PcHardwareGlyph.Shape mark = PcHardwareGlyph.Shape.Check;
                Color tint = validColor;
                for (int diagnostic = 0; diagnostic < pc.Diagnostics.Count; diagnostic++)
                {
                    PcDiagnostic finding = pc.Diagnostics[diagnostic];
                    if (finding.Component != row.component) continue;
                    bool required = finding.Severity == PcDiagnosticSeverity.Blocker;
                    mark = required ? PcHardwareGlyph.Shape.Cross : PcHardwareGlyph.Shape.Warning;
                    tint = required ? requiredColor : optionalColor;
                    break;
                }
                row.marker.Show(mark, tint);
            }
            verdictText.text = text.StatusHeading(pc);
            verdictText.color = pc.CanUseDesktop ? readyColor : requiredColor;
            explanationText.text = text.StatusExplanation(pc);
        }

        private void RejectConfiguration()
        {
            Debug.LogError($"{nameof(PcHardwareStatusView)} on {name} requires six explicit hardware rows and status text references.", this);
            enabled = false;
        }
    }
}
