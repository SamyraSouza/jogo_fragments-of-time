using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RiftOfTime
{
    /// <summary>Cria elementos de uGUI por código (o jogo não depende de prefabs de UI).</summary>
    public static class UIFactory
    {
        static Font font;
        static Sprite rounded;

        public static Font DefaultFont
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        /// <summary>Sprite de retângulo arredondado (9-slice) gerado em código.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int size = 64;
                const float radius = 26f;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                var px = new Color32[size * size];
                float half = size * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                        float dy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                        float ox = Mathf.Max(dx, 0f);
                        float oy = Mathf.Max(dy, 0f);
                        float dist = Mathf.Sqrt(ox * ox + oy * oy) - radius;
                        float a = Mathf.Clamp01(0.5f - dist);
                        px[y * size + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
                return rounded;
            }
        }

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static Image CreateImage(Transform parent, Color color, string name = "Image", bool rounded = false)
        {
            RectTransform rt = CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (rounded)
            {
                img.sprite = Rounded;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        public static Text CreateText(Transform parent, string content, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal, string name = "Text")
        {
            RectTransform rt = CreateRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }

        /// <summary>Posiciona por âncora/pivô com tamanho fixo.</summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        /// <summary>Botão arredondado no estilo da tela inicial do storyboard.</summary>
        public static Button CreateButton(Transform parent, string label, Vector2 size, UnityAction onClick,
            int fontSize = 40, string name = "Button")
        {
            Image img = CreateImage(parent, new Color(0.99f, 0.84f, 0.67f), name, true);
            img.rectTransform.sizeDelta = size;
            img.raycastTarget = true;

            var shadow = img.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.72f, 0.55f, 0.45f, 1f);
            shadow.effectDistance = new Vector2(0f, -8f);

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.93f, 0.84f, 0.74f);
            colors.selectedColor = new Color(0.93f, 0.84f, 0.74f);
            colors.pressedColor = new Color(0.78f, 0.68f, 0.58f);
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.55f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(onClick);

            Text t = CreateText(img.transform, label, fontSize, new Color(0.45f, 0.36f, 0.33f), TextAnchor.MiddleCenter, FontStyle.Bold, "Label");
            Stretch(t.rectTransform);
            return btn;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
    }
}
