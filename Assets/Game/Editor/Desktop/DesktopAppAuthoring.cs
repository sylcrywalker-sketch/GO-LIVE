using GoLive.Desktop;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static GoLive.Editor.Desktop.DesktopUiAuthoring;

namespace GoLive.Editor.Desktop
{
    internal sealed class DesktopAppAuthoring
    {
        private readonly DesktopUiAuthoring ui;
        private readonly DesktopRuntimeBehaviour runtime;
        private readonly DesktopAppCatalog catalog;
        internal DesktopAppAuthoring(DesktopUiAuthoring ui, DesktopRuntimeBehaviour runtime, DesktopAppCatalog catalog)
        { this.ui=ui; this.runtime=runtime; this.catalog=catalog; }

        internal void Build(DesktopAppId id, Transform body)
        {
            switch(id)
            {
                case DesktopAppId.MyComputer: Computer(body); break;
                case DesktopAppId.Hub: Hub(body); break;
                case DesktopAppId.Web: Web(body); break;
                case DesktopAppId.Trich: DesktopCommunityAuthoring.Trich(ui,runtime,body); break;
                case DesktopAppId.Outline: DesktopCommunityAuthoring.Outline(ui,runtime,body); break;
                case DesktopAppId.Streamly: DesktopBroadcastAuthoring.Streamly(ui,runtime,body); break;
                case DesktopAppId.Donation: DesktopBroadcastAuthoring.Donation(ui,runtime,body); break;
            }
        }
        private T View<T>(Transform body) where T:DesktopAppView
        {
            var view=body.gameObject.AddComponent<T>();
            Set(view,"runtime",runtime); Set(view,"localization",ui.Localization);
            Set(view,"feedback",ui.Text("Feedback",body,26,669,1128,27,"",16,ui.Muted));
            return view;
        }
        private void Computer(Transform parent)
        {
            var view=View<MyComputerView>(parent);
            ui.Panel("Drive header",parent,0,0,1180,72,new Color(.91f,.94f,.97f));
            ui.Label("polish.computer.title",parent,28,23,1100,36,24,ui.Ink,true);
            var empty=ui.Label("desktop.disk.empty",parent,28,112,1000,80,21,ui.Muted);
            Set(view,"empty",empty.gameObject);
            var serial=new SerializedObject(view); var rows=serial.FindProperty("drives"); rows.arraySize=4;
            for(int i=0;i<4;i++)
            {
                var row=ui.Rect("Drive "+i,parent,28+(i%2)*570,112+(i/2)*155,535,124);
                ui.Panel("Disk body",row,12,28,66,43,new Color(.59f,.64f,.68f));
                ui.Panel("Disk front",row,12,58,66,14,new Color(.31f,.38f,.44f));
                ui.Panel("Disk light",row,65,64,6,3,new Color(.40f,.84f,.48f));
                var name=ui.Text("Drive name",row,98,15,430,32,"",21,ui.Ink,true);
                var track=ui.Panel("Usage track",row,98,58,417,15,new Color(.80f,.85f,.89f));
                var fill=ui.Panel("Usage",track.transform,0,0,417,15,new Color(.08f,.48f,.77f));
                fill.rectTransform.anchorMin=Vector2.zero;fill.rectTransform.anchorMax=Vector2.one;
                fill.rectTransform.offsetMin=fill.rectTransform.offsetMax=Vector2.zero;
                var capacity=ui.Text("Capacity",row,98,82,427,32,"",17,ui.Muted);
                var entry=rows.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("root").objectReferenceValue=row.gameObject;
                entry.FindPropertyRelative("name").objectReferenceValue=name;
                entry.FindPropertyRelative("capacity").objectReferenceValue=capacity;
                entry.FindPropertyRelative("usage").objectReferenceValue=fill;
            }
            serial.ApplyModifiedPropertiesWithoutUndo();
        }
        private void Hub(Transform parent)
        {
            var view=View<HubView>(parent);
            ui.Label("polish.hub.subtitle",parent,28,25,690,44,23,ui.Ink,true);
            Set(view,"search",ui.Input("Search apps",parent,768,22,382,40,"polish.hub.search",48));
            ui.Glyph("Search icon",parent,1118,30,22,DesktopGlyphGraphic.GlyphKind.Search,ui.Muted);
            ui.Panel("Header divider",parent,28,88,1124,1,ui.Line);
            var list=ui.Rect("Application list",parent,28,110,1124,535);
            var layout=list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing=10;layout.childControlWidth=layout.childControlHeight=false;
            layout.childForceExpandWidth=layout.childForceExpandHeight=false;
            Set(view,"noResults",ui.Label("polish.hub.no_results",parent,28,126,1120,42,20,ui.Muted).gameObject);
            var serial=new SerializedObject(view);var cards=serial.FindProperty("cards");
            int count=0;foreach(var app in catalog.Apps) if(app.Id!=DesktopAppId.Hub && app.Id!=DesktopAppId.MyComputer)count++;
            cards.arraySize=count;int index=0;
            foreach(var app in catalog.Apps)
            {
                if(app.Id==DesktopAppId.Hub||app.Id==DesktopAppId.MyComputer)continue;
                var row=ui.Panel(app.Id+" card",list,0,0,1124,92,Color.white);
                ui.Icon("App icon",row.transform,18,15,58,app.Icon);
                ui.Label(app.NameKey,row.transform,98,10,720,38,21,ui.Ink,true);
                ui.Label(app.DescriptionKey,row.transform,98,48,720,34,17,ui.Muted);
                var install=ui.Button("desktop.install",row.transform,940,25,158,40,true);
                Object.DestroyImmediate(install.GetComponentInChildren<GoLive.Localization.LocalizedTextView>());
                var entry=cards.GetArrayElementAtIndex(index++);
                entry.FindPropertyRelative("appId").enumValueIndex=(int)app.Id;
                entry.FindPropertyRelative("root").objectReferenceValue=row.gameObject;
                entry.FindPropertyRelative("install").objectReferenceValue=install;
                entry.FindPropertyRelative("state").objectReferenceValue=install.GetComponentInChildren<TMP_Text>();
            }
            serial.ApplyModifiedPropertiesWithoutUndo();
        }
        private void Web(Transform parent)
        {
            var view=View<WebView>(parent);
            ui.Panel("Browser toolbar",parent,0,0,1180,66,new Color(.89f,.93f,.97f));
            Set(view,"back",GlyphButton("polish.web.back",parent,18,DesktopGlyphGraphic.GlyphKind.ArrowLeft));
            Set(view,"forward",GlyphButton("polish.web.forward",parent,66,DesktopGlyphGraphic.GlyphKind.ArrowRight));
            Set(view,"home",GlyphButton("desktop.web.home",parent,114,DesktopGlyphGraphic.GlyphKind.Home));
            var address=ui.Input("Address",parent,176,14,855,38,"desktop.web.address",128);
            address.text="home.go";Set(view,"address",address);
            Set(view,"go",ui.Button("desktop.web.go",parent,1047,14,112,38,true));
            ui.Panel("Page",parent,0,67,1180,590,Color.white);
            ui.Icon("Web emblem",parent,42,109,62,FindIcon(DesktopAppId.Web));
            ui.Label("polish.web.welcome",parent,124,116,990,44,28,ui.Ink,true);
            Set(view,"page",ui.Text("Page content",parent,44,198,1090,200,"",21,ui.Muted));
            ui.Label("polish.web.shortcuts",parent,44,440,1050,32,19,ui.Ink,true);
            Set(view,"hub",ui.Button("desktop.web.hub_link",parent,44,492,316,48));
            Set(view,"trich",ui.Button("desktop.web.trich_link",parent,380,492,316,48));
        }
        private Button GlyphButton(string key,Transform parent,float x,DesktopGlyphGraphic.GlyphKind kind)
        {
            var button=ui.Button(key,parent,x,14,38,38);
            Object.DestroyImmediate(button.GetComponentInChildren<TMP_Text>().gameObject);
            ui.Glyph(key,button.transform,8,8,22,kind,ui.Ink);return button;
        }
        private Sprite FindIcon(DesktopAppId id)
        {foreach(var app in catalog.Apps)if(app.Id==id)return app.Icon;return null;}
    }
}
