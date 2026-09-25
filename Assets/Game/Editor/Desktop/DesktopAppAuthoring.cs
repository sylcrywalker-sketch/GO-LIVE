using System;
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
        { this.ui = ui; this.runtime = runtime; this.catalog = catalog; }

        internal void Build(DesktopAppId id, Transform body)
        {
            switch (id)
            {
                case DesktopAppId.MyComputer: Computer(body); break;
                case DesktopAppId.Hub: Hub(body); break;
                case DesktopAppId.Outline: Outline(body); break;
                case DesktopAppId.Trich: Trich(body); break;
                case DesktopAppId.Streamly: Streamly(body); break;
                case DesktopAppId.Donation: Donation(body); break;
                case DesktopAppId.Web: Web(body); break;
            }
        }
        private T View<T>(Transform body) where T : DesktopAppView
        {
            T view = body.gameObject.AddComponent<T>();
            Set(view,"runtime",runtime); Set(view,"localization",ui.Localization);
            var feedback = ui.Text("Feedback",body,32,654,1100,40,"",19,new Color(.52f,.23f,.15f));
            Set(view,"feedback",feedback);
            return view;
        }
        private void Heading(Transform parent,string title,string subtitle)
        {
            ui.Label(title,parent,32,26,1090,50,32,ui.Ink,true);
            ui.Label(subtitle,parent,32,83,1090,65,21,ui.Muted);
        }
        private void Computer(Transform parent)
        {
            var view = View<MyComputerView>(parent);
            Heading(parent,"desktop.computer.title","desktop.computer.subtitle");
            var empty = ui.Label("desktop.disk.empty",parent,32,180,1000,100,26);
            Set(view,"empty",empty.gameObject);
            var serialized = new SerializedObject(view);
            var rows = serialized.FindProperty("drives"); rows.arraySize=4;
            for(int i=0;i<4;i++)
            {
                var plate=ui.Panel("Drive "+i,parent,32+(i%2)*570,170+(i/2)*205,548,180,Color.white);
                ui.Panel("Disk body",plate.transform,26,32,60,40,ui.Blue);
                ui.Panel("Disk rim",plate.transform,26,62,60,10,new Color(.11f,.26f,.35f));
                ui.Panel("Disk activity",plate.transform,72,66,5,3,new Color(.59f,.85f,.64f));
                var name=ui.Text("Name",plate.transform,104,24,412,42,"",25,ui.Ink,true);
                var track=ui.Panel("Usage track",plate.transform,24,93,498,14,ui.Line);
                var fill=ui.Panel("Usage",track.transform,0,0,498,14,ui.Blue);
                fill.rectTransform.anchorMin=Vector2.zero;fill.rectTransform.anchorMax=Vector2.one;
                fill.rectTransform.offsetMin=fill.rectTransform.offsetMax=Vector2.zero;
                var capacity=ui.Text("Capacity",plate.transform,24,125,500,42,"",21,ui.Muted);
                var row=rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("root").objectReferenceValue=plate.gameObject;
                row.FindPropertyRelative("name").objectReferenceValue=name;
                row.FindPropertyRelative("capacity").objectReferenceValue=capacity;
                row.FindPropertyRelative("usage").objectReferenceValue=fill;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private void Hub(Transform parent)
        {
            var view=View<HubView>(parent);
            Heading(parent,"desktop.hub.title","desktop.hub.subtitle");
            var serialized=new SerializedObject(view); var cards=serialized.FindProperty("cards");
            int count=0; foreach(var app in catalog.Apps) if(!app.Preinstalled) count++;
            cards.arraySize=count; int index=0;
            foreach(var app in catalog.Apps)
            {
                if(app.Preinstalled) continue;
                var card=ui.Panel(app.Id+" card",parent,32+(index%2)*570,165+(index/2)*228,548,206,Color.white);
                ui.Icon("Icon",card.transform,22,24,68,app.Icon);
                ui.Label(app.NameKey,card.transform,108,22,406,40,27,ui.Ink,true);
                ui.Label(app.DescriptionKey,card.transform,108,66,410,65,19,ui.Muted);
                ui.Text("Size",card.transform,24,152,175,32,$"{app.SizeMiB} MB",20,ui.Muted);
                var install=ui.Button("desktop.install",card.transform,316,147,208,42,true);
                UnityEngine.Object.DestroyImmediate(install.GetComponentInChildren<GoLive.Localization.LocalizedTextView>());
                var entry=cards.GetArrayElementAtIndex(index++);
                entry.FindPropertyRelative("appId").enumValueIndex=(int)app.Id;
                entry.FindPropertyRelative("install").objectReferenceValue=install;
                entry.FindPropertyRelative("state").objectReferenceValue=install.GetComponentInChildren<TMP_Text>();
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private void Outline(Transform parent)
        {
            var view=View<OutlineView>(parent);
            var create=ui.Rect("Create account",parent,0,0,1180,640);
            Heading(create,"desktop.outline.create_title","desktop.outline.create_subtitle");
            ui.Label("desktop.outline.username",create,140,220,840,36,21,ui.Muted);
            Set(view,"username",ui.Input("Username",create,140,264,530,58,"desktop.outline.username_hint",24));
            ui.Text("Domain",create,687,270,350,50,"@outline.local",28,ui.Blue);
            Set(view,"create",ui.Button("desktop.outline.create",create,140,353,530,56,true));
            ui.Label("desktop.outline.no_password",create,140,438,850,100,23,ui.Muted);
            var inbox=ui.Rect("Inbox",parent,0,0,1180,640);
            ui.Label("desktop.outline.inbox",inbox,32,22,600,45,32,ui.Ink,true);
            Set(view,"address",ui.Text("Address",inbox,32,76,1100,42,"",22,ui.Blue));
            ui.Panel("Message sheet",inbox,434,140,712,490,Color.white);
            Set(view,"message",ui.Text("Message",inbox,465,172,650,420,"",23));
            Set(view,"createPanel",create.gameObject); Set(view,"inboxPanel",inbox.gameObject);
            var serialized=new SerializedObject(view);var rows=serialized.FindProperty("rows"); rows.arraySize=6;
            for(int i=0;i<6;i++)
            {
                var button=ui.Button("desktop.outline.select",inbox,32,140+i*67,380,58);
                UnityEngine.Object.DestroyImmediate(button.GetComponentInChildren<GoLive.Localization.LocalizedTextView>());
                var label=button.GetComponentInChildren<TMP_Text>();label.alignment=TextAlignmentOptions.MidlineLeft;label.fontSize=19;
                rows.GetArrayElementAtIndex(i).FindPropertyRelative("button").objectReferenceValue=button;
                rows.GetArrayElementAtIndex(i).FindPropertyRelative("subject").objectReferenceValue=label;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Set(view,"previous",ui.Button("desktop.previous",inbox,32,567,112,46));
            Set(view,"next",ui.Button("desktop.next",inbox,300,567,112,46));
            Set(view,"pageLabel",ui.Text("Page",inbox,161,576,120,30,"",20,ui.Muted));
        }
        private void Trich(Transform parent)
        {
            var view=View<TrichView>(parent);
            var register=ui.Rect("Registration",parent,0,0,1180,640);
            Heading(register,"desktop.trich.join","desktop.trich.join_subtitle");
            ui.Label("desktop.trich.email",register,140,224,850,36,21,ui.Muted);
            Set(view,"email",ui.Input("Email",register,140,266,840,58,"desktop.trich.email_hint",64));
            Set(view,"register",ui.Button("desktop.trich.register",register,140,358,390,56,true));
            Set(view,"openOutline",ui.Button("desktop.trich.open_outline",register,550,358,430,56));
            var profile=ui.Rect("Profile",parent,0,0,1180,640);
            ui.Label("desktop.trich.channel",profile,32,22,1000,50,32,ui.Ink,true);
            ui.Label("desktop.trich.name",profile,32,100,650,30,19,ui.Muted);
            Set(view,"channelName",ui.Input("Channel name",profile,32,139,646,54,"desktop.trich.name_hint",32));
            ui.Label("desktop.trich.description",profile,32,216,650,30,19,ui.Muted);
            Set(view,"description",ui.Input("Description",profile,32,255,646,116,"desktop.trich.description_hint",240,true));
            var avatarButtons=new UnityEngine.Object[4];var avatarFrames=new UnityEngine.Object[4];
            for(int i=0;i<4;i++)
            {
                var button=ui.Button("desktop.trich.avatar."+i,profile,32+i*168,404,142,102);
                UnityEngine.Object.DestroyImmediate(button.GetComponentInChildren<TMP_Text>().gameObject);
                var portrait=ui.Rect("Portrait",button.transform,24,5,94,94).gameObject.AddComponent<ChannelAvatarGraphic>();
                Int(portrait,"portrait",i);portrait.raycastTarget=false;
                avatarButtons[i]=button;avatarFrames[i]=button.targetGraphic;
            }
            References(view,"avatars",avatarButtons); References(view,"avatarFrames",avatarFrames);
            Set(view,"save",ui.Button("desktop.save_profile",profile,32,551,646,52,true));
            ui.Panel("Channel card",profile,712,100,434,503,Color.white);
            ui.Label("desktop.trich.code",profile,736,124,390,35,22,ui.Ink,true);
            Set(view,"code",ui.Text("Channel code",profile,736,173,386,66,"",20,ui.Blue));
            Set(view,"copy",ui.Button("desktop.copy",profile,736,251,386,44));
            ui.Panel("Divider",profile,736,324,386,1,ui.Line);
            Set(view,"statistics",ui.Text("Statistics",profile,736,347,386,230,"",22));
            Set(view,"registerPanel",register.gameObject);Set(view,"profilePanel",profile.gameObject);
        }
        private void Streamly(Transform parent)
        {
            var view=View<StreamlyView>(parent);
            ui.Label("desktop.stream.setup",parent,32,24,1100,50,32,ui.Ink,true);
            ui.Label("desktop.stream.code",parent,32,100,1100,32,19,ui.Muted);
            Set(view,"channelCode",ui.Input("Channel code",parent,32,144,682,54,"desktop.stream.code_hint",32));
            Set(view,"paste",ui.Button("desktop.paste",parent,730,144,154,54));
            Set(view,"connect",ui.Button("desktop.stream.connect",parent,900,144,246,54,true));
            Set(view,"status",ui.Text("Connection status",parent,32,215,1100,40,"",21,ui.Blue));
            ui.Label("desktop.stream.quality",parent,32,278,500,32,21,ui.Ink,true);
            var quality=new UnityEngine.Object[3];string[] keys={"low","medium","high"};
            for(int i=0;i<3;i++) quality[i]=ui.Button("desktop.stream.quality."+keys[i],parent,32+i*167,326,155,46);
            References(view,"quality",quality);
            Set(view,"requirements",ui.Text("Requirements",parent,32,400,485,150,"",21,ui.Muted));
            var preview=ui.Panel("Preview",parent,556,278,590,258,new Color(.10f,.15f,.19f));
            ui.Panel("Preview accent",preview.transform,0,0,590,4,new Color(.67f,.23f,.24f));
            var caption=ui.Text("Preview caption",preview.transform,32,43,526,182,"",25,new Color(.85f,.90f,.92f));caption.alignment=TextAlignmentOptions.Center;
            Set(view,"preview",caption);
            var start=ui.Button("desktop.stream.start",parent,556,566,590,56,true);
            Set(view,"startStop",start);Set(view,"startStopText",start.GetComponentInChildren<TMP_Text>());
            UnityEngine.Object.DestroyImmediate(start.GetComponentInChildren<GoLive.Localization.LocalizedTextView>());
        }
        private void Donation(Transform parent)
        {
            var view=View<DonationView>(parent);
            Heading(parent,"desktop.donation.title","desktop.donation.subtitle");
            ui.Label("desktop.donation.name",parent,32,184,510,35,20,ui.Muted);
            Set(view,"accountName",ui.Input("Display name",parent,32,232,510,55,"desktop.donation.name_hint",32));
            var check=ui.Panel("Alerts toggle",parent,32,330,28,28,Color.white,true);
            var toggle=check.gameObject.AddComponent<Toggle>();toggle.targetGraphic=check;
            toggle.graphic=ui.Panel("Tick",check.transform,5,5,18,18,ui.Blue);toggle.isOn=true;
            Set(view,"alerts",toggle);ui.Label("desktop.donation.alerts",parent,77,326,460,65,22);
            Set(view,"save",ui.Button("desktop.save",parent,32,426,510,54,true));
            ui.Panel("Donation history",parent,576,165,570,459,Color.white);
            Set(view,"total",ui.Text("Total",parent,602,190,518,56,"",30,new Color(.69f,.35f,.13f),true));
            ui.Label("desktop.donation.history",parent,602,271,518,40,21,ui.Muted);
            Set(view,"history",ui.Text("History",parent,602,324,518,270,"",22));
        }
        private void Web(Transform parent)
        {
            var view=View<WebView>(parent);
            Set(view,"home",ui.Button("desktop.web.home",parent,24,24,155,52));
            var address=ui.Input("Address",parent,193,24,737,52,"desktop.web.address",128);
            address.text="home.go";Set(view,"address",address);
            Set(view,"go",ui.Button("desktop.web.go",parent,946,24,210,52,true));
            var page=ui.Panel("Page",parent,24,96,1132,537,Color.white);
            ui.Text("GO Web",page.transform,52,42,980,90,"GO! / home",56,ui.Blue,true);
            Set(view,"page",ui.Text("Home content",page.transform,52,158,1028,176,"",26));
            Set(view,"hub",ui.Button("desktop.web.hub_link",page.transform,52,373,490,66));
            Set(view,"trich",ui.Button("desktop.web.trich_link",page.transform,564,373,514,66));
        }
    }
}
