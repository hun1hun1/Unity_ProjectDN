using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MapChoiceCardSetup
{
    [MenuItem("ProjectDN/Update Map Choice Cards")]
    public static void UpdateScene()
    {
        var scene = EditorSceneManager.OpenScene(MapAssetSetup.MapScenePath);
        MapController map = UnityEngine.Object.FindFirstObjectByType<MapController>();
        if (map == null) throw new InvalidOperationException("MapController is missing.");
        Configure(map);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    public static void Configure(MapController map)
    {
        var config = new SerializedObject(map);
        Transform row = ((UnityEngine.UI.Button)config.FindProperty("choiceButtons").GetArrayElementAtIndex(0).objectReferenceValue).transform.parent;
        Transform content = row.parent;
        TMP_FontAsset font = ((TMP_Text)config.FindProperty("heading").objectReferenceValue).font;

        // Preserve the existing scene objects, button events and data references.
        Remove<UnityEngine.UI.VerticalLayoutGroup>(content);
        Place(content.Find("Heading"), new Vector2(1200, 72), new Vector2(0, 365));
        Place(content.Find("Progress"), new Vector2(1200, 40), new Vector2(0, 308));
        if (config.FindProperty("goldText").objectReferenceValue == null)
            Place(content.Find("Resources"), new Vector2(1200, 40), new Vector2(0, 265));
        Place(content.Find("Status"), new Vector2(1200, 48), new Vector2(0, -268));
        ConfigureProgress(content, (Transform)config.FindProperty("nodeContainer").objectReferenceValue);
        Place(row, new Vector2(1250, 440), Vector2.zero);
        Remove<UnityEngine.UI.VerticalLayoutGroup>(row);
        var horizontal = Ensure<UnityEngine.UI.HorizontalLayoutGroup>(row);
        horizontal.spacing = 28; horizontal.padding = new RectOffset(); horizontal.childAlignment = TextAnchor.MiddleCenter;
        horizontal.childControlWidth = horizontal.childControlHeight = false;
        horizontal.childForceExpandWidth = horizontal.childForceExpandHeight = false;

        var presentations = config.FindProperty("choicePresentations");
        if (presentations.arraySize == 0)
        {
            MapContentType[] types = (MapContentType[])Enum.GetValues(typeof(MapContentType));
            presentations.arraySize = types.Length;
            Sprite normalArtwork = AssetDatabase.LoadAllAssetsAtPath("Assets/Images/UI/TestEnemy.png").OfType<Sprite>().FirstOrDefault();
            for (int i = 0; i < types.Length; i++)
            {
                var entry = presentations.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("type").intValue = (int)types[i];
                entry.FindPropertyRelative("title").stringValue = MapChoiceCardUI.DefaultTitle(types[i]);
                entry.FindPropertyRelative("description").stringValue = MapChoiceCardUI.DefaultDescription(types[i]);
                entry.FindPropertyRelative("artwork").objectReferenceValue = types[i] == MapContentType.NormalBattle ? normalArtwork : null;
            }
            config.ApplyModifiedPropertiesWithoutUndo();
        }
        string previewTitle = MapChoiceCardUI.DefaultTitle(MapContentType.NormalBattle);
        string previewDescription = MapChoiceCardUI.DefaultDescription(MapContentType.NormalBattle);
        Sprite previewArtwork = null;
        for (int i = 0; i < presentations.arraySize; i++)
        {
            var entry = presentations.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("type").intValue != (int)MapContentType.NormalBattle) continue;
            string title = entry.FindPropertyRelative("title").stringValue;
            string details = entry.FindPropertyRelative("description").stringValue;
            if (!string.IsNullOrWhiteSpace(title)) previewTitle = title;
            if (!string.IsNullOrWhiteSpace(details)) previewDescription = details;
            previewArtwork = (Sprite)entry.FindPropertyRelative("artwork").objectReferenceValue;
            break;
        }

        var buttons = config.FindProperty("choiceButtons");
        for (int i = 0; i < buttons.arraySize; i++)
        {
            var button = (UnityEngine.UI.Button)buttons.GetArrayElementAtIndex(i).objectReferenceValue;
            Transform root = button.transform;
            Place(root, new Vector2(360, 440), Vector2.zero);
            Preferred(root, 360, 440);
            if (root.Find("DecorativeFrame") == null)
            {
                var background = root.GetComponent<UnityEngine.UI.Image>();
                background.color = new Color(.12f, .18f, .27f, 1);
                var outline = Ensure<UnityEngine.UI.Outline>(root);
                outline.effectColor = new Color(.31f, .43f, .59f, .85f);
                outline.effectDistance = new Vector2(2, -2);
            }
            var vertical = Ensure<UnityEngine.UI.VerticalLayoutGroup>(root);
            vertical.padding = new RectOffset(20, 20, 20, 20); vertical.spacing = 12; vertical.childAlignment = TextAnchor.MiddleCenter;
            vertical.childControlWidth = vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true; vertical.childForceExpandHeight = false;

            Transform artFrame = Child(root, "ArtworkFrame"); artFrame.SetSiblingIndex(0);
            Preferred(artFrame, 320, 220);
            var frameImage = Ensure<UnityEngine.UI.Image>(artFrame);
            frameImage.color = new Color(.055f, .085f, .14f, 1); frameImage.raycastTarget = false;
            Transform art = Child(artFrame, "Artwork"); Stretch(art, 12);
            var image = Ensure<UnityEngine.UI.Image>(art);
            image.preserveAspect = true; image.raycastTarget = false; image.color = Color.white;
            TMP_Text placeholder = Text(artFrame, "ArtworkPlaceholder", font, 26); Stretch(placeholder.transform, 12);
            placeholder.text = "아트워크"; placeholder.color = new Color(.55f, .64f, .77f);

            Transform previousTitle = root.Find("Title") ?? root.Find("Text");
            if (previousTitle != null) previousTitle.name = "Title";
            TMP_Text name = Text(root, "Title", font, 34); name.transform.SetSiblingIndex(1);
            Preferred(name.transform, 320, 48); name.fontSizeMin = 22;
            TMP_Text description = Text(root, "Description", font, 24); description.transform.SetSiblingIndex(2);
            Preferred(description.transform, 320, 84); description.fontSizeMin = 18;
            MapChoiceCardUI card = Ensure<MapChoiceCardUI>(root);
            Bind(card, "artwork", image); Bind(card, "artworkPlaceholder", placeholder);
            Bind(card, "title", name); Bind(card, "description", description);
            root.Find("Nameplate")?.SetAsFirstSibling();
            card.SetContent(previewTitle, previewDescription, previewArtwork, true, true);
        }
    }
    private static void ConfigureProgress(Transform parent, Transform nodes)
    {
        Transform root = Child(parent, "ProgressScrollView");
        Place(root, new Vector2(1200, 86), new Vector2(0, -344));
        Transform viewport = Child(root, "Viewport"); Stretch(viewport, 0);
        Ensure<UnityEngine.UI.RectMask2D>(viewport);
        var background = Ensure<UnityEngine.UI.Image>(viewport); background.color = Color.clear; background.raycastTarget = true;
        nodes.SetParent(viewport, false);
        var content = (RectTransform)nodes;
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1);
        content.sizeDelta = new Vector2(1200, 86); content.anchoredPosition = Vector2.zero;
        Remove<UnityEngine.UI.LayoutElement>(nodes);
        var grid = nodes.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        grid.cellSize = new Vector2(100, 86); grid.childAlignment = TextAnchor.UpperCenter;
        var fit = Ensure<UnityEngine.UI.ContentSizeFitter>(nodes);
        fit.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
        fit.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        var scroll = Ensure<UnityEngine.UI.ScrollRect>(root);
        scroll.content = content; scroll.viewport = (RectTransform)viewport;
        scroll.horizontal = false; scroll.vertical = true; scroll.inertia = false;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 100;
        Transform bar = Child(root, "Scrollbar"); var rect = (RectTransform)bar;
        rect.anchorMin = new Vector2(1, 0); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(1, .5f);
        rect.sizeDelta = new Vector2(12, 0); rect.anchoredPosition = new Vector2(-4, 0);
        var barImage = Ensure<UnityEngine.UI.Image>(bar); barImage.color = new Color(.08f, .12f, .2f);
        Transform sliding = Child(bar, "SlidingArea"); Stretch(sliding, 2);
        Transform handle = Child(sliding, "Handle"); Stretch(handle, 0);
        var handleImage = Ensure<UnityEngine.UI.Image>(handle); handleImage.color = new Color(.38f, .5f, .68f);
        var scrollbar = Ensure<UnityEngine.UI.Scrollbar>(bar);
        scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = (RectTransform)handle; scrollbar.targetGraphic = handleImage;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
    }
    private static T Ensure<T>(Transform target) where T : Component => target.GetComponent<T>() ?? target.gameObject.AddComponent<T>();
    private static void Remove<T>(Transform target) where T : Component
    { T component = target.GetComponent<T>(); if (component != null) UnityEngine.Object.DestroyImmediate(component); }
    private static Transform Child(Transform parent, string name)
    {
        Transform result = parent.Find(name);
        if (result == null) { result = new GameObject(name, typeof(RectTransform)).transform; result.SetParent(parent, false); }
        return result;
    }
    private static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size)
    {
        var label = Ensure<TextMeshProUGUI>(Child(parent, name));
        label.font = font; label.fontSize = label.fontSizeMax = size; label.enableAutoSizing = true;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        label.color = new Color(.9f, .93f, .98f); return label;
    }
    private static void Place(Transform target, Vector2 size, Vector2 position)
    {
        var rect = (RectTransform)target; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size; rect.anchoredPosition = position;
    }
    private static void Preferred(Transform target, float width, float height)
    {
        var element = Ensure<UnityEngine.UI.LayoutElement>(target);
        element.minWidth = element.preferredWidth = width; element.minHeight = element.preferredHeight = height;
        element.flexibleWidth = element.flexibleHeight = 0;
    }
    private static void Stretch(Transform target, float margin)
    {
        var rect = (RectTransform)target; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(margin, margin); rect.offsetMax = new Vector2(-margin, -margin);
    }
    private static void Bind(UnityEngine.Object target, string name, UnityEngine.Object value)
    { var config = new SerializedObject(target); config.FindProperty(name).objectReferenceValue = value; config.ApplyModifiedPropertiesWithoutUndo(); }
}
