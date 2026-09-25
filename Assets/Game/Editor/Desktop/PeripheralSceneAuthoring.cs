using System;
using GoLive.Desktop;
using GoLive.Editor.Items;
using GoLive.Items;
using GoLive.Localization;
using GoLive.PcBuilding;
using GoLive.Player;
using GoLive.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GoLive.Editor.Desktop
{
    // Explicit editor authoring only. The scene records the two connections and the same original desk microphone;
    // no runtime search, nearby-object inference, purchase history or decorative copy supplies a connection.
    public static class PeripheralSceneAuthoring
    {
        public const string WebcamDefinitionPath = "Assets/Game/Scripts/Items/Config/Item_UsedWebcam.asset";
        public const string WebcamPrefabPath = "Assets/Game/Prefab/Items/Item_UsedWebcam.prefab";
        private const string MicrophoneDefinitionPath = "Assets/Game/Scripts/Items/Config/Item_UsedMicrophone.asset";
        private const string WebcamModelPath = "Assets/Game/Prefab/1_Main Room/PC/Webcam/Webcam.prefab";
        private const string CatalogPath = "Assets/Game/Scripts/Shop/DefaultShopCatalog.asset";
        private const string InitialMicrophoneId = "a471382304f940a98e991212583c2cb6";

        [MenuItem("GO! LIVE/Desktop/Author peripheral readiness in GL")]
        public static void ApplyReadiness()
        {
            const string scenePath = "Assets/Game/Scenes/GL.unity";
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var runtime = One<DesktopRuntimeBehaviour>();
            var locale = One<LocalizationContext>();
            var previousView = One<StreamlyView>();
            var preview = new SerializedObject(previousView).FindProperty("previewCamera").objectReferenceValue;
            Transform body = previousView.transform;
            Object.DestroyImmediate(previousView);
            for (int i = body.childCount - 1; i >= 0; i--) Object.DestroyImmediate(body.GetChild(i).gameObject);
            var ui = new DesktopUiAuthoring(RequireAsset<TMP_FontAsset>("Assets/Game/UI/Fonts/Manrope/Manrope-Medium SDF.asset"), locale);
            DesktopBroadcastAuthoring.Streamly(ui, runtime, body);
            Set(body.GetComponent<StreamlyView>(), "previewCamera", preview);
            DesktopLocalizationAuthoring.Apply(locale);
            Apply(runtime, One<PlayerController>(), One<PlayerCarry>());
            EditorSceneManager.MarkSceneDirty(runtime.gameObject.scene);
            EditorSceneManager.SaveScene(runtime.gameObject.scene, scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Peripheral readiness authored while preserving the existing Desktop shell and other app objects.");
        }

        public static PcPeripheralsBehaviour Apply(DesktopRuntimeBehaviour runtime, PlayerController player, PlayerCarry carry)
        {
            if (Application.isPlaying || runtime == null || player == null || carry == null)
                throw new InvalidOperationException("Peripheral authoring requires the loaded edit-mode scene and explicit desktop/player references.");

            ItemDefinition microphone = RequireAsset<ItemDefinition>(MicrophoneDefinitionPath);
            SetKind(microphone, PcPeripheralKind.Microphone);
            ItemDefinition webcam = AuthorWebcam();
            AuthorFulfillment(webcam);

            PcScreenView screen = One<PcScreenView>();
            MonitorScreenGeometry geometry = MonitorScreenGeometry.Read(screen.GetComponent<MeshFilter>(), screen.GetComponent<Renderer>());
            PcPeripheralsBehaviour rig = FindRig();
            WorldItem initial = AuthorInitialMicrophone(microphone);
            Vector3 initialPosition = initial.transform.position;
            Quaternion initialRotation = initial.transform.rotation;
            Bounds microphoneBounds = VisibleBounds(initial.gameObject);
            Vector3 towardPlayer = Vector3.ProjectOnPlane(geometry.Normal, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, -towardPlayer).normalized;
            Quaternion facingPlayer = Quaternion.LookRotation(-towardPlayer, Vector3.up);

            // Targets sit in front of the devices, just above the desk, so neither the monitor nor the case blocks E.
            Vector3 micTarget = microphoneBounds.center + towardPlayer * .22f;
            micTarget.y = microphoneBounds.min.y - .018f;
            Vector3 webcamTarget = geometry.Center - right * (geometry.Width * .5f + .055f) + towardPlayer * .28f;
            webcamTarget.y = micTarget.y;
            // The keyboard and mug occupy the inner desk surface. Keep both controls on the free front edge.
            micTarget += towardPlayer * (.55f - Vector3.Dot(micTarget - geometry.Center, towardPlayer));
            webcamTarget += towardPlayer * (.55f - Vector3.Dot(webcamTarget - geometry.Center, towardPlayer));
            PcPeripheralSocket micSocket = AuthorSocket(rig, "Microphone connection", PcPeripheralKind.Microphone,
                micTarget, facingPlayer, initialPosition, initialRotation, "pc.peripheral.microphone");

            // A purchased webcam sits on the existing monitor casing. Its mount starts empty in a new game.
            Vector3 webcamPosition = geometry.Center - towardPlayer * .055f;
            webcamPosition.y = screen.GetComponent<Renderer>().bounds.max.y + .005f;
            PcPeripheralSocket camSocket = AuthorSocket(rig, "Webcam connection", PcPeripheralKind.Webcam,
                webcamTarget, facingPlayer, webcamPosition, facingPlayer, "pc.peripheral.webcam");

            initial.transform.SetParent(micSocket.InstallAnchor, true);
            initial.transform.localPosition = Vector3.zero;
            initial.transform.localRotation = Quaternion.identity;
            Set(rig, "microphoneSocket", micSocket);
            Set(rig, "webcamSocket", camSocket);
            Set(rig, "initialMicrophone", initial);
            Set(rig, "playerCarry", carry);
            Set(rig, "playerController", player);
            Set(runtime, "peripherals", rig);
            Dirty(initial.transform);
            Dirty(rig);
            Debug.Log($"PC peripherals authored: original microphone {initial.AuthoredInstanceId}; empty webcam mount; available used-webcam delivery.");
            return rig;
        }

        private static ItemDefinition AuthorWebcam()
        {
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(WebcamDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(definition, WebcamDefinitionPath);
            }
            SerializedObject data = new(definition);
            data.FindProperty("itemId").stringValue = "used-webcam";
            data.FindProperty("nameLocalizationKey").stringValue = "shop.product.used_webcam.name";
            data.FindProperty("category").intValue = (int)ItemCategory.Electronics;
            data.FindProperty("canStoreInInventory").boolValue = true;
            data.FindProperty("carryStyle").intValue = (int)ItemCarryStyle.OneHanded;
            data.FindProperty("carryLocalPosition").vector3Value = new Vector3(0, -.035f, 0);
            data.FindProperty("carryLocalEulerAngles").vector3Value = Vector3.zero;
            data.FindProperty("peripheralKind").intValue = (int)PcPeripheralKind.Webcam;
            data.FindProperty("pcComponent").objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();

            // Only build the owned runtime wrapper once. A second authoring pass keeps prefab GUIDs and file IDs.
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WebcamPrefabPath);
            if (prefab == null)
            {
                GameObject root = new("Item_UsedWebcam");
                try
                {
                    GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(RequireAsset<GameObject>(WebcamModelPath), root.transform);
                    model.name = "Visual";
                    model.transform.localPosition = Vector3.zero;
                    Bounds bounds = VisibleBounds(root);
                    float longestSide = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    if (longestSide < .0001f) throw new InvalidOperationException("Webcam model has no usable visible bounds.");
                    model.transform.localScale *= .14f / longestSide;
                    bounds = VisibleBounds(root);
                    model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    bounds = VisibleBounds(root);
                    BoxCollider collider = root.AddComponent<BoxCollider>();
                    collider.center = bounds.center;
                    collider.size = bounds.size;
                    Rigidbody body = root.AddComponent<Rigidbody>();
                    body.mass = .2f;
                    WorldItem item = root.AddComponent<WorldItem>();
                    Set(item, "definition", definition);
                    SetString(item, "authoredInstanceId", string.Empty);
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, WebcamPrefabPath);
                }
                finally { Object.DestroyImmediate(root); }
            }
            Set(definition, "worldPrefab", prefab);
            if (!definition.TryGetRuntimePrefab(out _))
                throw new InvalidOperationException("Used webcam must have a compatible runtime WorldItem prefab and collider.");
            if (definition.InventoryIcon == null &&
                !ItemIconGenerator.TryGenerate(definition, ItemIconGenerator.OutputFolder, out string error))
                throw new InvalidOperationException($"Could not render the real webcam's inventory icon: {error}");
            Dirty(definition);
            return definition;
        }

        private static void AuthorFulfillment(ItemDefinition webcam)
        {
            ShopCatalogConfig catalog = RequireAsset<ShopCatalogConfig>(CatalogPath);
            SerializedObject data = new(catalog);
            SerializedProperty products = data.FindProperty("products");
            for (int i = 0; i < products.arraySize; i++)
            {
                SerializedProperty product = products.GetArrayElementAtIndex(i);
                if (product.FindPropertyRelative("productId").stringValue != "used-webcam") continue;
                product.FindPropertyRelative("fulfillmentItem").objectReferenceValue = webcam;
                product.FindPropertyRelative("availability").intValue = (int)ShopProductAvailability.Available;
                data.ApplyModifiedPropertiesWithoutUndo();
                Dirty(catalog);
                return;
            }
            throw new InvalidOperationException("The existing used-webcam shop product is missing.");
        }

        private static PcPeripheralsBehaviour FindRig()
        {
            PcPeripheralsBehaviour[] rigs = Object.FindObjectsByType<PcPeripheralsBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (rigs.Length > 1) throw new InvalidOperationException("GL must have one explicitly authored peripheral rig.");
            // Keep this root separate from Desktop System, which DesktopSceneAuthoring rebuilds on each pass.
            return rigs.Length == 1 ? rigs[0] : new GameObject("PC Peripherals").AddComponent<PcPeripheralsBehaviour>();
        }

        private static WorldItem AuthorInitialMicrophone(ItemDefinition definition)
        {
            foreach (WorldItem item in Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (item.AuthoredInstanceId != InitialMicrophoneId) continue;
                if (item.Definition != definition) throw new InvalidOperationException("The authored desk microphone ID belongs to another item.");
                return item;
            }

            Transform visual = null;
            foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.name != "SM_Microphone_001" || candidate.GetComponentInParent<WorldItem>() != null) continue;
                if (visual != null) throw new InvalidOperationException("Expected one original decorative desk microphone in GL.");
                visual = candidate;
            }
            if (visual == null) throw new InvalidOperationException("The original decorative desk microphone was not found.");

            // Match the existing runtime microphone's coordinate system, preserving every visual transform exactly.
            // Its same original renderer/material remains in the scene; wrapping does not instantiate a second mic.
            Transform reference = definition.WorldPrefab.transform.GetChild(0);
            Quaternion rotation = visual.rotation * Quaternion.Inverse(reference.localRotation);
            Vector3 position = visual.position - rotation * reference.localPosition;
            GameObject root = new("Desk microphone");
            root.transform.SetPositionAndRotation(position, rotation);
            visual.SetParent(root.transform, true);
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = .5f;
            body.isKinematic = true;
            body.useGravity = false;
            WorldItem itemRoot = root.AddComponent<WorldItem>();
            Set(itemRoot, "definition", definition);
            SetString(itemRoot, "authoredInstanceId", InitialMicrophoneId);
            if (root.GetComponentInChildren<Collider>(true) == null)
                throw new InvalidOperationException("The original microphone requires its authored collider.");
            Dirty(visual);
            return itemRoot;
        }

        private static PcPeripheralSocket AuthorSocket(PcPeripheralsBehaviour rig, string name, PcPeripheralKind kind,
            Vector3 targetPosition, Quaternion targetRotation, Vector3 installPosition, Quaternion installRotation, string labelKey)
        {
            Transform root = EnsureChild(rig.transform, name);
            root.SetPositionAndRotation(targetPosition, targetRotation);
            PcPeripheralSocket socket = GetOrAdd<PcPeripheralSocket>(root.gameObject);
            BoxCollider collider = GetOrAdd<BoxCollider>(root.gameObject);
            collider.isTrigger = false;
            collider.center = Vector3.zero;
            collider.size = new Vector3(.16f, .058f, .024f);
            Transform anchor = EnsureChild(root, "Install Anchor");
            anchor.SetPositionAndRotation(installPosition, installRotation);
            Set(socket, "peripherals", rig);
            Set(socket, "installAnchor", anchor);
            Set(socket, "interactionCollider", collider);
            SerializedObject data = new(socket);
            data.FindProperty("kind").intValue = (int)kind;
            data.ApplyModifiedPropertiesWithoutUndo();
            AuthorLabel(root, labelKey);
            Dirty(root);
            Dirty(collider);
            return socket;
        }

        private static void AuthorLabel(Transform socket, string labelKey)
        {
            RectTransform plate = EnsureRect(socket, "Connection label");
            plate.localPosition = new Vector3(0, 0, -.013f);
            plate.localRotation = Quaternion.identity;
            plate.localScale = Vector3.one * .001f;
            plate.sizeDelta = new Vector2(160, 58);
            Canvas canvas = GetOrAdd<Canvas>(plate.gameObject);
            canvas.renderMode = RenderMode.WorldSpace;
            Image background = GetOrAdd<Image>(plate.gameObject);
            background.color = new Color(.065f, .085f, .105f, 1);
            background.raycastTarget = false;
            RectTransform caption = EnsureRect(plate, "Device name");
            caption.anchorMin = Vector2.zero;
            caption.anchorMax = Vector2.one;
            caption.offsetMin = new Vector2(4, 3);
            caption.offsetMax = new Vector2(-4, -3);
            TextMeshProUGUI text = GetOrAdd<TextMeshProUGUI>(caption.gameObject);
            text.font = RequireAsset<TMP_FontAsset>("Assets/Game/UI/Fonts/Manrope/Manrope-Medium SDF.asset");
            text.fontSize = 18;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(.8f, .94f, .97f, 1);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.text = labelKey == "pc.peripheral.microphone" ? "Микрофон" : "Веб-камера";
            LocalizedTextView localized = GetOrAdd<LocalizedTextView>(caption.gameObject);
            Set(localized, "_localization", One<LocalizationContext>());
            SetString(localized, "_key", labelKey);
            Dirty(text);
        }

        private static Bounds VisibleBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            bool found = false;
            Bounds bounds = default;
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled || renderer is not (MeshRenderer or SkinnedMeshRenderer)) continue;
                if (found) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; found = true; }
            }
            if (!found) throw new InvalidOperationException($"{root.name} has no visible model.");
            return bounds;
        }

        private static void SetKind(ItemDefinition definition, PcPeripheralKind kind)
        {
            SerializedObject data = new(definition);
            data.FindProperty("peripheralKind").intValue = (int)kind;
            data.FindProperty("pcComponent").objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();
            Dirty(definition);
        }

        private static T RequireAsset<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"Missing required {typeof(T).Name}: {path}");

        private static T One<T>() where T : Object
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return found.Length == 1 ? found[0] : throw new InvalidOperationException($"Expected one {typeof(T).Name}, found {found.Length}.");
        }

        private static T GetOrAdd<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            return component != null ? component : owner.AddComponent<T>();
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child;
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static RectTransform EnsureRect(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return (RectTransform)child;
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Set(Object owner, string field, Object value)
        {
            SerializedObject data = new(owner);
            SerializedProperty property = data.FindProperty(field) ?? throw new InvalidOperationException($"Missing {owner.GetType().Name}.{field}.");
            property.objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
            Dirty(owner);
        }

        private static void SetString(Object owner, string field, string value)
        {
            SerializedObject data = new(owner);
            data.FindProperty(field).stringValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
            Dirty(owner);
        }

        private static void Dirty(Object target)
        {
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
