using GoLive.Desktop;
using GoLive.Localization;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static GoLive.Editor.Desktop.DesktopUiAuthoring;
using GlyphKind = GoLive.Desktop.DesktopGlyphGraphic.GlyphKind;
using Object = UnityEngine.Object;

namespace GoLive.Editor.Desktop
{
    internal static class DesktopBroadcastAuthoring
    {
        internal static void Streamly(DesktopUiAuthoring source, DesktopRuntimeBehaviour runtime, Transform body)
        {
            var ui = source.WithTheme(true, new Color(.47f, .31f, .68f));
            ui.Panel("Streamly paper", body, 0, 0, 1180, 706, ui.Paper);
            var view = View<StreamlyView>(ui, runtime, body);
            ui.Panel("Toolbar", body, 0, 0, 1180, 54, new Color(.10f, .11f, .16f));
            ui.Glyph("Broadcast", body, 24, 15, 24, GlyphKind.Monitor, new Color(.71f, .60f, .85f));
            ui.Label("desktop.stream.broadcast", body, 62, 12, 460, 32, 22, ui.Ink, true);
            var streamStatus = ui.Text("Stream status", body, 876, 15, 282, 28, "", 17, ui.Muted);
            streamStatus.alignment = TextAlignmentOptions.Right;
            Set(view, "streamStatus", streamStatus);

            ui.Panel("Settings", body, 18, 70, 382, 578, new Color(.092f, .104f, .137f));
            ui.Panel("Preview and output", body, 416, 70, 746, 578, new Color(.083f, .098f, .127f));
            ui.Label("desktop.stream.code", body, 34, 91, 350, 29, 18, ui.Ink, true);
            var code = ui.Input("Channel code", body, 34, 132, 246, 42, "desktop.stream.code_hint", 32);
            code.textComponent.fontSize = 17;
            Set(view, "channelCode", code);
            var paste = ui.Button("desktop.paste", body, 290, 132, 94, 42);
            paste.GetComponentInChildren<TMP_Text>().fontSize = 15;
            Set(view, "paste", paste);
            ui.Label("desktop.stream.channel", body, 34, 199, 232, 27, 17, ui.Muted);
            ui.Text("Channel service", body, 314, 199, 70, 27, "Trich", 17, new Color(.71f, .60f, .85f)).alignment = TextAlignmentOptions.Right;
            Set(view, "channelIdentity", ui.Text("Channel identity from Trich", body, 34, 234, 350, 39, "", 25, ui.Ink, true));
            ui.Label("desktop.stream.identity_source", body, 34, 277, 350, 25, 15, ui.Muted);
            Set(view, "connectionIndicator", ui.Panel("Connection indicator", body, 34, 324, 7, 7, ui.Muted));
            Set(view, "status", ui.Text("Connection status", body, 49, 314, 210, 36, "", 16, ui.Muted));
            var connect = ui.Button("desktop.stream.connect", body, 264, 311, 120, 38);
            connect.GetComponentInChildren<TMP_Text>().fontSize = 16;
            Set(view, "connect", connect);
            Set(view, "connectText", DynamicCaption(connect));
            ui.Panel("Settings divider", body, 34, 369, 350, 1, ui.Line);
            ui.Label("desktop.stream.quality", body, 34, 389, 350, 29, 18, ui.Ink, true);
            var qualities = new Object[3];
            string[] keys = { "low", "medium", "high" };
            for (int i = 0; i < qualities.Length; i++)
            {
                var quality = ui.Button("desktop.stream.quality." + keys[i], body, 34 + i * 120, 433, 110, 39);
                quality.GetComponentInChildren<TMP_Text>().fontSize = 16;
                var colors = quality.colors;
                colors.disabledColor = new Color(.93f, .93f, 1f);
                quality.colors = colors;
                qualities[i] = quality;
            }
            References(view, "quality", qualities);
            ui.Label("desktop.stream.quality_requirement", body, 34, 489, 350, 59, 16, ui.Muted);
            ui.Panel("Environment divider", body, 34, 552, 350, 1, ui.Line);
            ui.Glyph("Internet", body, 34, 570, 21, GlyphKind.Network, ui.Muted);
            Set(view, "upload", ui.Text("Upload speed", body, 67, 565, 317, 31, "", 17, ui.Ink));
            ui.Glyph("Graphics", body, 34, 610, 21, GlyphKind.Monitor, ui.Muted);
            Set(view, "hardware", ui.Text("Graphics availability", body, 67, 606, 317, 31, "", 16, ui.Muted));

            ui.Label("desktop.stream.preview", body, 434, 91, 708, 29, 18, ui.Ink, true);
            ui.Panel("Preview frame", body, 432, 128, 712, 403, ui.Line);
            ui.Panel("Preview black", body, 434, 130, 708, 399, Color.black);
            var image = ui.Rect("Scene preview", body, 434, 130, 708, 399).gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            Set(view, "previewImage", image);
            Set(view, "preview", ui.Text("Preview source", body, 434, 542, 708, 26, "", 15, ui.Muted));
            ui.Panel("Output divider", body, 434, 579, 708, 1, ui.Line);
            Set(view, "requirements", ui.Text("Output requirements", body, 434, 595, 410, 52, "", 17, ui.Muted));
            var start = ui.Button("desktop.stream.start", body, 868, 594, 274, 42, true);
            Set(view, "startStop", start);
            Set(view, "startStopText", DynamicCaption(start));
        }

        internal static void Donation(DesktopUiAuthoring source, DesktopRuntimeBehaviour runtime, Transform body)
        {
            var ui = source.WithTheme(true, new Color(.66f, .43f, .17f));
            Color amber = new(.87f, .68f, .35f);
            ui.Panel("Donation paper", body, 0, 0, 1180, 706, ui.Paper);
            var view = View<DonationView>(ui, runtime, body);
            ui.Label("desktop.donation.title", body, 24, 19, 720, 37, 26, ui.Ink, true);
            Set(view, "serviceStatus", ui.Text("Service status", body, 26, 61, 1128, 28, "", 17, ui.Muted));
            ui.Panel("Donation settings", body, 18, 112, 352, 536, new Color(.10f, .111f, .132f));
            ui.Label("desktop.donation.settings", body, 36, 131, 316, 32, 20, ui.Ink, true);
            ui.Label("desktop.donation.name", body, 36, 188, 316, 26, 17, ui.Muted);
            Set(view, "accountName", ui.Input("Display name", body, 36, 225, 316, 44, "desktop.donation.name_hint", 32));
            var toggleArea = ui.Rect("Alert option", body, 36, 306, 316, 69);
            var check = ui.Panel("Alerts toggle", toggleArea, 0, 4, 23, 23, new Color(.18f, .20f, .23f), true);
            var border = check.gameObject.AddComponent<Outline>();
            border.effectColor = ui.Line; border.effectDistance = new Vector2(1, -1);
            var toggle = toggleArea.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = check;
            toggle.graphic = ui.Glyph("Check", check.transform, 2, 2, 19, GlyphKind.Check, amber);
            toggle.isOn = true;
            var toggleHit = toggleArea.gameObject.AddComponent<Image>();
            toggleHit.color = Color.clear;
            Set(view, "alerts", toggle);
            ui.Label("desktop.donation.alerts", toggleArea, 37, 0, 279, 51, 17, ui.Ink);
            Set(view, "alertStatus", ui.Text("Saved alert state", body, 73, 362, 279, 27, "", 15, ui.Muted));
            Set(view, "save", ui.Button("desktop.save", body, 36, 413, 316, 43, true));
            ui.Panel("Donation settings divider", body, 36, 491, 316, 1, ui.Line);
            ui.Label("desktop.donation.total_support", body, 36, 515, 316, 26, 17, ui.Muted);
            Set(view, "total", ui.Text("Total", body, 36, 552, 316, 55, "", 34, amber, true));

            ui.Panel("Donation history", body, 388, 112, 774, 536, new Color(.086f, .098f, .12f));
            ui.Label("desktop.donation.history", body, 410, 131, 730, 32, 20, ui.Ink, true);
            ui.Label("desktop.donation.sender", body, 410, 185, 570, 26, 15, ui.Muted);
            var amount = ui.Label("desktop.donation.sum", body, 990, 185, 150, 26, 15, ui.Muted);
            amount.alignment = TextAlignmentOptions.Right;
            ui.Panel("History divider", body, 410, 219, 730, 1, ui.Line);
            Set(view, "history", ui.Text("Empty history", body, 410, 251, 680, 85, "", 20, ui.Muted));
            var serialized = new SerializedObject(view);
            var rows = serialized.FindProperty("receipts"); rows.arraySize = 7;
            for (int i = 0; i < rows.arraySize; i++)
            {
                var row = ui.Rect("Donation receipt " + i, body, 410, 231 + i * 56, 730, 53);
                var username = ui.Text("Sender", row, 0, 10, 545, 34, "", 18, ui.Ink, true);
                var value = ui.Text("Amount", row, 560, 10, 170, 34, "", 18, amber, true);
                value.alignment = TextAlignmentOptions.Right;
                ui.Panel("Receipt divider", row, 0, 52, 730, 1, new Color(.14f, .16f, .19f));
                var item = rows.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("root").objectReferenceValue = row.gameObject;
                item.FindPropertyRelative("username").objectReferenceValue = username;
                item.FindPropertyRelative("amount").objectReferenceValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void Overlay(DesktopUiAuthoring source, DesktopRuntimeBehaviour runtime, Transform root)
        {
            var ui = source.WithTheme(true, new Color(.47f, .31f, .68f));
            var canvas = DesktopSceneAuthoring.CanvasRoot(ui, root, "Stream overlay canvas", 120);
            Object.DestroyImmediate(canvas.GetComponent<GraphicRaycaster>());
            var view = canvas.gameObject.AddComponent<StreamOverlayView>();
            var panel = ui.Rect("Live overlay", canvas, 0, 0, 1920, 1080);
            Color paper = new(.026f, .055f, .085f, .96f);
            Color glyphColor = new(.71f, .79f, .89f);
            var statistics = ui.Panel("Stream counters", panel, 708, 18, 504, 56, paper);
            Border(statistics, new Color(.21f, .34f, .47f));
            ui.Glyph("Viewers", statistics.transform, 18, 15, 26, GlyphKind.Eye, glyphColor);
            var viewers = ui.Text("Viewer count", statistics.transform, 55, 13, 91, 33, "", 22, ui.Ink);
            ui.Panel("Viewer divider", statistics.transform, 154, 11, 1, 34, ui.Line);
            ui.Glyph("Subscribers", statistics.transform, 169, 15, 26, GlyphKind.People, glyphColor);
            var followers = ui.Text("Subscriber count", statistics.transform, 207, 13, 91, 33, "", 22, ui.Ink);
            ui.Panel("Subscriber divider", statistics.transform, 312, 11, 1, 34, ui.Line);
            ui.Glyph("Duration", statistics.transform, 329, 15, 26, GlyphKind.Clock, glyphColor);
            var duration = ui.Text("Stream duration", statistics.transform, 365, 13, 128, 33, "", 22, ui.Ink);

            var chatPanel = ui.Panel("Compact stream chat", panel, 1502, 112, 394, 575, paper);
            Border(chatPanel, new Color(.21f, .34f, .47f));
            ui.Label("desktop.overlay.chat", chatPanel.transform, 16, 14, 272, 33, 20, ui.Ink, true);
            ui.Glyph("Chat viewers", chatPanel.transform, 314, 19, 18, GlyphKind.People, glyphColor);
            var chatViewers = ui.Text("Chat viewer count", chatPanel.transform, 340, 17, 39, 29, "", 16, ui.Muted);
            chatViewers.alignment = TextAlignmentOptions.Right;
            ui.Panel("Chat divider", chatPanel.transform, 16, 59, 362, 1, ui.Line);
            var chat = ui.Text("Messages", chatPanel.transform, 16, 76, 362, 418, "", 17, new Color(.83f, .87f, .92f));
            chat.richText = true; chat.lineSpacing = 8;
            var donationPanel = ui.Panel("Donation highlight", chatPanel.transform, 14, 509, 366, 49, new Color(.19f, .15f, .09f, .98f));
            ui.Panel("Donation accent", donationPanel.transform, 0, 0, 3, 49, new Color(.86f, .65f, .30f));
            var donation = ui.Text("Donation alert", donationPanel.transform, 15, 11, 335, 30, "", 17, new Color(.95f, .77f, .42f), true);
            donationPanel.gameObject.SetActive(false);
            Set(view, "runtime", runtime); Set(view, "localization", ui.Localization); Set(view, "panel", panel.gameObject);
            Set(view, "statistics", viewers); Set(view, "subscribers", followers); Set(view, "duration", duration);
            Set(view, "chatViewers", chatViewers); Set(view, "chat", chat); Set(view, "donation", donation);
            Set(view, "donationPanel", donationPanel.gameObject);
        }

        private static T View<T>(DesktopUiAuthoring ui, DesktopRuntimeBehaviour runtime, Transform body) where T : DesktopAppView
        {
            var view = body.gameObject.AddComponent<T>();
            Set(view, "runtime", runtime); Set(view, "localization", ui.Localization);
            Set(view, "feedback", ui.Text("Feedback", body, 24, 663, 1132, 28, "", 17, new Color(.92f, .73f, .45f)));
            return view;
        }

        private static TMP_Text DynamicCaption(Button button)
        {
            Object.DestroyImmediate(button.GetComponentInChildren<LocalizedTextView>());
            return button.GetComponentInChildren<TMP_Text>();
        }

        private static void Border(Image panel, Color color)
        {
            var border = panel.gameObject.AddComponent<Outline>();
            border.effectColor = color;
            border.effectDistance = new Vector2(1, -1);
        }
    }
}
