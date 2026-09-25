using GoLive.Desktop;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static GoLive.Editor.Desktop.DesktopUiAuthoring;

namespace GoLive.Editor.Desktop
{
    // Editor-only layout for the two account services; existing domain owners still own every account and message.
    internal static class DesktopCommunityAuthoring
    {
        internal static void Trich(DesktopUiAuthoring source, DesktopRuntimeBehaviour runtime, Transform parent)
        {
            DesktopUiAuthoring ui = source.WithTheme(true, new Color(.47f, .31f, .69f));
            ui.Panel("Trich paper", parent, 0, 0, 1180, 706, ui.Paper);
            ui.Panel("Channel navigation", parent, 0, 0, 190, 706, new Color(.065f, .075f, .095f));
            ui.Panel("Navigation divider", parent, 189, 0, 1, 706, ui.Line);
            ui.Icon("Trich", parent, 18, 22, 39, AppIcon("Trich"));
            ui.Text("Trich name", parent, 68, 26, 112, 39, "Trich", 27, ui.Ink, true);
            ui.Panel("Selected channel", parent, 12, 96, 166, 45, new Color(.19f, .145f, .28f));
            ui.Panel("Selected accent", parent, 12, 96, 3, 45, ui.Blue);
            ui.Glyph("My channel", parent, 24, 106, 25, DesktopGlyphGraphic.GlyphKind.Home, new Color(.70f, .48f, .88f));
            ui.Label("desktop.trich.my_channel", parent, 58, 107, 113, 26, 17, ui.Ink, true);

            TrichView view = parent.gameObject.AddComponent<TrichView>();
            Bind(view, ui, runtime);

            var registration = ui.Rect("Registration", parent, 190, 0, 990, 660);
            ui.Label("desktop.trich.setup_title", registration, 56, 83, 850, 46, 30, ui.Ink, true);
            ui.Label("desktop.trich.setup_subtitle", registration, 56, 143, 814, 75, 20, ui.Muted);
            ui.Label("desktop.trich.email", registration, 56, 245, 760, 28, 18, ui.Muted);
            Set(view, "email", ui.Input("Email", registration, 56, 286, 780, 50, "desktop.trich.email_hint", 64));
            Set(view, "register", ui.Button("desktop.trich.register", registration, 56, 368, 354, 48, true));
            Set(view, "openOutline", ui.Button("desktop.trich.open_outline", registration, 430, 368, 406, 48));
            Set(view, "registerPanel", registration.gameObject);

            var profile = ui.Rect("Profile", parent, 190, 0, 990, 667);
            var overview = ui.Rect("Channel overview", profile, 0, 0, 990, 667);
            ui.Panel("Default channel banner", overview, 22, 24, 946, 188, Color.black);
            var avatar = ui.Rect("Channel avatar", overview, 46, 190, 114, 114).gameObject.AddComponent<ChannelAvatarGraphic>();
            avatar.raycastTarget = false;
            Set(view, "profileAvatar", avatar);
            var identity = ui.Rect("Channel identity", overview, 185, 246, 756, 148);
            var identityLayout = identity.gameObject.AddComponent<VerticalLayoutGroup>();
            identityLayout.spacing = 8;
            identityLayout.childControlWidth = false;
            identityLayout.childControlHeight = true;
            identityLayout.childForceExpandWidth = identityLayout.childForceExpandHeight = false;
            var profileName = ui.Text("Channel name", identity, 0, 0, 504, 41, "", 27, ui.Ink, true);
            profileName.overflowMode = TextOverflowModes.Overflow;
            Set(view, "profileName", profileName);
            // A 32-character name may need two lines. The description uses the remaining height
            // and keeps all 240 permitted characters readable at the normal body size.
            var descriptionViewport = ui.Rect("Description viewport", identity, 0, 49, 756, 91);
            var descriptionLayout = descriptionViewport.gameObject.AddComponent<LayoutElement>();
            descriptionLayout.minHeight = 48;
            descriptionLayout.flexibleHeight = 1;
            descriptionViewport.gameObject.AddComponent<RectMask2D>();
            var descriptionHit = descriptionViewport.gameObject.AddComponent<Image>();
            descriptionHit.color = Color.clear;
            var summary = ui.Text("Channel description", descriptionViewport, 0, 0, 743, 91, "", 18, ui.Muted);
            summary.overflowMode = TextOverflowModes.Overflow;
            var descriptionFitter = summary.gameObject.AddComponent<ContentSizeFitter>();
            descriptionFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var descriptionScroll = descriptionViewport.gameObject.AddComponent<ScrollRect>();
            descriptionScroll.viewport = descriptionViewport;
            descriptionScroll.content = summary.rectTransform;
            descriptionScroll.horizontal = false;
            descriptionScroll.movementType = ScrollRect.MovementType.Clamped;
            descriptionScroll.scrollSensitivity = 28;
            descriptionScroll.inertia = false;
            var descriptionTrack = ui.Panel("Description scroll track", descriptionViewport, 750, 0, 5, 91, ui.Paper);
            descriptionTrack.rectTransform.anchorMin = new Vector2(1, 0);
            descriptionTrack.rectTransform.anchorMax = new Vector2(1, 1);
            descriptionTrack.rectTransform.pivot = new Vector2(1, 1);
            descriptionTrack.rectTransform.anchoredPosition = new Vector2(-1, 0);
            descriptionTrack.rectTransform.sizeDelta = new Vector2(5, 0);
            var descriptionHandle = ui.Panel("Description scroll handle", descriptionTrack.transform, 0, 0, 5, 40, ui.Muted, true);
            var descriptionScrollbar = descriptionTrack.gameObject.AddComponent<Scrollbar>();
            descriptionScrollbar.direction = Scrollbar.Direction.BottomToTop;
            descriptionScrollbar.handleRect = descriptionHandle.rectTransform;
            descriptionScrollbar.targetGraphic = descriptionHandle;
            descriptionScroll.verticalScrollbar = descriptionScrollbar;
            descriptionScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            Set(view, "profileDescription", summary);
            Set(view, "editProfile", ui.Button("desktop.trich.edit_profile", overview, 705, 246, 240, 44));

            Set(view, "subscribers", Statistic(ui, overview, 44, "desktop.trich.subscribers", DesktopGlyphGraphic.GlyphKind.People));
            Set(view, "peakViewers", Statistic(ui, overview, 351, "desktop.trich.peak_viewers", DesktopGlyphGraphic.GlyphKind.Eye));
            Set(view, "hoursStreamed", Statistic(ui, overview, 658, "desktop.trich.hours_streamed", DesktopGlyphGraphic.GlyphKind.Clock));
            ui.Panel("Code divider", overview, 44, 520, 901, 1, ui.Line);
            ui.Label("desktop.trich.code", overview, 44, 544, 710, 28, 19, ui.Ink, true);
            var codePanel = ui.Panel("Channel code field", overview, 44, 582, 633, 51, new Color(.065f, .075f, .10f));
            Border(codePanel, ui.Line);
            var code = ui.Text("Channel code", codePanel.transform, 16, 1, 601, 49, "", 23, ui.Ink);
            code.alignment = TextAlignmentOptions.MidlineLeft;
            code.textWrappingMode = TextWrappingModes.NoWrap;
            Set(view, "code", code);
            Set(view, "copy", ui.Button("desktop.trich.copy_code", overview, 691, 582, 254, 51));
            ui.Label("desktop.trich.code_help", overview, 44, 645, 901, 25, 17, ui.Muted);

            var edit = ui.Rect("Edit channel", profile, 0, 0, 990, 667);
            ui.Label("desktop.trich.edit_profile", edit, 44, 34, 850, 43, 28, ui.Ink, true);
            ui.Label("desktop.trich.name", edit, 44, 115, 850, 26, 18, ui.Muted);
            Set(view, "channelName", ui.Input("Channel name input", edit, 44, 151, 900, 49, "desktop.trich.name_hint", 32));
            ui.Label("desktop.trich.description", edit, 44, 225, 850, 26, 18, ui.Muted);
            Set(view, "description", ui.Input("Description input", edit, 44, 263, 900, 121, "desktop.trich.description_hint", 240, true));
            ui.Label("desktop.trich.avatar_label", edit, 44, 407, 850, 28, 18, ui.Muted);
            var avatarButtons = new Object[4];
            var avatarFrames = new Object[4];
            for (int i = 0; i < 4; i++)
            {
                Button button = ui.Button("desktop.trich.avatar." + i, edit, 44 + i * 112, 448, 94, 94);
                Object.DestroyImmediate(button.GetComponentInChildren<TMP_Text>().gameObject);
                ChannelAvatarGraphic portrait = ui.Rect("Portrait", button.transform, 7, 7, 80, 80).gameObject.AddComponent<ChannelAvatarGraphic>();
                Int(portrait, "portrait", i);
                portrait.raycastTarget = false;
                avatarButtons[i] = button;
                avatarFrames[i] = button.targetGraphic;
            }
            References(view, "avatars", avatarButtons);
            References(view, "avatarFrames", avatarFrames);
            Set(view, "save", ui.Button("desktop.save_profile", edit, 44, 585, 358, 48, true));
            Set(view, "backToProfile", ui.Button("desktop.trich.back_to_profile", edit, 424, 585, 302, 48));
            Set(view, "profilePanel", profile.gameObject);
            Set(view, "overviewPanel", overview.gameObject);
            Set(view, "editPanel", edit.gameObject);
            // Keep the earlier serialized field available for existing scenes, while the new overview has separate real metrics.
            Set(view, "statistics", null);
            edit.gameObject.SetActive(false);
            Set(view, "feedback", ui.Text("Feedback", parent, 234, 677, 916, 27, "", 17, ui.Muted));
        }

        internal static void Outline(DesktopUiAuthoring source, DesktopRuntimeBehaviour runtime, Transform parent)
        {
            DesktopUiAuthoring ui = source.WithTheme(false, new Color(.13f, .37f, .68f));
            ui.Panel("Outline paper", parent, 0, 0, 1180, 706, ui.Paper);
            OutlineView view = parent.gameObject.AddComponent<OutlineView>();
            Bind(view, ui, runtime);
            Sprite outlineIcon = AppIcon("Outline");
            Sprite trichIcon = AppIcon("Trich");
            Set(view, "outlineIcon", outlineIcon);
            Set(view, "trichIcon", trichIcon);

            var create = ui.Rect("Create account", parent, 0, 0, 1180, 660);
            ui.Icon("Outline", create, 86, 91, 64, outlineIcon);
            ui.Label("desktop.outline.setup_title", create, 170, 92, 870, 47, 30, ui.Ink, true);
            ui.Label("desktop.outline.setup_subtitle", create, 170, 157, 800, 77, 20, ui.Muted);
            ui.Label("desktop.outline.username", create, 170, 277, 860, 28, 18, ui.Muted);
            Set(view, "username", ui.Input("Username", create, 170, 316, 525, 50, "desktop.outline.username_hint", 24));
            ui.Text("Domain", create, 712, 322, 300, 42, "@outline.local", 23, ui.Muted);
            Set(view, "create", ui.Button("desktop.outline.create", create, 170, 399, 355, 48, true));
            ui.Label("desktop.outline.address_rules", create, 170, 474, 800, 82, 18, ui.Muted);
            Set(view, "createPanel", create.gameObject);

            var inbox = ui.Rect("Inbox", parent, 0, 0, 1180, 665);
            ui.Panel("Account strip", inbox, 0, 0, 1180, 56, new Color(.92f, .95f, .98f));
            ui.Icon("Mail account", inbox, 21, 15, 25, outlineIcon);
            Set(view, "address", ui.Text("Address", inbox, 61, 16, 1020, 31, "", 19, ui.Ink));
            ui.Panel("Account divider", inbox, 0, 55, 1180, 1, ui.Line);
            ui.Panel("Mail navigation", inbox, 0, 56, 176, 609, new Color(.93f, .96f, .98f));
            ui.Panel("Inbox selected", inbox, 10, 76, 156, 43, new Color(.82f, .89f, .98f));
            ui.Label("desktop.outline.inbox", inbox, 22, 86, 117, 28, 18, ui.Blue, true);
            var unread = ui.Text("Unread count", inbox, 134, 86, 27, 27, "", 16, ui.Blue, true);
            unread.alignment = TextAlignmentOptions.Center;
            Set(view, "unreadCount", unread);
            ui.Panel("Navigation divider", inbox, 175, 56, 1, 609, ui.Line);
            ui.Panel("Mail list", inbox, 176, 56, 368, 609, new Color(.97f, .98f, .99f));
            ui.Panel("Message divider", inbox, 543, 56, 1, 609, ui.Line);
            ui.Panel("Message paper", inbox, 544, 56, 636, 609, Color.white);
            var heading = ui.Rect("Message heading", inbox, 574, 82, 576, 128);
            Set(view, "messageSubject", ui.Text("Subject", heading, 0, 0, 570, 62, "", 26, ui.Ink, true));
            Set(view, "messageSenderIcon", ui.Icon("Sender", heading, 0, 74, 35, outlineIcon));
            var sender = ui.Text("Sender name", heading, 49, 78, 510, 34, "", 19, ui.Ink, true);
            Set(view, "messageSender", sender);
            ui.Panel("Header divider", heading, 0, 127, 576, 1, ui.Line);
            Set(view, "messageHeading", heading.gameObject);

            var viewport = ui.Rect("Message viewport", inbox, 574, 233, 576, 408);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportHit = viewport.gameObject.AddComponent<Image>();
            viewportHit.color = Color.clear;
            viewportHit.raycastTarget = true;
            var message = ui.Text("Message", viewport, 0, 0, 563, 408, "", 21, ui.Ink);
            message.overflowMode = TextOverflowModes.Overflow;
            message.lineSpacing = 8;
            var fitter = message.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = message.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            scroll.inertia = false;
            var track = ui.Panel("Message scroll track", inbox, 1165, 233, 5, 408, new Color(.93f, .95f, .97f));
            var handle = ui.Panel("Message scroll handle", track.transform, 0, 0, 5, 70, new Color(.64f, .70f, .78f), true);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            Set(view, "messageScroll", scroll);
            Set(view, "message", message);
            Set(view, "inboxPanel", inbox.gameObject);

            var serialized = new SerializedObject(view);
            SerializedProperty rows = serialized.FindProperty("rows");
            rows.arraySize = 6;
            for (int i = 0; i < 6; i++)
            {
                var plate = ui.Panel("Message row " + i, inbox, 176, 56 + i * 92, 367, 92, Color.white, true);
                var button = plate.gameObject.AddComponent<Button>();
                button.targetGraphic = plate;
                ColorBlock states = button.colors;
                states.highlightedColor = new Color(.91f, .95f, 1f);
                states.pressedColor = new Color(.80f, .88f, .97f);
                states.selectedColor = Color.white;
                states.fadeDuration = .12f;
                button.colors = states;
                var rowIcon = ui.Icon("Sender icon", plate.transform, 18, 19, 34, outlineIcon);
                var rowSender = ui.Text("Sender", plate.transform, 68, 12, 280, 25, "", 17, ui.Ink, true);
                var subject = ui.Text("Subject", plate.transform, 68, 36, 282, 28, "", 18, ui.Ink);
                subject.textWrappingMode = TextWrappingModes.NoWrap;
                var excerpt = ui.Text("Excerpt", plate.transform, 68, 64, 282, 24, "", 16, ui.Muted);
                excerpt.textWrappingMode = TextWrappingModes.NoWrap;
                var marker = ui.Text("Unread marker", plate.transform, 6, 10, 13, 25, "•", 19, ui.Blue, true);
                ui.Panel("Row divider", plate.transform, 0, 91, 367, 1, ui.Line);
                SerializedProperty row = rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("button").objectReferenceValue = button;
                row.FindPropertyRelative("subject").objectReferenceValue = subject;
                row.FindPropertyRelative("sender").objectReferenceValue = rowSender;
                row.FindPropertyRelative("excerpt").objectReferenceValue = excerpt;
                row.FindPropertyRelative("background").objectReferenceValue = plate;
                row.FindPropertyRelative("senderIcon").objectReferenceValue = rowIcon;
                row.FindPropertyRelative("unreadMarker").objectReferenceValue = marker.gameObject;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Set(view, "previous", ui.Button("desktop.previous", inbox, 192, 620, 98, 34));
            Set(view, "next", ui.Button("desktop.next", inbox, 429, 620, 98, 34));
            var pageLabel = ui.Text("Page", inbox, 305, 624, 109, 27, "", 17, ui.Muted);
            pageLabel.alignment = TextAlignmentOptions.Center;
            Set(view, "pageLabel", pageLabel);
            Set(view, "feedback", ui.Text("Feedback", parent, 22, 677, 1136, 27, "", 17, ui.Muted));
        }

        private static TMP_Text Statistic(DesktopUiAuthoring ui, Transform parent, float x, string label, DesktopGlyphGraphic.GlyphKind glyph)
        {
            var card = ui.Panel(label, parent, x, 404, 287, 84, new Color(.095f, .11f, .145f));
            Border(card, ui.Line);
            ui.Glyph("Statistic icon", card.transform, 21, 23, 34, glyph, ui.Muted);
            TMP_Text value = ui.Text("Value", card.transform, 75, 13, 195, 34, "", 26, ui.Ink, true);
            value.enableAutoSizing = true;
            value.fontSizeMin = 18;
            value.fontSizeMax = 26;
            value.textWrappingMode = TextWrappingModes.NoWrap;
            ui.Label(label, card.transform, 75, 51, 195, 27, 17, ui.Muted);
            return value;
        }

        private static void Bind(DesktopAppView view, DesktopUiAuthoring ui, DesktopRuntimeBehaviour runtime)
        {
            Set(view, "runtime", runtime);
            Set(view, "localization", ui.Localization);
        }

        private static Sprite AppIcon(string app) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Desktop/Icons/" + app + ".png");

        private static void Border(Image image, Color color)
        {
            var border = image.gameObject.AddComponent<UnityEngine.UI.Outline>();
            border.effectColor = color;
            border.effectDistance = new Vector2(1, -1);
        }
    }
}
