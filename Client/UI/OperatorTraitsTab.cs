using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Ragfair;
using Newtonsoft.Json;
using SPT.Common.Http;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OperatorTraits
{
    internal static class OperatorTraitsTab
    {
        private const string TabObjectName = "OperatorTraitsTab";
        private const string PanelObjectName = "OperatorTraitsPanel";
        private static Sprite _tabIcon;

        internal static bool TryAttach(InventoryScreen screen)
        {
            if (screen == null)
                return false;
            Transform menuRoot = screen.transform;
            if (FindDeep(menuRoot, TabObjectName) != null)
                return true;

            if (screen._tabDictionary == null ||
                !screen._tabDictionary.TryGetValue(EInventoryTab.Skills, out Tab skillsTab) ||
                skillsTab == null || screen._tasksScreen == null)
                return false;

            GameObject tabObject = UnityEngine.Object.Instantiate(
                skillsTab.gameObject,
                skillsTab.transform.parent,
                false);
            tabObject.name = TabObjectName;
            tabObject.transform.SetSiblingIndex(
                skillsTab.transform.GetSiblingIndex());

            Tab clonedTab = tabObject.GetComponent<Tab>();
            RemoveCopiedLocalization(tabObject, clonedTab);
            SetAllLabels(tabObject, "TRAITS");

            GameObject panel = CreatePanel(screen, skillsTab);
            clonedTab.Init(new TraitsTabController(panel));
            SetCustomIcon(clonedTab);
            clonedTab.UpdateVisual(false);
            clonedTab.SetInteractable(true);
            RegisterTab(screen, skillsTab, clonedTab);
            InstallRowLayout(screen, skillsTab.transform.parent);

            Plugin.Log.LogInfo("Added Operator Traits after Health and before Skills.");
            return true;
        }

        private static void RegisterTab(
            InventoryScreen screen,
            Tab skillsTab,
            Tab traitsTab)
        {
            const EInventoryTab traitsKey = (EInventoryTab)8;
            var tabs = new Dictionary<EInventoryTab, Tab>();

            foreach (KeyValuePair<EInventoryTab, Tab> entry in screen._tabDictionary)
            {
                if (entry.Value == skillsTab)
                    tabs.Add(traitsKey, traitsTab);
                tabs.Add(entry.Key, entry.Value);
            }

            screen._tabDictionary = tabs;
        }

        private static void InstallRowLayout(
            InventoryScreen screen,
            Transform tabRow)
        {
            TraitsTabRowLayout layout =
                tabRow.gameObject.GetComponent<TraitsTabRowLayout>() ??
                tabRow.gameObject.AddComponent<TraitsTabRowLayout>();
            layout.Initialize(screen._tabDictionary.Values);
        }

        private static void SetAllLabels(GameObject root, string value)
        {
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
                if (!string.IsNullOrWhiteSpace(label.text))
                {
                    label.text = value;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 10f;
                    label.fontSizeMax = label.fontSize;
                    label.enableWordWrapping = false;
                }
        }

        private static void RemoveCopiedLocalization(GameObject tabObject, Tab tab)
        {
            // A cloned Tarkov tab keeps the Tasks localization subscription.
            // Disabling it is not sufficient because its queued locale refresh can
            // still replace our TMP text after the clone appears.
            foreach (LocalizedText localizedText in
                     tabObject.GetComponentsInChildren<LocalizedText>(true))
                UnityEngine.Object.DestroyImmediate(localizedText);

            tab.LocalizedText = null;
        }

        private static void SetCustomIcon(Tab tab)
        {
            if (tab == null)
                return;

            if (_tabIcon == null)
                _tabIcon = LoadEmbeddedIcon();
            if (_tabIcon == null)
                return;

            var iconImages = new HashSet<Image>();
            CollectVersionIcons(tab._normalVersion, iconImages);
            CollectVersionIcons(tab._selectedVersion, iconImages);

            // Some tab prefabs keep the icon outside the two state roots. Only
            // use _targetImage when it is square; wide target images are the
            // hover/background graphic and must retain their native sprites.
            if (IsIconImage(tab._targetImage))
                iconImages.Add(tab._targetImage);

            foreach (Image image in iconImages)
            {
                image.sprite = _tabIcon;
                image.overrideSprite = _tabIcon;
                image.preserveAspect = true;
                image.color = new Color(
                    image.color.r, image.color.g, image.color.b, 1f);
                PersistentTabIcon guard =
                    image.gameObject.GetComponent<PersistentTabIcon>() ??
                    image.gameObject.AddComponent<PersistentTabIcon>();
                guard.Initialize(image, _tabIcon);
            }

            Plugin.Log.LogInfo(
                $"Applied Operator Traits icon to {iconImages.Count} tab image(s)." );
        }

        private static void CollectVersionIcons(
            GameObject version,
            ISet<Image> output)
        {
            if (version == null)
                return;

            foreach (Image image in version.GetComponentsInChildren<Image>(true))
                if (IsIconImage(image))
                    output.Add(image);
        }

        private static bool IsIconImage(Image image)
        {
            if (image == null || image.sprite == null)
                return false;

            RectTransform rect = image.rectTransform;
            float width = Mathf.Abs(rect.rect.width);
            float height = Mathf.Abs(rect.rect.height);
            if (width < 8f || height < 8f || width > 72f || height > 72f)
                return false;

            float ratio = width / height;
            return ratio >= 0.6f && ratio <= 1.67f;
        }

        private static Sprite LoadEmbeddedIcon()
        {
            const string resourceName =
                "OperatorTraits.Icons.OperatorTraitsTabIcon.png";
            using (Stream stream = typeof(OperatorTraitsTab).Assembly
                       .GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Plugin.Log.LogError(
                        $"Embedded tab icon not found: {resourceName}");
                    return null;
                }

                byte[] bytes = new byte[stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read == 0)
                        break;
                    offset += read;
                }

                var texture = new Texture2D(
                    2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes, false))
                {
                    Plugin.Log.LogError("Could not decode embedded tab icon.");
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                texture.name = "OperatorTraitsTabIcon";
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                sprite.name = "OperatorTraitsTabIcon";
                return sprite;
            }
        }

        private static GameObject CreatePanel(
            InventoryScreen screen,
            Tab tasksTab)
        {
            Transform tasksScreen = screen._tasksScreen.transform;
            GameObject panel = new GameObject(
                PanelObjectName,
                typeof(RectTransform),
                typeof(CanvasGroup));
            panel.transform.SetParent(tasksScreen.parent, false);
            panel.transform.SetSiblingIndex(tasksScreen.GetSiblingIndex());

            RectTransform rect = (RectTransform)panel.transform;
            RectTransform nativeContent =
                screen._skillsAndMasteringScreen.transform as RectTransform;
            rect.anchorMin = nativeContent.anchorMin;
            rect.anchorMax = nativeContent.anchorMax;
            rect.pivot = nativeContent.pivot;
            rect.anchoredPosition = nativeContent.anchoredPosition;
            rect.sizeDelta = nativeContent.sizeDelta;

            TMP_Text template = tasksTab
                .GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault();
            TMP_Text pointsLabel = CreateScreenHeader(panel.transform, template);

            TraitSelectionController selection =
                panel.AddComponent<TraitSelectionController>();
            selection.Initialize(pointsLabel, screen);
            CreatePerkColumn(panel.transform, template, true, selection);
            CreatePerkColumn(panel.transform, template, false, selection);
            OwnedTraitsLayout ownedLayout =
                panel.AddComponent<OwnedTraitsLayout>();
            ownedLayout.Initialize();
            Button confirmButton = CreateActionButton(
                panel.transform, template, "CONFIRM", false);
            Button resetButton = CreateActionButton(
                panel.transform, template,
                "RESET TRAITS     <color=#d7b85a>50 GP</color>", true);
            selection.FinalizeSetup(confirmButton, resetButton, ownedLayout);
            CreateBackButton(panel.transform, screen._backButton);

            panel.SetActive(false);
            panel.transform.SetAsLastSibling();
            return panel;
        }

        private static Button CreateActionButton(
            Transform parent,
            TMP_Text textTemplate,
            string label,
            bool hidden)
        {
            GameObject buttonObject = new GameObject(
                hidden ? "ResetTraitsButton" : "ConfirmTraitsButton",
                typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.name = hidden ? "ResetTraitsButton" : "ConfirmTraitsButton";

            RectTransform rect = buttonObject.transform as RectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 24f);
            rect.sizeDelta = new Vector2(330f, 56f);

            Image background = buttonObject.GetComponent<Image>();
            background.color = new Color(0.11f, 0.12f, 0.12f, 0.98f);
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.38f, 0.38f, 0.38f, 0.72f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            TMP_Text buttonLabel = CreateFlowLabel(
                buttonObject.transform, textTemplate, label, 20f,
                new Color(0.82f, 0.82f, 0.76f, 1f));
            buttonLabel.alignment = TextAlignmentOptions.Midline;
            buttonLabel.enableAutoSizing = true;
            buttonLabel.fontSizeMin = 13f;
            RectTransform labelRect = buttonLabel.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 5f);
            labelRect.offsetMax = new Vector2(-12f, -5f);

            buttonObject.SetActive(!hidden);
            return button;
        }

        private static void CreatePerkColumn(
            Transform parent,
            TMP_Text template,
            bool positive,
            TraitSelectionController selection)
        {
            GameObject column = new GameObject(
                positive ? "PositivePerks" : "NegativePerks",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(ScrollRect));
            column.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)column.transform;
            rect.anchorMin = new Vector2(positive ? 0.03f : 0.515f, 0.10f);
            rect.anchorMax = new Vector2(positive ? 0.485f : 0.97f, 0.79f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image background = column.GetComponent<Image>();
            background.color = new Color(0.055f, 0.06f, 0.062f, 0.94f);

            CreateColumnLabel(column.transform, template,
                positive ? "POSITIVE TRAITS" : "NEGATIVE TRAITS",
                24f, 14f, 10f, 44f,
                positive
                    ? new Color(0.58f, 0.72f, 0.53f, 1f)
                    : new Color(0.75f, 0.48f, 0.43f, 1f));

            IReadOnlyList<TraitDefinition> traits = positive
                ? TraitCatalog.Strengths
                : TraitCatalog.Scars;
            GameObject viewport = new GameObject("Viewport",
                typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(column.transform, false);
            RectTransform viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.offsetMin = new Vector2(8f, 8f);
            viewportRect.offsetMax = new Vector2(-8f, -54f);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            var list = content.AddComponent<VerticalLayoutGroup>();
            list.spacing = 8f;
            list.padding = new RectOffset(4, 4, 4, 4);
            list.childControlWidth = true;
            list.childControlHeight = true;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = column.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;
            scroll.enabled = false;
            viewport.AddComponent<OwnedColumnScroller>()
                .Initialize(contentRect);
            Transform currentRow = null;
            for (int index = 0; index < traits.Count; index++)
            {
                if (index % 2 == 0)
                    currentRow = CreateTraitRow(content.transform, index / 2);
                CreatePerkRow(currentRow, template, positive, index,
                    traits[index], selection);
            }
        }

        private static Transform CreateTraitRow(Transform parent, int index)
        {
            GameObject row = new GameObject($"TraitRow{index + 1}",
                typeof(RectTransform), typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 132f;
            HorizontalLayoutGroup layout =
                row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return row.transform;
        }

        private static void CreatePerkRow(
            Transform parent,
            TMP_Text template,
            bool positive,
            int index,
            TraitDefinition trait,
            TraitSelectionController selection)
        {
            GameObject row = new GameObject(
                $"{(positive ? "Positive" : "Negative")}Trait{index + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            row.transform.SetParent(parent, false);

            Image image = row.GetComponent<Image>();
            image.color = new Color(0.11f, 0.12f, 0.12f, 0.96f);
            Button button = row.GetComponent<Button>();
            button.targetGraphic = image;
            LayoutElement cardSize = row.AddComponent<LayoutElement>();
            cardSize.flexibleWidth = 1f;
            cardSize.minWidth = 0f;
            cardSize.preferredWidth = 0f;

            var cardLayout = row.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(7, 7, 6, 7);
            cardLayout.spacing = 5f;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = false;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;

            GameObject header = new GameObject("Header",
                typeof(RectTransform), typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            header.transform.SetParent(row.transform, false);
            header.GetComponent<LayoutElement>().preferredHeight = 27f;
            HorizontalLayoutGroup headerLayout =
                header.GetComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 5f;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = true;

            TMP_Text titleLabel = CreateFlowLabel(header.transform, template,
                trait.Name, 17f,
                new Color(0.82f, 0.82f, 0.76f, 1f));
            LayoutElement titleLayout =
                titleLabel.gameObject.AddComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1f;
            titleLayout.minWidth = 0f;
            titleLayout.preferredWidth = 0f;
            titleLabel.overflowMode = TextOverflowModes.Ellipsis;

            TMP_Text costLabel = CreateFlowLabel(header.transform, template,
                positive ? $"-{trait.Points}" : $"+{trait.Points}",
                17f,
                positive
                    ? new Color(0.78f, 0.57f, 0.48f, 1f)
                    : new Color(0.58f, 0.76f, 0.52f, 1f));
            costLabel.alignment = TextAlignmentOptions.MidlineRight;
            LayoutElement costLayout =
                costLabel.gameObject.AddComponent<LayoutElement>();
            costLayout.preferredWidth = 36f;
            costLayout.flexibleWidth = 0f;

            GameObject description = new GameObject("Description",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(LayoutElement));
            description.transform.SetParent(row.transform, false);
            description.GetComponent<Image>().color =
                new Color(0.035f, 0.038f, 0.04f, 0.98f);
            LayoutElement descriptionLayout =
                description.GetComponent<LayoutElement>();
            descriptionLayout.preferredHeight = 82f;
            VerticalLayoutGroup descriptionGroup =
                description.GetComponent<VerticalLayoutGroup>();
            descriptionGroup.padding = new RectOffset(7, 7, 5, 5);
            descriptionGroup.childControlWidth = true;
            descriptionGroup.childControlHeight = true;
            descriptionGroup.childForceExpandWidth = true;
            descriptionGroup.childForceExpandHeight = true;

            TMP_Text descriptionLabel = CreateFlowLabel(
                description.transform, template, trait.Description,
                14f,
                new Color(0.70f, 0.70f, 0.66f, 1f));
            descriptionLabel.enableWordWrapping = true;
            descriptionLabel.fontSizeMin = 9f;
            descriptionLabel.overflowMode = TextOverflowModes.Ellipsis;
            selection.Register(button, image, costLabel, trait, positive);
        }

        private static TMP_Text CreateScreenHeader(
            Transform parent,
            TMP_Text template)
        {
            GameObject header = new GameObject("ScreenHeader",
                typeof(RectTransform));
            header.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)header.transform;
            rect.anchorMin = new Vector2(0.25f, 1f);
            rect.anchorMax = new Vector2(0.75f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -190f);
            rect.offsetMax = new Vector2(0f, -95f);

            TMP_Text title = CreateFlowLabel(header.transform, template,
                "TRAITS", 30f,
                new Color(0.82f, 0.82f, 0.76f, 1f));
            title.alignment = TextAlignmentOptions.Midline;
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(0f, -46f);
            titleRect.offsetMax = Vector2.zero;

            TMP_Text points = CreateFlowLabel(header.transform, template,
                "AVAILABLE POINTS: 0", 20f,
                new Color(0.82f, 0.82f, 0.76f, 1f));
            points.alignment = TextAlignmentOptions.Midline;
            RectTransform pointsRect = points.rectTransform;
            pointsRect.anchorMin = new Vector2(0f, 1f);
            pointsRect.anchorMax = new Vector2(1f, 1f);
            pointsRect.pivot = new Vector2(0.5f, 1f);
            pointsRect.offsetMin = new Vector2(0f, -80f);
            pointsRect.offsetMax = new Vector2(0f, -48f);
            return points;
        }

        private static TMP_Text CreateFlowLabel(
            Transform parent,
            TMP_Text template,
            string text,
            float fontSize,
            Color color)
        {
            GameObject labelObject = new GameObject(
                "Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);
            labelObject.name = "Label";

            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            if (template != null)
            {
                label.font = template.font;
                label.fontSharedMaterial = template.fontSharedMaterial;
                label.fontStyle = template.fontStyle;
            }
            label.text = text;
            label.fontSize = fontSize;
            label.enableAutoSizing = true;
            label.fontSizeMin = 11f;
            label.fontSizeMax = fontSize;
            label.enableWordWrapping = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static TMP_Text CreateColumnLabel(
            Transform parent,
            TMP_Text template,
            string text,
            float fontSize,
            float horizontalPadding,
            float top,
            float height,
            Color color)
        {
            GameObject labelObject = new GameObject(
                "Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);
            labelObject.name = "Label";

            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            if (template != null)
            {
                label.font = template.font;
                label.fontSharedMaterial = template.fontSharedMaterial;
                label.fontStyle = template.fontStyle;
            }
            label.text = text;
            label.fontSize = fontSize;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = fontSize;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = color;

            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(horizontalPadding, -top - height);
            rect.offsetMax = new Vector2(-horizontalPadding, -top);
            return label;
        }

        private static void CreateBackButton(
            Transform panel,
            DefaultUIButton nativeBackButton)
        {
            if (nativeBackButton == null)
                return;

            GameObject backObject = UnityEngine.Object.Instantiate(
                nativeBackButton.gameObject,
                panel,
                true);
            backObject.name = "BackButton";

            DefaultUIButton backButton =
                backObject.GetComponent<DefaultUIButton>();
            backButton.OnClick.RemoveAllListeners();
            backButton.OnClick.AddListener(() =>
                nativeBackButton.OnClick.Invoke());
        }

        private static void CreateLabel(
            Transform parent,
            TMP_Text template,
            string text,
            float fontSize,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            GameObject labelObject = template != null
                ? UnityEngine.Object.Instantiate(template.gameObject, parent, false)
                : new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (labelObject.transform.parent != parent)
                labelObject.transform.SetParent(parent, false);

            labelObject.name = "Label";
            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.color = new Color(0.82f, 0.82f, 0.76f, 1f);

            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child;
            return null;
        }

        private sealed class TraitsTabController : ITabController
        {
            private readonly GameObject _panel;

            internal TraitsTabController(GameObject panel)
            {
                _panel = panel;
            }

            public void Show()
            {
                _panel.SetActive(true);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(
                    _panel.transform as RectTransform);
            }

            public Task<bool> TryHide()
            {
                _panel.SetActive(false);
                return Task.FromResult(true);
            }
        }
    }

    internal sealed class PersistentTabIcon : MonoBehaviour
    {
        private Image _image;
        private Sprite _sprite;

        internal void Initialize(Image image, Sprite sprite)
        {
            _image = image;
            _sprite = sprite;
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            if (_image != null && _sprite != null &&
                _image.sprite != _sprite)
                _image.sprite = _sprite;
        }
    }

    internal sealed class OwnedColumnScroller : MonoBehaviour,
        IScrollHandler, IBeginDragHandler, IDragHandler
    {
        private RectTransform _viewport;
        private RectTransform _content;

        internal void Initialize(RectTransform content)
        {
            _viewport = transform as RectTransform;
            _content = content;
        }

        public void OnScroll(PointerEventData eventData)
        {
            ScrollBy(-eventData.scrollDelta.y * 42f);
            eventData.Use();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnDrag(PointerEventData eventData)
        {
            ScrollBy(eventData.delta.y);
            eventData.Use();
        }

        private void ScrollBy(float delta)
        {
            if (_viewport == null || _content == null)
                return;

            float maximum = Mathf.Max(
                0f, _content.rect.height - _viewport.rect.height);
            Vector2 position = _content.anchoredPosition;
            position.y = Mathf.Clamp(position.y + delta, 0f, maximum);
            _content.anchoredPosition = position;
        }
    }

    internal sealed class TraitSelectionController : MonoBehaviour
    {
        private readonly HashSet<TraitDefinition> _selected =
            new HashSet<TraitDefinition>();
        private readonly List<Entry> _entries = new List<Entry>();
        private TMP_Text _pointsLabel;
        private Button _confirmButton;
        private Button _resetButton;
        private OwnedTraitsLayout _layout;
        private InventoryScreen _inventoryScreen;
        private Profile _profile;
        private InventoryController _inventoryController;
        private HashSet<string> _stashGpItemIds = new HashSet<string>();
        private int _points;
        private bool _paymentOpen;

        private const string GpCoinTemplateId = "5d235b4d86f7742e017bc88a";
        private const int ResetPrice = 50;

        internal void Initialize(
            TMP_Text pointsLabel,
            InventoryScreen inventoryScreen)
        {
            _pointsLabel = pointsLabel;
            _inventoryScreen = inventoryScreen;
            _points = 0;
            UpdatePointsLabel();
        }

        private static T GetPrivateField<T>(object instance, string name)
            where T : class
        {
            FieldInfo field = instance.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(instance) as T;
        }

        internal void Register(
            Button button,
            Image background,
            TMP_Text costLabel,
            TraitDefinition trait,
            bool positive)
        {
            Color normal = background.color;
            Color selected = positive
                ? new Color(0.18f, 0.29f, 0.16f, 0.98f)
                : new Color(0.32f, 0.15f, 0.14f, 0.98f);

            var entry = new Entry
            {
                Button = button,
                Background = background,
                CostLabel = costLabel,
                Trait = trait,
                Positive = positive,
                NormalColor = normal,
                SelectedColor = selected
            };
            _entries.Add(entry);

            button.onClick.AddListener(() =>
            {
                if (_selected.Remove(trait))
                {
                    _points += positive ? trait.Points : -trait.Points;
                    background.color = normal;
                }
                else
                {
                    _selected.Add(trait);
                    _points += positive ? -trait.Points : trait.Points;
                    background.color = selected;
                }

                UpdatePointsLabel();
                UpdateConfirmState();
            });
        }

        internal void FinalizeSetup(
            Button confirmButton,
            Button resetButton,
            OwnedTraitsLayout layout)
        {
            _confirmButton = confirmButton;
            _resetButton = resetButton;
            _layout = layout;
            _confirmButton.onClick.AddListener(ConfirmSelection);
            _resetButton.onClick.AddListener(OpenResetPayment);
            UpdateConfirmState();
            LoadPersistedSelection();
        }

        private async void ConfirmSelection()
        {
            if (_points < 0)
            {
                _pointsLabel.text = $"NEED {-_points} MORE POINTS";
                return;
            }

            if (!await SaveSelection())
                return;

            ShowConfirmedSelection();
        }

        private void ShowConfirmedSelection()
        {
            _pointsLabel.gameObject.SetActive(false);
            _confirmButton.gameObject.SetActive(false);
            _resetButton.gameObject.SetActive(true);
            foreach (Entry entry in _entries)
            {
                bool selected = _selected.Contains(entry.Trait);
                entry.Button.gameObject.SetActive(selected);
                entry.Button.interactable = false;
                entry.CostLabel.gameObject.SetActive(false);
                if (selected)
                    entry.Background.color = entry.SelectedColor;
            }
            _layout.Refresh();
        }

        private Task<bool> SaveSelection()
        {
            try
            {
                string json = RequestHandler.PostJson(
                    "/operator-traits/save",
                    JsonConvert.SerializeObject(new SaveTraitsRequest
                    {
                        Traits = _selected.Select(trait => trait.Id).ToList()
                    }));
                TraitsStateResponse response =
                    JsonConvert.DeserializeObject<TraitsStateResponse>(json);
                if (response == null || !response.Success)
                {
                    Plugin.Log.LogError(
                        $"Could not save traits: {response?.Error ?? "no response"}");
                    return Task.FromResult(false);
                }

                Plugin.SetActiveTraits(response.Traits);
                Plugin.SetActiveAllergies(response.Allergies);
                return Task.FromResult(true);
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError($"Could not save traits: {exception}");
                return Task.FromResult(false);
            }
        }

        private void LoadPersistedSelection()
        {
            try
            {
                string json = RequestHandler.PostJson(
                    "/operator-traits/load", "{}");
                TraitsStateResponse response =
                    JsonConvert.DeserializeObject<TraitsStateResponse>(json);
                if (response == null || !response.Success ||
                    response.Traits == null || response.Traits.Count == 0)
                {
                    Plugin.SetActiveTraits(response?.Traits);
                    Plugin.SetActiveAllergies(response?.Allergies);
                    return;
                }

                Plugin.SetActiveTraits(response.Traits);
                Plugin.SetActiveAllergies(response.Allergies);

                var saved = new HashSet<string>(
                    response.Traits, StringComparer.Ordinal);
                foreach (Entry entry in _entries)
                {
                    if (!saved.Contains(entry.Trait.Id))
                        continue;
                    _selected.Add(entry.Trait);
                    entry.Background.color = entry.SelectedColor;
                }
                ShowConfirmedSelection();
                Plugin.Log.LogInfo(
                    $"Loaded {_selected.Count} persisted Operator Traits.");
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError($"Could not load saved traits: {exception}");
            }
        }

        private async void OpenResetPayment()
        {
            if (_paymentOpen)
                return;

            // The tab is constructed from InventoryScreen.Show's prefix, before
            // EFT assigns these fields. Resolve them at click time instead.
            _profile = GetPrivateField<Profile>(_inventoryScreen, "_profile");
            _inventoryController = GetPrivateField<InventoryController>(
                _inventoryScreen, "_inventoryController");
            if (_profile == null || _inventoryController == null)
            {
                Plugin.Log.LogError(
                    "Reset payment could not access the active profile/inventory controller.");
                return;
            }

            if (ItemUiContext.Instance == null ||
                ItemUiContext.Instance.HandoverItemsWindow == null)
            {
                Plugin.Log.LogError(
                    "Reset payment could not access EFT's handover window.");
                return;
            }

            List<Item> stashGpCoins = _inventoryController.Inventory.Stash
                .GetAllItems()
                .Where(item => item.StringTemplateId == GpCoinTemplateId)
                .ToList();
            int availableGp = stashGpCoins.Sum(item => item.StackObjectsCount);
            Plugin.Log.LogInfo(
                $"Found {availableGp} GP in {stashGpCoins.Count} stash stack(s).");
            if (availableGp < ResetPrice)
            {
                ItemUiContext.Instance.ShowMessageWindow(
                    "NOT ENOUGH GP COINS",
                    null,
                    null,
                    $"You need {ResetPrice} GP coins in your stash to reset " +
                    $"your traits. You currently have {availableGp}.");
                return;
            }

            Item gpCoin = stashGpCoins[0];
            _stashGpItemIds = new HashSet<string>(
                stashGpCoins.Select(item => item.Id));

            var offer = new Offer
            {
                Id = "operator-traits-reset",
                Item = gpCoin,
                Quantity = 1,
                EndTime = DateTime.MaxValue,
                User = new Offer.Merchant
                {
                    MemberType = EMemberCategory.Trader,
                    SelectedMemberType = EMemberCategory.Trader,
                    Nickname = "Operator Traits"
                }
            };
            var requirement = new HandoverRequirement(
                GpCoinTemplateId, ResetPrice, false, false)
            {
                Offer = offer
            };
            offer.Requirements = new IExchangeRequirement[] { requirement };

            var goods = new Dictionary<IExchangeable, int>
            {
                [offer] = 1
            };

            try
            {
                _paymentOpen = true;
                Plugin.Log.LogInfo("Opening 50 GP trait reset payment window.");
                CommoditiesToPurchase purchase = await ItemUiContext.Instance
                    .HandoverItemsWindow.SelectItemsAsync(
                    EExchangeableWindowType.Ragfair,
                    goods,
                    false,
                    _profile,
                    _inventoryController,
                    item => _stashGpItemIds.Contains(item.Id));
                if (purchase != null)
                {
                    Plugin.Log.LogInfo(
                        "Trait reset payment selection was confirmed.");
                    await PayForReset(purchase);
                }
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError(
                    $"Could not open trait reset payment: {exception}");
            }
            finally
            {
                _paymentOpen = false;
            }
        }

        private async Task PayForReset(CommoditiesToPurchase purchase)
        {
            try
            {
                var selectedItems = new List<Item>();
                foreach (CommodityToPurchase commodity in purchase)
                foreach (ItemReference reference in commodity.Items)
                {
                    Item item;
                    if (_inventoryController.TryFindItem(
                            reference.Id, out item))
                        selectedItems.Add(item);
                }

                // Synthetic exchange offers validate correctly in EFT's native
                // handover window, but do not populate ItemReference entries.
                // Feed the paymaster the current stash GP stacks; it applies the
                // configured price and consumes only the required quantity.
                if (selectedItems.Count == 0)
                {
                    selectedItems.AddRange(_inventoryController.Inventory.Stash
                        .GetAllItems()
                        .Where(item =>
                            item.StringTemplateId == GpCoinTemplateId));
                    Plugin.Log.LogInfo(
                        "Handover returned no references; using live stash GP " +
                        $"stacks for payment ({selectedItems.Count} stacks)." );
                }

                Item paymentStack = selectedItems
                    .Where(item => item.StackObjectsCount > ResetPrice)
                    .OrderBy(item => item.StackObjectsCount)
                    .FirstOrDefault();
                bool paid = paymentStack != null &&
                    await DebitGpOnServer(paymentStack);
                Plugin.Log.LogInfo(
                    $"Trait reset GP payment result: {paid}; " +
                    $"selected stacks: {selectedItems.Count}.");
                if (paid)
                    ResetSelection();
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError(
                    $"Could not process trait reset payment: {exception}");
            }
        }

        private Task<bool> DebitGpOnServer(Item paymentStack)
        {
            var request = new ResetPaymentRequest
            {
                ItemId = paymentStack.Id,
                Amount = ResetPrice
            };
            string json = RequestHandler.PostJson(
                "/operator-traits/reset",
                JsonConvert.SerializeObject(request));
            ResetPaymentResponse response =
                JsonConvert.DeserializeObject<ResetPaymentResponse>(json);
            if (response == null || !response.Success)
            {
                Plugin.Log.LogError(
                    $"Server rejected GP reset payment: {response?.Error ?? "no response"}");
                return Task.FromResult(false);
            }

            paymentStack.StackObjectsCount = response.NewCount;
            _inventoryController.ReportProfileUpdate();
            return Task.FromResult(true);
        }

        private sealed class ResetPaymentRequest
        {
            public string ItemId;
            public int Amount;
        }

        private sealed class ResetPaymentResponse
        {
            public bool Success { get; set; }
            public string ItemId { get; set; }
            public int NewCount { get; set; }
            public string Error { get; set; }
        }

        private sealed class SaveTraitsRequest
        {
            public List<string> Traits { get; set; }
        }

        private sealed class TraitsStateResponse
        {
            public bool Success { get; set; }
            public List<string> Traits { get; set; }
            public List<string> Allergies { get; set; }
            public string Error { get; set; }
        }

        private void ResetSelection()
        {
            _selected.Clear();
            Plugin.SetActiveTraits(Array.Empty<string>());
            Plugin.SetActiveAllergies(Array.Empty<string>());
            _points = 0;
            _pointsLabel.gameObject.SetActive(true);
            _confirmButton.gameObject.SetActive(true);
            _resetButton.gameObject.SetActive(false);
            foreach (Entry entry in _entries)
            {
                entry.Button.gameObject.SetActive(true);
                entry.Button.interactable = true;
                entry.CostLabel.gameObject.SetActive(true);
                entry.Background.color = entry.NormalColor;
            }
            UpdatePointsLabel();
            UpdateConfirmState();
            _layout.Refresh();
        }

        private void UpdateConfirmState()
        {
            if (_confirmButton == null)
                return;

            bool hasStrength = _entries.Any(entry =>
                entry.Positive && _selected.Contains(entry.Trait));
            _confirmButton.interactable = hasStrength && _points >= 0;
        }

        private void UpdatePointsLabel()
        {
            if (_pointsLabel == null)
                return;

            _pointsLabel.text = $"AVAILABLE POINTS: {_points}";
            _pointsLabel.color = _points < 0
                ? new Color(0.82f, 0.38f, 0.34f, 1f)
                : _points > 0
                    ? new Color(0.58f, 0.76f, 0.52f, 1f)
                    : new Color(0.82f, 0.82f, 0.76f, 1f);
        }

        private sealed class Entry
        {
            internal Button Button;
            internal Image Background;
            internal TMP_Text CostLabel;
            internal TraitDefinition Trait;
            internal bool Positive;
            internal Color NormalColor;
            internal Color SelectedColor;
        }
    }

    internal sealed class OwnedTraitsLayout : MonoBehaviour
    {
        private readonly List<Column> _columns = new List<Column>();

        internal void Initialize()
        {
            foreach (LayoutGroup group in
                     GetComponentsInChildren<LayoutGroup>(true))
                group.enabled = false;
            foreach (ContentSizeFitter fitter in
                     GetComponentsInChildren<ContentSizeFitter>(true))
                fitter.enabled = false;

            _columns.Clear();
            AddColumn("PositivePerks");
            AddColumn("NegativePerks");
            ApplyLayout();
        }

        private void LateUpdate()
        {
            ApplyLayout();
        }

        private void AddColumn(string name)
        {
            Transform column = Find(name);
            if (column == null)
                return;
            Transform viewport = Find(column, "Viewport");
            Transform content = viewport != null ? Find(viewport, "Content") : null;
            if (viewport == null || content == null)
                return;

            List<RectTransform> rows = content.Cast<Transform>()
                .Select(child => child as RectTransform)
                .Where(child => child != null)
                .ToList();
            List<RectTransform> cards = rows
                .SelectMany(row => row.Cast<Transform>())
                .Select(child => child as RectTransform)
                .Where(child => child != null)
                .ToList();
            foreach (RectTransform card in cards)
                card.SetParent(content, false);
            foreach (RectTransform row in rows)
                row.gameObject.SetActive(false);

            _columns.Add(new Column
            {
                Viewport = viewport as RectTransform,
                Content = content as RectTransform,
                Cards = cards
            });
        }

        internal void Refresh()
        {
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            foreach (Column column in _columns)
            {
                if (column.Viewport == null || column.Content == null)
                    continue;

                SetRect(column.Viewport, Vector2.zero, Vector2.one,
                    new Vector2(8f, 8f), new Vector2(-8f, -54f));

                const float rowHeight = 142f;
                const float rowSpacing = 8f;
                List<RectTransform> cards = column.Cards
                    .Where(card => card != null && card.gameObject.activeSelf)
                    .ToList();
                int rowCount = (cards.Count + 1) / 2;
                float contentHeight = rowCount * rowHeight +
                                      Mathf.Max(0, rowCount - 1) * rowSpacing;
                column.Content.anchorMin = new Vector2(0f, 1f);
                column.Content.anchorMax = new Vector2(1f, 1f);
                column.Content.pivot = new Vector2(0.5f, 1f);
                column.Content.sizeDelta = new Vector2(0f, contentHeight);
                Vector2 scrollPosition = column.Content.anchoredPosition;
                scrollPosition.x = 0f;
                scrollPosition.y = Mathf.Clamp(
                    scrollPosition.y,
                    0f,
                    Mathf.Max(0f, contentHeight - column.Viewport.rect.height));
                column.Content.anchoredPosition = scrollPosition;

                for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
                {
                    int rowIndex = cardIndex / 2;
                    int columnIndex = cardIndex % 2;
                    float top = rowIndex * (rowHeight + rowSpacing);
                    LayoutCard(cards[cardIndex], columnIndex, top, rowHeight);
                }
            }
        }

        private static void LayoutCard(
            RectTransform card,
            int index,
            float top,
            float height)
        {
            float leftAnchor = index * 0.5f;
            float rightAnchor = (index + 1) * 0.5f;
            SetTopRect(card, leftAnchor, rightAnchor, top, height,
                index == 0 ? 0f : 4f,
                index == 0 ? -4f : 0f);

            RectTransform header = Find(card, "Header") as RectTransform;
            RectTransform description = Find(card, "Description") as RectTransform;
            if (header != null)
            {
                SetTopRect(header, 0f, 1f, 6f, 28f, 8f, -8f);
                List<RectTransform> labels = header.Cast<Transform>()
                    .Select(child => child as RectTransform)
                    .Where(child => child != null)
                    .ToList();
                if (labels.Count > 0)
                    SetRect(labels[0], Vector2.zero,
                        new Vector2(0.82f, 1f), Vector2.zero, Vector2.zero);
                if (labels.Count > 1)
                    SetRect(labels[1], new Vector2(0.82f, 0f),
                        Vector2.one, Vector2.zero, Vector2.zero);
            }

            if (description != null)
            {
                SetRect(description, Vector2.zero, Vector2.one,
                    new Vector2(7f, 7f), new Vector2(-7f, -40f));
                RectTransform label = description.childCount > 0
                    ? description.GetChild(0) as RectTransform
                    : null;
                if (label != null)
                    SetRect(label, Vector2.zero, Vector2.one,
                        new Vector2(7f, 5f), new Vector2(-7f, -5f));
            }
        }

        private static void SetTopRect(
            RectTransform rect,
            float anchorLeft,
            float anchorRight,
            float top,
            float height,
            float left,
            float right)
        {
            rect.anchorMin = new Vector2(anchorLeft, 1f);
            rect.anchorMax = new Vector2(anchorRight, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(right, -top);
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private Transform Find(string name)
        {
            return Find(transform, name);
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child;
            return null;
        }

        private sealed class Column
        {
            internal RectTransform Viewport;
            internal RectTransform Content;
            internal List<RectTransform> Cards;
        }
    }

    internal sealed class TraitsTabRowLayout : MonoBehaviour
    {
        private readonly List<RectTransform> _tabs =
            new List<RectTransform>();
        private readonly Dictionary<RectTransform, float> _originalCenters =
            new Dictionary<RectTransform, float>();
        private readonly Dictionary<RectTransform, float> _originalWidths =
            new Dictionary<RectTransform, float>();
        private RectTransform _row;
        private float _originalRowWidth;

        internal void Initialize(IEnumerable<Tab> tabs)
        {
            _row = transform as RectTransform;
            _originalRowWidth = _row != null ? _row.rect.width : 0f;
            _tabs.Clear();
            _tabs.AddRange(tabs
                .Where(tab => tab != null)
                .Select(tab => tab.transform as RectTransform)
                .Where(rect => rect != null));
            _originalCenters.Clear();
            _originalWidths.Clear();
            foreach (RectTransform rect in _tabs)
            {
                _originalCenters[rect] = rect.localPosition.x;
                _originalWidths[rect] = rect.rect.width;
            }
            ApplyLayout();
        }

        private void LateUpdate()
        {
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            List<RectTransform> visibleTabs = _tabs
                .Where(rect => rect != null && rect.gameObject.activeSelf)
                .ToList();
            if (visibleTabs.Count == 0)
                return;

            List<RectTransform> nativeTabs = visibleTabs
                .Where(rect => rect.name != "OperatorTraitsTab")
                .OrderBy(rect => _originalCenters[rect])
                .ToList();
            if (nativeTabs.Count < 2)
                return;

            float firstCenter = _originalCenters[nativeTabs.First()];
            float lastCenter = _originalCenters[nativeTabs.Last()];
            float nativeSpacing =
                (lastCenter - firstCenter) / (nativeTabs.Count - 1);
            if (nativeSpacing <= 0f)
                return;

            if (_row != null)
                _row.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    _originalRowWidth + nativeSpacing);

            bool insertedTraitsTab = false;
            float previousNativeCenter = firstCenter;
            foreach (RectTransform rect in visibleTabs)
            {
                Vector3 position = rect.localPosition;
                if (rect.name == "OperatorTraitsTab")
                {
                    position.x = previousNativeCenter + nativeSpacing;
                    insertedTraitsTab = true;
                }
                else
                {
                    position.x = _originalCenters[rect] +
                                 (insertedTraitsTab ? nativeSpacing : 0f);
                    previousNativeCenter = _originalCenters[rect];
                }
                rect.localPosition = position;
                rect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    _originalWidths[rect]);
            }
        }
    }
}
