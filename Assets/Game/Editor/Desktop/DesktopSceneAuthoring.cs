using System;
using System.Collections.Generic;
using System.IO;
using GoLive.Desktop;
using GoLive.Localization;
using GoLive.PcBuilding;
using GoLive.Persistence;
using GoLive.Player;
using GoLive.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static GoLive.Editor.Desktop.DesktopUiAuthoring;
using Object = UnityEngine.Object;

namespace GoLive.Editor.Desktop
{
    // Explicit one-shot authoring. No initialization hook and no runtime UI construction/searches.
    public static class DesktopSceneAuthoring
    {
        private const string ScenePath="Assets/Game/Scenes/GL.unity";
        private const string CatalogPath="Assets/Game/Config/Desktop/DesktopAppCatalog.asset";

        [MenuItem("GO! LIVE/Desktop/Author vertical slice in GL")]
        public static void Apply()
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var existing=Object.FindObjectsByType<DesktopShellView>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            foreach(var shell in existing) Object.DestroyImmediate(shell.gameObject);
            var pc=One<PcAssemblyBehaviour>();var player=One<PlayerController>();var carry=One<PlayerCarry>();
            var screen=One<PcScreenView>();var locale=One<LocalizationContext>();var workbench=One<PcWorkbenchBehaviour>();
            var camera=player.GetComponentInChildren<Camera>(true);
            var catalog=ImportCatalog();
            DesktopLocalizationAuthoring.Apply(locale);
            var root=new GameObject("Desktop System");
            var session=root.AddComponent<PcSessionBehaviour>();
            var runtime=root.AddComponent<DesktopRuntimeBehaviour>();
            var shellView=root.AddComponent<DesktopShellView>();
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/UI/Fonts/Manrope/Manrope-Medium SDF.asset");
            var ui=new DesktopUiAuthoring(font,locale);
            Set(session,"pc",pc);Set(session,"playerController",player);Set(session,"playerCarry",carry);Set(session,"playerCamera",camera);
            var monitorCollider=screen.GetComponent<Collider>();
            if(monitorCollider==null) throw new InvalidOperationException("Physical monitor collider missing.");
            Set(session,"monitorCollider",monitorCollider);
            Set(session,"monitorAction",InputReference("Use"));Set(session,"focusAction",InputReference("PhoneSubmit"));
            Set(runtime,"pc",pc);Set(runtime,"session",session);Set(runtime,"catalog",catalog);
            Set(screen,"session",session);Set(workbench,"session",session);Set(One<GameUiInputRouter>(),"pcSession",session);
            Set(One<GameSaveController>(),"_desktop",runtime);
            var caseInteractable=pc.GetComponent<PcCaseInteractable>();
            if(caseInteractable==null) caseInteractable=pc.gameObject.AddComponent<PcCaseInteractable>();
            Set(caseInteractable,"session",session);
            var monitorInteractable=screen.GetComponent<PcMonitorInteractable>();
            if(monitorInteractable==null) monitorInteractable=screen.gameObject.AddComponent<PcMonitorInteractable>();
            Set(monitorInteractable,"session",session);
            var body=screen.GetComponent<Rigidbody>();if(body!=null) body.isKinematic=true;
            var shellCanvas=CanvasRoot(ui,root.transform,"Desktop Canvas",100);
            var desktop=ui.Rect("Desktop",shellCanvas,0,0,1920,1080);
            Wallpaper(ui,desktop,1920,1080);
            ui.Text("Edition",desktop,1540,38,330,78,"GO! LIVE\nhome edition",25,new Color(1,1,1,.55f));
            var windows=ui.Rect("Windows",desktop,0,0,1920,1080);
            var taskbar=ui.Panel("Taskbar",desktop,0,1018,1920,62,new Color(.09f,.18f,.24f,.96f),true);
            ui.Panel("Taskbar glint",taskbar.transform,0,0,1920,1,new Color(.7f,.9f,1,.4f));
            var start=ui.Button("desktop.start",taskbar.transform,12,8,140,47,true);
            var leave=ui.Button("desktop.leave",taskbar.transform,1730,8,176,47);
            var tasks=ui.Rect("Open applications",taskbar.transform,169,8,1500,47);
            var taskLayout=tasks.gameObject.AddComponent<HorizontalLayoutGroup>();taskLayout.spacing=8;
            taskLayout.childControlWidth=taskLayout.childControlHeight=false;
            taskLayout.childForceExpandWidth=taskLayout.childForceExpandHeight=false;
            var menu=ui.Panel("Start menu",desktop,12,469,446,539,new Color(.93f,.97f,.98f,.98f),true);
            ui.Text("Menu header",menu.transform,22,14,400,42,"GO! LIVE",27,ui.Blue,true);
            var menuItems=ui.Rect("Installed applications",menu.transform,18,66,408,453);
            var menuLayout=menuItems.gameObject.AddComponent<VerticalLayoutGroup>();menuLayout.spacing=8;
            menuLayout.childControlWidth=menuLayout.childControlHeight=false;
            menuLayout.childForceExpandWidth=menuLayout.childForceExpandHeight=false;
            var serial=new SerializedObject(shellView);var apps=serial.FindProperty("apps");apps.arraySize=catalog.Apps.Count;
            var contents=new DesktopAppAuthoring(ui,runtime,catalog);
            for(int i=0;i<catalog.Apps.Count;i++)
            {
                var app=catalog.Apps[i];float y=26+i*136;
                var shortcut=ui.Button(app.NameKey,desktop,12,y,174,122);
                shortcut.targetGraphic.color=Color.white;
                var iconColors=shortcut.colors;iconColors.normalColor=Color.clear;
                iconColors.highlightedColor=new Color(1,1,1,.13f);iconColors.selectedColor=new Color(1,1,1,.18f);
                iconColors.pressedColor=new Color(1,1,1,.23f);shortcut.colors=iconColors;
                shortcut.GetComponent<UnityEngine.UI.Outline>().effectColor=Color.clear;
                var label=shortcut.GetComponentInChildren<TMP_Text>();Place(label.rectTransform,0,88,174,32);label.fontSize=18;label.color=Color.white;
                ui.Panel("Icon backing",shortcut.transform,50,6,74,74,new Color(1,1,1,.94f));
                ui.Icon("App icon",shortcut.transform,54,10,66,app.Icon);
                var appearance=shortcut.gameObject.AddComponent<CanvasGroup>();
                var menuShortcut=ui.Button(app.NameKey,menuItems,0,0,408,56);
                ui.Icon("App icon",menuShortcut.transform,10,6,44,app.Icon);
                Place(menuShortcut.GetComponentInChildren<TMP_Text>().rectTransform,66,0,330,56);
                var window=ui.Panel(app.Id+" window",windows,205+(i%3)*22,134+(i%3)*22,1180,758,ui.Paper,true);
                var shadow=window.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.28f);shadow.effectDistance=new Vector2(7,-10);
                var titlebar=ui.Panel("Titlebar",window.transform,0,0,1180,52,ui.Blue,true);
                ui.Icon("App icon",titlebar.transform,13,9,32,app.Icon);
                ui.Label(app.NameKey,titlebar.transform,58,8,885,40,22,Color.white,true);
                var minimize=ui.Button("desktop.minimize",titlebar.transform,994,7,78,37);
                var close=ui.Button("desktop.close",titlebar.transform,1083,7,84,37);
                var focus=window.gameObject.AddComponent<DesktopWindowFocus>();Set(focus,"runtime",runtime);Int(focus,"appId",(int)app.Id);
                var content=ui.Rect("Content",window.transform,0,52,1180,706);
                contents.Build(app.Id,content);
                // Buttons and fields handle PointerDown before the window root receives it.
                foreach(Selectable selectable in content.GetComponentsInChildren<Selectable>(true))
                {
                    var childFocus=selectable.gameObject.AddComponent<DesktopWindowFocus>();
                    Set(childFocus,"runtime",runtime);Int(childFocus,"appId",(int)app.Id);
                }
                var task=ui.Button(app.NameKey,tasks,0,0,198,47);
                ui.Icon("App icon",task.transform,8,9,29,app.Icon);
                Place(task.GetComponentInChildren<TMP_Text>().rectTransform,42,0,150,47);task.GetComponentInChildren<TMP_Text>().fontSize=17;
                var entry=apps.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("appId").enumValueIndex=(int)app.Id;
                entry.FindPropertyRelative("shortcut").objectReferenceValue=shortcut;
                entry.FindPropertyRelative("startShortcut").objectReferenceValue=menuShortcut;
                entry.FindPropertyRelative("shortcutAppearance").objectReferenceValue=appearance;
                entry.FindPropertyRelative("window").objectReferenceValue=window.gameObject;
                entry.FindPropertyRelative("minimize").objectReferenceValue=minimize;
                entry.FindPropertyRelative("close").objectReferenceValue=close;
                entry.FindPropertyRelative("task").objectReferenceValue=task;
                entry.FindPropertyRelative("titlebar").objectReferenceValue=titlebar;
            }
            serial.ApplyModifiedPropertiesWithoutUndo();
            var toast=ui.Text("Desktop feedback",desktop,226,952,1330,46,"",23,Color.white);
            Set(shellView,"runtime",runtime);Set(shellView,"localization",locale);Set(shellView,"desktopRoot",desktop.gameObject);
            Set(shellView,"startMenu",menu.gameObject);Set(shellView,"startButton",start);Set(shellView,"leave",leave);Set(shellView,"toast",toast);
            BuildOverlay(ui,root.transform,runtime);
            BuildMonitor(ui,root.transform,screen,session,runtime,catalog);
            PcWorkbenchUxAuthoring.Apply();
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene,ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Desktop vertical slice authored in GL with seven verified icon mappings and explicit references.");
        }

        private static T One<T>() where T:Object
        {
            T[] found=Object.FindObjectsByType<T>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            if(found.Length!=1) throw new InvalidOperationException($"Expected one {typeof(T).Name}, found {found.Length}.");
            return found[0];
        }
        internal static RectTransform CanvasRoot(DesktopUiAuthoring ui,Transform parent,string name,int order)
        {
            var rect=ui.Rect(name,parent,0,0,1920,1080);
            var canvas=rect.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=order;
            var scaler=rect.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            rect.gameObject.AddComponent<GraphicRaycaster>();
            return rect;
        }
        internal static void Wallpaper(DesktopUiAuthoring ui,Transform parent,float width,float height)
        {
            var wallpaper=ui.Rect("Wallpaper",parent,0,0,width,height).gameObject.AddComponent<DesktopWallpaper>();wallpaper.raycastTarget=false;
        }
        private static void Place(RectTransform rect,float x,float y,float width,float height)
        {rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);}
        private static InputActionReference InputReference(string actionName)
        {
            const string path="Assets/Game/_Project/GO_LIVE_Input.inputactions";
            foreach(Object item in AssetDatabase.LoadAllAssetsAtPath(path))
                if(item is InputActionReference reference && reference.action?.name==actionName) return reference;
            var asset=AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            var created=InputActionReference.Create(asset.FindAction(actionName,true));
            Directory.CreateDirectory("Assets/Game/Config/Desktop");
            AssetDatabase.CreateAsset(created,"Assets/Game/Config/Desktop/"+actionName+".asset");
            return created;
        }
        private static DesktopAppCatalog ImportCatalog()
        {
            Directory.CreateDirectory("Assets/Game/Config/Desktop");AssetDatabase.Refresh();
            var catalog=AssetDatabase.LoadAssetAtPath<DesktopAppCatalog>(CatalogPath);
            if(catalog==null) {catalog=ScriptableObject.CreateInstance<DesktopAppCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath);}
            var serial=new SerializedObject(catalog);var apps=serial.FindProperty("apps");apps.arraySize=7;
            int[] sizes={48,240,96,64,48,80,120};
            for(int i=0;i<7;i++)
            {
                var id=(DesktopAppId)i;string name=id.ToString();string path="Assets/Game/Art/Desktop/Icons/"+name+".png";
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;
                importer.maxTextureSize=1024;importer.alphaIsTransparency=true;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
                var app=apps.GetArrayElementAtIndex(i);string token=name.ToLowerInvariant();
                app.FindPropertyRelative("id").enumValueIndex=i;
                app.FindPropertyRelative("nameKey").stringValue="desktop.app."+token;
                app.FindPropertyRelative("descriptionKey").stringValue="desktop.app."+token+".description";
                app.FindPropertyRelative("icon").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                app.FindPropertyRelative("sizeMiB").intValue=sizes[i];
                app.FindPropertyRelative("preinstalled").boolValue=id==DesktopAppId.MyComputer||id==DesktopAppId.Hub||id==DesktopAppId.Web;
            }
            serial.ApplyModifiedPropertiesWithoutUndo();return catalog;
        }
        private static void BuildOverlay(DesktopUiAuthoring ui,Transform root,DesktopRuntimeBehaviour runtime)
        {
            var canvas=CanvasRoot(ui,root,"Stream overlay canvas",120);Object.DestroyImmediate(canvas.GetComponent<GraphicRaycaster>());
            var view=canvas.gameObject.AddComponent<StreamOverlayView>();
            var panel=ui.Rect("Live overlay",canvas,1556,116,330,842);
            ui.Panel("Stats",panel,0,0,330,235,new Color(.06f,.09f,.12f,.94f));
            ui.Text("Live label",panel,22,16,284,32,"● LIVE",22,new Color(.96f,.39f,.35f),true);
            var stats=ui.Text("Statistics",panel,22,64,284,152,"",23,new Color(.94f,.96f,.97f));
            ui.Panel("Chat",panel,0,247,330,595,new Color(.32f,.11f,.12f,.94f));
            ui.Label("desktop.overlay.chat",panel,22,265,284,34,22,Color.white,true);
            var chat=ui.Text("Messages",panel,22,313,284,470,"",18,new Color(1,.88f,.85f));
            var donation=ui.Text("Donation alert",panel,22,798,284,36,"",17,new Color(1,.81f,.40f));
            Set(view,"runtime",runtime);Set(view,"localization",ui.Localization);Set(view,"panel",panel.gameObject);
            Set(view,"statistics",stats);Set(view,"chat",chat);Set(view,"donation",donation);
        }
        private static void BuildMonitor(DesktopUiAuthoring ui,Transform root,PcScreenView screen,PcSessionBehaviour session,DesktopRuntimeBehaviour runtime,DesktopAppCatalog catalog)
        {
            MonitorScreenGeometry geometry=MonitorScreenGeometry.Read(screen.GetComponent<MeshFilter>(),screen.GetComponent<Renderer>());
            var anchor=new GameObject("PC seat view").transform;anchor.SetParent(root,false);
            anchor.position=geometry.Center+geometry.Normal*.86f+Vector3.up*.06f;
            anchor.rotation=Quaternion.LookRotation(geometry.Center-anchor.position,Vector3.up);Set(session,"seatViewAnchor",anchor);
            var surface=ui.Rect("Physical monitor display",root,0,0,800,600);
            surface.pivot=new Vector2(.5f,.5f);surface.position=geometry.Center+geometry.Normal*.0015f;
            surface.rotation=Quaternion.LookRotation(-geometry.Normal,Vector3.up);
            surface.localScale=new Vector3(geometry.Width/800f,geometry.Height/600f,1);
            var canvas=surface.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            var dark=ui.Panel("Black screen",surface,0,0,800,600,new Color(.006f,.009f,.013f));
            var splash=ui.Rect("Boot splash",surface,0,0,800,600);
            var title=ui.Text("GO boot",splash,100,214,600,92,"GO! LIVE",70,new Color(.65f,.82f,.85f),true);title.alignment=TextAlignmentOptions.Center;
            var boot=ui.Label("desktop.boot",splash,100,323,600,50,27,new Color(.55f,.68f,.73f));boot.alignment=TextAlignmentOptions.Center;
            var desktop=ui.Rect("Monitor desktop",surface,0,0,800,600);Wallpaper(ui,desktop,800,600);
            var iconGroups=new Object[catalog.Apps.Count];
            for(int i=0;i<catalog.Apps.Count;i++) iconGroups[i]=ui.Icon(catalog.Apps[i].Id.ToString(),desktop,28,20+i*69,49,catalog.Apps[i].Icon).gameObject.AddComponent<CanvasGroup>();
            var miniature=ui.Panel("Active window",desktop,122,82,626,434,ui.Paper);
            ui.Panel("Titlebar",miniature.transform,0,0,626,48,ui.Blue);
            var miniTitle=ui.Text("App title",miniature.transform,18,8,590,38,"",25,Color.white,true);
            var miniContent=ui.Text("App content",miniature.transform,28,78,568,310,"",28,ui.Ink);
            var mirror=desktop.gameObject.AddComponent<MonitorDesktopView>();
            Set(mirror,"runtime",runtime);Set(mirror,"localization",ui.Localization);Set(mirror,"window",miniature.gameObject);
            Set(mirror,"title",miniTitle);Set(mirror,"content",miniContent);References(mirror,"icons",iconGroups);
            ui.Panel("Monitor taskbar",desktop,0,560,800,40,new Color(.08f,.15f,.2f));ui.Text("Start",desktop,15,567,120,25,"GO!",20,Color.white,true);
            var hints=CanvasRoot(ui,root,"PC session feedback canvas",110);Object.DestroyImmediate(hints.GetComponent<GraphicRaycaster>());
            var hint=ui.Text("Seated hint",hints,440,963,1040,66,"",23,Color.white);hint.alignment=TextAlignmentOptions.Center;
            var view=surface.gameObject.AddComponent<MonitorSurfaceView>();Set(view,"session",session);Set(view,"localization",ui.Localization);
            Set(view,"splash",splash.gameObject);Set(view,"desktop",desktop.gameObject);Set(view,"seatedHint",hint);
            var feedbackView=hints.gameObject.AddComponent<PcSessionFeedbackView>();
            var panel=ui.Panel("PC feedback",hints,610,190,700,216,new Color(.06f,.1f,.13f,.96f));
            var message=ui.Text("Reason",panel.transform,25,20,650,180,"",25,Color.white);
            Set(feedbackView,"session",session);Set(feedbackView,"localization",ui.Localization);Set(feedbackView,"panel",panel.gameObject);Set(feedbackView,"message",message);
            Debug.Log($"Monitor authored center={geometry.Center} normal={geometry.Normal} size={geometry.Width}x{geometry.Height}; seat={anchor.position}.");
        }
    }
}
