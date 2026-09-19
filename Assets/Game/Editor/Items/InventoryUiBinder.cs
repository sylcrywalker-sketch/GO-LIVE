using System;
using GoLive.Inventory;
using GoLive.Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Editor.Inventory
{
    public static class InventoryUiBinder
    {
        private const int SlotCount = 12;
        private const int CategoryCount = 4;

        [MenuItem("GO LIVE/Inventory/Bind Migrated Inventory UI")]
        public static void Bind()
        {
            RectTransform inventoryRoot = FindInventoryRoot();

            if (inventoryRoot == null)
                throw new InvalidOperationException("InventoryUI was not found in the loaded scene.");

            PlayerInventory playerInventory = FindSingle<PlayerInventory>();
            PlayerCarry playerCarry = playerInventory.GetComponent<PlayerCarry>();
            PlayerController playerController = playerInventory.GetComponent<PlayerController>();

            if (playerCarry == null || playerController == null)
                throw new InvalidOperationException("Player requires PlayerCarry and PlayerController.");

            InventoryUiController controller = inventoryRoot.GetComponent<InventoryUiController>();

            if (controller == null)
                controller = Undo.AddComponent<InventoryUiController>(inventoryRoot.gameObject);

            Transform overlay = Require(inventoryRoot, "InventoryOverlay");
            Transform panel = Require(overlay, "InventoryComposition");
            Transform grid = Require(panel, "GridViewport/InventoryGrid");
            Transform details = Require(panel, "ItemDetails");
            Transform held = Require(panel, "HeldItem");

            Button[] slotButtons = new Button[SlotCount];
            Image[] slotIcons = new Image[SlotCount];
            TMP_Text[] slotNames = new TMP_Text[SlotCount];
            TMP_Text[] slotQuantities = new TMP_Text[SlotCount];
            TMP_Text[] slotFallbacks = new TMP_Text[SlotCount];
            Image[] slotSelections = new Image[SlotCount];

            for (int i = 0; i < SlotCount; i++)
            {
                Transform slot = Require(grid, $"Slot_{i + 1:00}");

                slotButtons[i] = GetOrAddButton(slot);
                slotIcons[i] = RequireComponent<Image>(slot, "Icon");
                slotNames[i] = RequireComponent<TMP_Text>(slot, "ItemName");
                slotQuantities[i] = RequireComponent<TMP_Text>(slot, "Quantity");
                slotFallbacks[i] = RequireComponent<TMP_Text>(slot, "MissingIcon");
                slotSelections[i] = RequireComponent<Image>(slot, "Selection");
            }

            Button[] categoryButtons = new Button[CategoryCount];
            Image[] categoryFocusLines = new Image[CategoryCount];

            for (int i = 0; i < CategoryCount; i++)
            {
                Transform category = Require(panel, $"Category_{i}");

                categoryButtons[i] = GetOrAddButton(category);
                categoryFocusLines[i] = RequireComponent<Image>(category, "KeyboardFocus");
            }

            SerializedObject serialized = new(controller);

            serialized.FindProperty("playerInventory").objectReferenceValue = playerInventory;
            serialized.FindProperty("playerCarry").objectReferenceValue = playerCarry;
            serialized.FindProperty("playerController").objectReferenceValue = playerController;

            serialized.FindProperty("overlay").objectReferenceValue = overlay.GetComponent<CanvasGroup>();
            serialized.FindProperty("inventoryHintText").objectReferenceValue = RequireComponent<TMP_Text>(inventoryRoot, "InventoryHint");

            serialized.FindProperty("capacityText").objectReferenceValue = RequireComponent<TMP_Text>(panel, "Capacity");
            serialized.FindProperty("emptyText").objectReferenceValue = RequireComponent<TMP_Text>(panel, "GridViewport/EmptyState");

            SetArray(serialized.FindProperty("slotButtons"), slotButtons);
            SetArray(serialized.FindProperty("slotIcons"), slotIcons);
            SetArray(serialized.FindProperty("slotNames"), slotNames);
            SetArray(serialized.FindProperty("slotQuantities"), slotQuantities);
            SetArray(serialized.FindProperty("slotIconFallbacks"), slotFallbacks);
            SetArray(serialized.FindProperty("slotSelections"), slotSelections);

            SetArray(serialized.FindProperty("categoryButtons"), categoryButtons);
            SetArray(serialized.FindProperty("categoryFocusLines"), categoryFocusLines);

            serialized.FindProperty("itemIcon").objectReferenceValue = RequireComponent<Image>(details, "ItemIcon");
            serialized.FindProperty("itemIconFallback").objectReferenceValue = RequireComponent<TMP_Text>(details, "MissingIcon");
            serialized.FindProperty("itemNameText").objectReferenceValue = RequireComponent<TMP_Text>(details, "TitleViewport/ItemName");
            serialized.FindProperty("categoryText").objectReferenceValue = RequireComponent<TMP_Text>(details, "ItemCategory");
            serialized.FindProperty("descriptionText").objectReferenceValue = RequireComponent<TMP_Text>(details, "DescriptionViewport/ItemDescription");
            serialized.FindProperty("propertiesText").objectReferenceValue = RequireComponent<TMP_Text>(details, "ItemProperties");

            serialized.FindProperty("takeButton").objectReferenceValue = GetOrAddButton(Require(details, "Take"));
            serialized.FindProperty("dropSelectedButton").objectReferenceValue = GetOrAddButton(Require(details, "Drop"));

            serialized.FindProperty("heldPanel").objectReferenceValue = held.gameObject;
            serialized.FindProperty("heldIcon").objectReferenceValue = RequireComponent<Image>(held, "HeldIcon");
            serialized.FindProperty("heldNameText").objectReferenceValue = RequireComponent<TMP_Text>(held, "HeldName");
            serialized.FindProperty("heldInfoText").objectReferenceValue = RequireComponent<TMP_Text>(held, "HeldInfo");
            serialized.FindProperty("storeHeldButton").objectReferenceValue = GetOrAddButton(Require(held, "StoreHeld"));
            serialized.FindProperty("useHeldButton").objectReferenceValue = GetOrAddButton(Require(held, "UseHeld"));
            serialized.FindProperty("dropHeldButton").objectReferenceValue = GetOrAddButton(Require(held, "DropHeld"));

            serialized.FindProperty("closeButton").objectReferenceValue = GetOrAddButton(Require(panel, "Close"));

            serialized.ApplyModifiedProperties();

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(inventoryRoot.gameObject.scene);

            Selection.activeGameObject = inventoryRoot.gameObject;
            EditorGUIUtility.PingObject(inventoryRoot.gameObject);

            Debug.Log("Migrated Inventory UI binding completed. Assign Inventory Action and Cancel Action on InventoryUiController.", controller);
        }

        private static RectTransform FindInventoryRoot()
        {
            RectTransform[] rects = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            RectTransform result = null;

            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].name != "InventoryUI")
                    continue;

                if (result != null)
                    throw new InvalidOperationException("More than one InventoryUI exists in the loaded scene.");

                result = rects[i];
            }

            return result;
        }

        private static T FindSingle<T>() where T : UnityEngine.Object
        {
            T[] objects = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (objects.Length != 1)
                throw new InvalidOperationException($"Expected exactly one {typeof(T).Name}, found {objects.Length}.");

            return objects[0];
        }

        private static Transform Require(Transform root, string path)
        {
            Transform result = root.Find(path);

            if (result == null)
                throw new InvalidOperationException($"Required UI object was not found: {root.name}/{path}");

            return result;
        }

        private static T RequireComponent<T>(Transform root, string path) where T : Component
        {
            Transform target = Require(root, path);
            T component = target.GetComponent<T>();

            if (component == null)
                throw new InvalidOperationException($"{target.name} requires {typeof(T).Name}.");

            return component;
        }

        private static Button GetOrAddButton(Transform target)
        {
            Button button = target.GetComponent<Button>();

            if (button == null)
                button = Undo.AddComponent<Button>(target.gameObject);

            button.targetGraphic = target.GetComponent<Graphic>();
            button.transition = Selectable.Transition.None;

            return button;
        }

        private static void SetArray<T>(SerializedProperty property, T[] values) where T : UnityEngine.Object
        {
            property.arraySize = values.Length;

            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}