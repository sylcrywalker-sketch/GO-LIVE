using GoLive.Desktop;
using GoLive.GameTime;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static GoLive.Editor.Desktop.DesktopUiAuthoring;

namespace GoLive.Editor.Desktop
{
    internal static class DesktopShellAuthoring
    {
        internal static void Build(DesktopUiAuthoring ui,Transform root,DesktopRuntimeBehaviour runtime,
            DesktopShellView shell,DesktopAppCatalog catalog,GameClockBehaviour clock)
        {
            var glass=ui.WithTheme(true,new Color(.055f,.31f,.56f));
            var canvas=DesktopSceneAuthoring.CanvasRoot(ui,root,"Desktop Canvas",100);
            var desktop=ui.Rect("Desktop",canvas,0,0,1920,1080);
            DesktopSceneAuthoring.Wallpaper(ui,desktop,1920,1080);
            var windows=ui.Rect("Windows",desktop,0,0,1920,1080);
            var taskbar=glass.Panel("Taskbar",desktop,0,1008,1920,72,new Color(.032f,.13f,.27f,.97f),true);
            ui.Panel("Taskbar top edge",taskbar.transform,0,0,1920,1,new Color(.49f,.75f,.95f,.7f));
            var start=glass.Button("desktop.start",taskbar.transform,0,5,176,62,true);
            start.GetComponentInChildren<TMP_Text>().fontSize=23;
            var tasks=ui.Rect("Open applications",taskbar.transform,194,10,1310,52);
            var taskLayout=tasks.gameObject.AddComponent<HorizontalLayoutGroup>();taskLayout.spacing=6;
            taskLayout.childControlWidth=taskLayout.childControlHeight=false;
            taskLayout.childForceExpandWidth=taskLayout.childForceExpandHeight=false;
            var tray=taskbar.gameObject.AddComponent<DesktopTrayView>();
            Set(tray,"clock",clock);Set(tray,"localization",ui.Localization);
            Set(tray,"language",ui.Text("Language",taskbar.transform,1553,23,42,30,"",17,Color.white));
            ui.Glyph("Network",taskbar.transform,1610,24,26,DesktopGlyphGraphic.GlyphKind.Network,new Color(.85f,.91f,1));
            ui.Glyph("Volume",taskbar.transform,1657,23,28,DesktopGlyphGraphic.GlyphKind.Volume,new Color(.85f,.91f,1));
            ui.Glyph("Monitor",taskbar.transform,1704,23,27,DesktopGlyphGraphic.GlyphKind.Monitor,new Color(.85f,.91f,1));
            var time=ui.Text("Clock",taskbar.transform,1748,9,149,56,"",18,Color.white);time.alignment=TextAlignmentOptions.Center;
            Set(tray,"time",time);
            var menu=glass.Panel("Start menu",desktop,0,454,524,550,new Color(.055f,.19f,.34f,1f),true);
            var menuShadow=menu.gameObject.AddComponent<Shadow>();menuShadow.effectColor=new Color(0,0,0,.24f);menuShadow.effectDistance=new Vector2(4,-4);
            var main=ui.Panel("Application list paper",menu.transform,6,7,298,535,new Color(.955f,.977f,1),true);
            ui.Label("polish.start.apps",main.transform,16,12,266,32,19,ui.Ink,true);
            var menuItems=ui.Rect("Installed applications",main.transform,8,55,282,450);
            var menuLayout=menuItems.gameObject.AddComponent<VerticalLayoutGroup>();menuLayout.spacing=4;
            menuLayout.childControlWidth=menuLayout.childControlHeight=false;
            menuLayout.childForceExpandWidth=menuLayout.childForceExpandHeight=false;
            ui.Glyph("User",menu.transform,375,28,56,DesktopGlyphGraphic.GlyphKind.People,new Color(.85f,.9f,.96f));
            ui.Label("polish.start.user",menu.transform,323,104,188,45,19,Color.white,true);
            var leave=glass.Button("desktop.leave",menu.transform,322,382,186,42);
            var shutdown=glass.Button("polish.start.shutdown",menu.transform,322,486,186,42,true);
            var serial=new SerializedObject(shell);var entries=serial.FindProperty("apps");entries.arraySize=catalog.Apps.Count;
            var contents=new DesktopAppAuthoring(ui,runtime,catalog);
            for(int i=0;i<catalog.Apps.Count;i++)
            {
                var app=catalog.Apps[i];
                int position=app.Id==DesktopAppId.Web?5:app.Id==DesktopAppId.Hub?6:i;
                float y=20+position*129;
                var shortcut=ui.Button(app.NameKey,desktop,4,y,146,116);
                shortcut.targetGraphic.color=Color.white;
                var hover=shortcut.colors;hover.normalColor=Color.clear;hover.highlightedColor=new Color(.6f,.83f,1,.20f);
                hover.selectedColor=new Color(.55f,.8f,1,.29f);hover.pressedColor=new Color(.63f,.85f,1,.37f);shortcut.colors=hover;
                shortcut.GetComponent<UnityEngine.UI.Outline>().effectColor=Color.clear;
                var label=shortcut.GetComponentInChildren<TMP_Text>();Place(label.rectTransform,0,81,146,42);label.fontSize=18;label.color=Color.white;
                var labelShadow=label.gameObject.AddComponent<Shadow>();labelShadow.effectColor=new Color(0,.07f,.18f,.8f);labelShadow.effectDistance=new Vector2(1,-1);
                ui.Icon("App icon",shortcut.transform,41,6,64,app.Icon);
                var appearance=shortcut.gameObject.AddComponent<CanvasGroup>();
                Button menuShortcut;
                if(app.Id==DesktopAppId.MyComputer) menuShortcut=glass.Button(app.NameKey,menu.transform,320,166,188,68);
                else menuShortcut=ui.Button(app.NameKey,menuItems,0,0,282,60);
                if(app.Id==DesktopAppId.Web) menuShortcut.transform.SetSiblingIndex(4);
                menuShortcut.GetComponent<UnityEngine.UI.Outline>().effectColor=Color.clear;
                ui.Icon("App icon",menuShortcut.transform,10,10,38,app.Icon);
                var menuLabel=menuShortcut.GetComponentInChildren<TMP_Text>();Place(menuLabel.rectTransform,58,0,app.Id==DesktopAppId.MyComputer?126:215,60);menuLabel.fontSize=18;menuLabel.alignment=TextAlignmentOptions.MidlineLeft;
                bool dark=app.Id==DesktopAppId.Streamly||app.Id==DesktopAppId.Trich||app.Id==DesktopAppId.Donation;
                var window=ui.Panel(app.Id+" window",windows,176+(i%3)*18,130+(i%3)*18,1180,750,
                    dark?new Color(.073f,.086f,.11f):ui.Paper,true);
                var border=window.gameObject.AddComponent<UnityEngine.UI.Outline>();border.effectColor=new Color(.07f,.15f,.23f,.95f);border.effectDistance=new Vector2(1,-1);
                var shadow=window.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.34f);shadow.effectDistance=new Vector2(5,-7);
                Color titleColor=dark?new Color(.075f,.085f,.12f):new Color(.045f,.24f,.40f);
                var titlebar=ui.Panel("Titlebar",window.transform,0,0,1180,44,titleColor,true);
                ui.Icon("App icon",titlebar.transform,13,8,28,app.Icon);
                ui.Label(app.NameKey,titlebar.transform,54,5,990,35,20,Color.white,true);
                var minimize=glass.Button("desktop.minimize",titlebar.transform,1070,4,44,34);
                var close=glass.Button("desktop.close",titlebar.transform,1120,4,50,34);
                close.targetGraphic.color=new Color(.49f,.13f,.19f);
                var focus=window.gameObject.AddComponent<DesktopWindowFocus>();Set(focus,"runtime",runtime);Int(focus,"appId",(int)app.Id);
                var body=ui.Rect("Content",window.transform,0,44,1180,706);contents.Build(app.Id,body);
                foreach(Selectable control in body.GetComponentsInChildren<Selectable>(true))
                {var child=control.gameObject.AddComponent<DesktopWindowFocus>();Set(child,"runtime",runtime);Int(child,"appId",(int)app.Id);}
                var task=glass.Button(app.NameKey,tasks,0,0,64,52);
                Object.DestroyImmediate(task.GetComponentInChildren<TMP_Text>().gameObject);
                ui.Icon("App icon",task.transform,13,7,38,app.Icon);
                var entry=entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("appId").enumValueIndex=(int)app.Id;
                entry.FindPropertyRelative("shortcut").objectReferenceValue=shortcut;
                entry.FindPropertyRelative("startShortcut").objectReferenceValue=menuShortcut;
                entry.FindPropertyRelative("shortcutAppearance").objectReferenceValue=appearance;
                entry.FindPropertyRelative("window").objectReferenceValue=window.gameObject;
                entry.FindPropertyRelative("minimize").objectReferenceValue=minimize;
                entry.FindPropertyRelative("close").objectReferenceValue=close;
                entry.FindPropertyRelative("task").objectReferenceValue=task;
                entry.FindPropertyRelative("titlebar").objectReferenceValue=titlebar;
                entry.FindPropertyRelative("activeTitleColor").colorValue=titleColor;
            }
            serial.ApplyModifiedPropertiesWithoutUndo();
            Set(shell,"runtime",runtime);Set(shell,"localization",ui.Localization);Set(shell,"desktopRoot",desktop.gameObject);
            Set(shell,"startMenu",menu.gameObject);Set(shell,"startButton",start);Set(shell,"leave",leave);Set(shell,"shutdown",shutdown);
            Set(shell,"toast",ui.Text("Desktop feedback",desktop,210,956,1180,36,"",19,Color.white));
        }
        private static void Place(RectTransform rect,float x,float y,float width,float height)
        {rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);}
    }
}
