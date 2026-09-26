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
        private const string AudienceTuningPath="Assets/Game/Config/Desktop/AudienceTuning.asset";
        private const string VoiceActivityPath="Assets/Game/Config/Voice/VoiceActivity.asset";
        private const string ViewerCorePath="Assets/Game/Config/Viewers/ViewerCore.asset";

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
            Set(runtime,"clock",One<GoLive.GameTime.GameClockBehaviour>());Set(runtime,"wallet",One<GoLive.Economy.WalletBehaviour>());
            Set(runtime,"audienceTuning",Config<AudienceTuningConfig>(AudienceTuningPath));
            Set(runtime,"viewerCore",Config<GoLive.Viewers.ViewerCoreConfig>(ViewerCorePath));
            Set(screen,"session",session);Set(workbench,"session",session);Set(One<GameUiInputRouter>(),"pcSession",session);
            Set(One<GameSaveController>(),"_desktop",runtime);
            var caseInteractable=pc.GetComponent<PcCaseInteractable>();
            if(caseInteractable==null) caseInteractable=pc.gameObject.AddComponent<PcCaseInteractable>();
            Set(caseInteractable,"session",session);
            var monitorInteractable=screen.GetComponent<PcMonitorInteractable>();
            if(monitorInteractable==null) monitorInteractable=screen.gameObject.AddComponent<PcMonitorInteractable>();
            Set(monitorInteractable,"session",session);
            var body=screen.GetComponent<Rigidbody>();if(body!=null) body.isKinematic=true;
            DesktopShellAuthoring.Build(ui,root.transform,runtime,shellView,catalog,One<GoLive.GameTime.GameClockBehaviour>());
            DesktopBroadcastAuthoring.Overlay(ui,runtime,root.transform);
            BuildMonitor(ui,root.transform,screen,session,runtime,catalog);
            BuildCaptureSource(root,runtime);
            PeripheralSceneAuthoring.Apply(runtime,player,carry);
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
        // Production configs are authored assets: authoring assigns them and never creates a substitute.
        private static T Config<T>(string path) where T:ScriptableObject
        {
            var config=AssetDatabase.LoadAssetAtPath<T>(path);
            if(config==null) throw new InvalidOperationException($"Missing production config {path}.");
            return config;
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
                var platform=importer.GetDefaultPlatformTextureSettings();
                platform.format=TextureImporterFormat.RGBA32;importer.SetPlatformTextureSettings(platform);
                importer.maxTextureSize=256;importer.alphaIsTransparency=true;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
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
        // The broadcast source is the displayed desktop itself, shared by Streamly preview and the live stream.
        private static void BuildCaptureSource(GameObject root,DesktopRuntimeBehaviour runtime)
        {
            var capture=root.AddComponent<DesktopCaptureSource>();
            Set(capture,"runtime",runtime);
            Set(One<StreamlyView>(),"capture",capture);
            // The player's real OS microphone: independent of the in-game microphone item.
            var voiceObject=new GameObject("Player voice input (OS microphone)");voiceObject.transform.SetParent(root.transform,false);
            var voice=voiceObject.AddComponent<GoLive.Voice.VoiceInputBehaviour>();
            Set(voice,"localization",One<LocalizationContext>());
            Set(voice,"activity",Config<GoLive.Voice.VoiceActivityConfig>(VoiceActivityPath));
            Set(runtime,"voice",voice);Set(One<StreamlyView>(),"voice",voice);
        }
    }
}
