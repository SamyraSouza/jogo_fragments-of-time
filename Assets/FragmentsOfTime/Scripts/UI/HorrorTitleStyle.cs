using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RiftOfTime
{
    /// <summary>Tela de título no estilo da abertura do Resident Evil Revelations 2.</summary>
    public static class MenuSounds
    {
        public const string MusicKey = "menu_music";
        static AudioClip bell;

        /// <summary>Começa a música do menu em loop (entra suave).</summary>
        public static bool PlayMusic(float volume = 0.8f)
        {
            var clip = Resources.Load<AudioClip>("Sfx/intro-menu");
            if (clip == null || AudioDirector.Instance == null) return false;
            AudioDirector.Instance.Loop(MusicKey, clip, volume);
            return true;
        }

        /// <summary>Para a música (por padrão NA HORA, sem fade; com immediate = false ela sai suave).</summary>
        public static void StopMusic(bool immediate = true)
        {
            if (AudioDirector.Instance == null) return;
            if (immediate) AudioDirector.Instance.StopLoopNow(MusicKey);
            else AudioDirector.Instance.StopLoop(MusicKey);
        }

        /// <summary>Sino ao clicar em Iniciar (menu) ou Continuar (pausa).</summary>
        public static void Bell(float volume = 1f)
        {
            if (bell == null) bell = Resources.Load<AudioClip>("Sfx/sino-iniciar");
            AudioDirector.Sfx(bell, volume);
        }
    }

    public static class HorrorTitleStyle
    {
        public static readonly Color Bone = new Color(0.9f, 0.88f, 0.84f);
        public static readonly Color Orange = new Color(1f, 0.6f, 0.17f);
        public static readonly Color OrangeHot = new Color(1f, 0.8f, 0.45f);

        /// <summary>Centro horizontal da coluna do título/opções (fração da largura da tela).</summary>
        public const float ColumnX = 0.30f;

        static Font font, sansFont;

        /// <summary>Fonte limpa, sem serifa, das opções (Segoe UI/Myriad/Arial do sistema).</summary>
        public static Font SansFont
        {
            get
            {
                if (sansFont != null) return sansFont;
                sansFont = Font.CreateDynamicFontFromOSFont(new[] { "Myriad Pro", "Segoe UI", "Calibri", "Arial" }, 40);
                if (sansFont == null) sansFont = UIFactory.DefaultFont;
                return sansFont;
            }
        }

        public static Font TitleFont
        {
            get
            {
                if (font != null) return font;
                font = Resources.Load<Font>("Fonts/titulo");
                if (font == null)
                    font = Font.CreateDynamicFontFromOSFont(new[] { "Trajan Pro", "Cinzel", "Constantia", "Palatino Linotype", "Book Antiqua", "Georgia", "Times New Roman" }, 100);
                if (font == null) font = UIFactory.DefaultFont;
                return font;
            }
        }

        static Text MakeText(Transform parent, string content, int size, Color color, string name)
        {
            var t = UIFactory.CreateText(parent, content, size, color, TextAnchor.MiddleCenter, FontStyle.Normal, name);
            t.font = TitleFont;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static RectTransform Column(Transform parent, string name, float y, Vector2 size)
        {
            var rt = UIFactory.CreateRect(name, parent);
            UIFactory.Place(rt, new Vector2(ColumnX, y), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return rt;
        }

        public static void CreateTitle(Transform parent, string line1, string line2)
        {
            var holder = Column(parent, "TituloMascara", 0.655f, new Vector2(1100f, 330f));
            var img = holder.gameObject.AddComponent<Image>();
            img.sprite = MakeWear(1024, 320, 7);
            img.color = Color.white;
            img.raycastTarget = false;
            holder.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var t1 = MakeText(holder, line1, 150, Bone, "Titulo");
            UIFactory.Place(t1.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 62f), new Vector2(1100f, 190f));
            t1.gameObject.AddComponent<GlitchText>().Configure(12f, 34f, 1f);
            AddShadow(t1, new Vector2(4f, -4f), 0.8f);

            var t2 = MakeText(holder, line2, 62, new Color(0.74f, 0.72f, 0.68f), "Subtitulo");
            UIFactory.Place(t2.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(1100f, 90f));
            t2.gameObject.AddComponent<GlitchText>().Configure(30f, 20f, 0.9f);
            AddShadow(t2, new Vector2(3f, -3f), 0.8f);
        }

        public static Button CreateEntry(Transform parent, string label, int fontSize, UnityAction onClick, string name, float y)
        {
            var rt = Column(parent, name, y, new Vector2(900f, fontSize * 1.6f));
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var t = UIFactory.CreateText(rt, label.ToUpper(), fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal, "Label");
            t.font = SansFont;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            UIFactory.Stretch(t.rectTransform);
            var gt = t.gameObject.AddComponent<GlitchText>();
            gt.Configure(2f, 10f, 0.5f);
            gt.glowColor = new Color(1f, 0.96f, 0.88f, 1f);
            gt.glowRadius = 1.3f;
            AddShadow(t, new Vector2(1.5f, -1.5f), 0.55f);
            var hover = rt.gameObject.AddComponent<MenuEntryHover>();
            hover.glow = gt;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = t;
            var c = btn.colors;
            c.normalColor = new Color(0.93f, 0.91f, 0.86f);
            c.highlightedColor = Color.white;
            c.selectedColor = Color.white;
            c.pressedColor = new Color(0.9f, 0.88f, 0.84f);
            c.disabledColor = new Color(0.6f, 0.55f, 0.5f, 0.5f);
            c.fadeDuration = 0.05f;
            btn.colors = c;
            if (onClick != null) btn.onClick.AddListener(onClick);
            Debug.Log("[Menu] opção '" + label + "': letra " + c.normalColor + " (cinza-creme), selecionada " + c.highlightedColor + " (branca), sem brilho laranja");
            return btn;
        }

        public static void CreateCopyright(Transform parent, string text)
        {
            var rt = UIFactory.CreateRect("Copyright", parent);
            UIFactory.Place(rt, new Vector2(0.5f, 0.095f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 40f));
            var t = MakeText(rt, text, 19, new Color(0.5f, 0.49f, 0.46f, 0.9f), "Texto");
            UIFactory.Stretch(t.rectTransform);
            t.gameObject.AddComponent<GlitchText>().Configure(1.2f, 8f, 0.4f);
        }

        public static RectTransform CreateDarkening(Transform parent, int siblingIndex)
        {
            var group = UIFactory.CreateRect("Escurecimento", parent);
            UIFactory.Stretch(group);
            group.SetSiblingIndex(siblingIndex);
            NewFullImage(group, "Gradiente", MakeGradient());
            NewFullImage(group, "Vinheta", MakeVignette());
            var veil = NewFullImage(group, "Veu", null);
            veil.color = new Color(0f, 0f, 0f, 0.55f);
            return group;
        }

        static Image NewFullImage(Transform parent, string name, Sprite sprite)
        {
            var rt = UIFactory.CreateRect(name, parent);
            UIFactory.Stretch(rt);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = Color.white;
            img.raycastTarget = false;
            return img;
        }

        static Sprite MakeGradient()
        {
            const int w = 256, h = 4;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int x = 0; x < w; x++)
            {
                float t = x / (w - 1f);
                float a = Mathf.Lerp(0.97f, 0.5f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.1f) / 0.8f)));
                for (int y = 0; y < h; y++) tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite MakeVignette()
        {
            const int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                    float a = Mathf.Lerp(0.15f, 0.95f, Mathf.SmoothStep(0.25f, 1f, r));
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }

        static void AddShadow(Text t, Vector2 dist, float alpha)
        {
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, alpha);
            sh.effectDistance = dist;
        }

        static Sprite MakeWear(int w, int h, int seed)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            var rng = new System.Random(seed);
            float ox = (float)rng.NextDouble() * 100f, oy = (float)rng.NextDouble() * 100f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float big = Mathf.PerlinNoise(ox + x * 0.012f, oy + y * 0.03f);
                    float fine = Mathf.PerlinNoise(ox + x * 0.55f, oy + y * 0.55f);
                    float n = big * 0.35f + fine * 0.65f;
                    int a;
                    if (n > 0.80f || rng.NextDouble() < 0.003) a = 0;
                    else if (n > 0.70f) a = 110;
                    else a = 255;
                    px[y * w + x] = new Color32(255, 255, 255, (byte)a);
                }
            void Carve(int x0, int y0, int lw, int lh, byte alpha)
            {
                for (int y = y0; y < y0 + lh; y++)
                    for (int x = x0; x < x0 + lw; x++)
                        if (x >= 0 && x < w && y >= 0 && y < h) px[y * w + x] = new Color32(255, 255, 255, alpha);
            }
            for (int i = 0; i < 90; i++) Carve(rng.Next(0, w), rng.Next(0, h), rng.Next(1, 3), rng.Next(30, 220), (byte)(rng.NextDouble() < 0.6 ? 0 : 90));
            for (int i = 0; i < 18; i++) Carve(rng.Next(0, w), rng.Next(0, h), rng.Next(15, 80), rng.Next(1, 3), 0);
            Crackle(px, w, h, seed);
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static void Crackle(Color32[] px, int w, int h, int seed)
        {
            const int cell = 46;
            int cx = w / cell + 2, cy = h / cell + 2;
            var rng = new System.Random(seed * 31 + 5);
            var sx = new float[cx, cy]; var sy = new float[cx, cy];
            for (int j = 0; j < cy; j++)
                for (int i = 0; i < cx; i++)
                {
                    sx[i, j] = (i + (float)rng.NextDouble() * 0.9f) * cell;
                    sy[i, j] = (j + (float)rng.NextDouble() * 0.9f) * cell;
                }
            float ox = (float)rng.NextDouble() * 50f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float wx = x + (Mathf.PerlinNoise(ox + x * 0.05f, y * 0.05f) - 0.5f) * 14f;
                    float wy = y + (Mathf.PerlinNoise(ox + 40f + x * 0.05f, y * 0.05f) - 0.5f) * 14f;
                    int ci = Mathf.Clamp((int)(wx / cell), 0, cx - 1), cj = Mathf.Clamp((int)(wy / cell), 0, cy - 1);
                    float d1 = float.MaxValue, d2 = float.MaxValue;
                    for (int j = Mathf.Max(0, cj - 1); j <= Mathf.Min(cy - 1, cj + 1); j++)
                        for (int i = Mathf.Max(0, ci - 1); i <= Mathf.Min(cx - 1, ci + 1); i++)
                        {
                            float dx = wx - sx[i, j], dy = wy - sy[i, j];
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
                        }
                    float edge = d2 - d1;
                    float keep = Mathf.PerlinNoise(ox + 80f + x * 0.02f, y * 0.02f);
                    if (edge < 1.5f && keep > 0.32f)
                    {
                        int idx = y * w + x;
                        px[idx] = new Color32(255, 255, 255, 0);
                    }
                }
        }
    }

    /// <summary>Opção de menu: a luz em volta da letra fica um pouco mais forte com o mouse por cima ou quando selecionada pelo teclado.</summary>
    public class MenuEntryHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public GlitchText glow;
        bool over, selected;

        public void OnPointerEnter(PointerEventData e) { over = true; Apply(); }
        public void OnPointerExit(PointerEventData e) { over = false; Apply(); }
        public void OnSelect(BaseEventData e) { selected = true; Apply(); }
        public void OnDeselect(BaseEventData e) { selected = false; Apply(); }

        void Apply() { if (glow != null) glow.SetGlowBoost(over || selected ? 1.8f : 1f); }
    }

    /// <summary>Cursor do menu: uma seta TRIANGULAR bege com contorno marrom (desenhada por código).</summary>
    public static class MenuCursor
    {
        static Texture2D tex;
        static Vector2 hotspot;

        public static void Set()
        {
            if (tex == null) Build();
            Cursor.SetCursor(tex, hotspot, CursorMode.Auto);
        }

        public static void Reset() { Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); }

        static void Build()
        {
            const int w = 32, h = 32;
            Vector2 A = new Vector2(3f, 2f), Bp = new Vector2(7f, 28f), C = new Vector2(19f, 19f);
            Color32 fill = new Color32(222, 198, 156, 255);
            Color32 line = new Color32(78, 52, 34, 255);
            var px = new Color32[w * h];
            const int ss = 4;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            Vector2 p = new Vector2(x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss);
                            bool inside = Inside(p, A, Bp, C);
                            float d = Mathf.Min(Seg(p, A, Bp), Mathf.Min(Seg(p, Bp, C), Seg(p, C, A)));
                            if (inside && d > 1.5f) { r += fill.r; g += fill.g; b += fill.b; a += 255f; }
                            else if (d <= 1.5f) { r += line.r; g += line.g; b += line.b; a += 255f; }
                        }
                    int n = ss * ss;
                    float al = a / n;
                    px[(h - 1 - y) * w + x] = a > 0f ? new Color32((byte)(r / (a / 255f)), (byte)(g / (a / 255f)), (byte)(b / (a / 255f)), (byte)al) : new Color32(0, 0, 0, 0);
                }
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px);
            tex.Apply();
            hotspot = new Vector2(2f, 1f);
        }

        static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        static float Sign(Vector2 p, Vector2 a, Vector2 b) { return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y); }

        static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }

    /// <summary>Relógio único das falhas: fica parado e, em rajadas curtas, TODOS os efeitos (título, opções, vídeo) falham juntos.</summary>
    public static class TvGlitchClock
    {
        public static Vector2 EveryRange = new Vector2(1.8f, 5f);
        public static Vector2 LengthRange = new Vector2(0.16f, 0.4f);
        public static float TickRate = 24f;

        public static bool Bursting { get; private set; }
        public static int FrameId { get; private set; }
        public static int BurstId { get; private set; }

        static float nextBurst = -1f, burstEnd, nextTick;

        public static void Tick()
        {
            float now = Time.unscaledTime;
            if (nextBurst < 0f) nextBurst = now + Random.Range(EveryRange.x * 0.6f, EveryRange.y);
            if (!Bursting && now >= nextBurst)
            {
                Bursting = true; BurstId++; burstEnd = now + Random.Range(LengthRange.x, LengthRange.y); nextTick = 0f;
            }
            else if (Bursting && now >= burstEnd)
            {
                Bursting = false; FrameId++;
                nextBurst = now + Random.Range(EveryRange.x, EveryRange.y);
            }
            if (Bursting && now >= nextTick) { nextTick = now + 1f / TickRate; FrameId++; }
        }

        public static void Reset() { Bursting = false; nextBurst = -1f; FrameId = 0; BurstId = 0; }
    }

    /// <summary>Base dos efeitos de falha: sorteia as faixas deslocadas de cada quadro da rajada.</summary>
    public abstract class GlitchBase : BaseMeshEffect
    {
        public float glitchStrength = 40f;
        public float intensity = 1f;
        public int bands = 48;
        public bool flickerRedraw;
        float nextFlick;

        protected float[] bandOffset;
        protected float rollY, split, dim;
        int lastFrame = -1;

        protected override void OnEnable()
        {
            base.OnEnable();
            bandOffset = new float[Mathf.Max(2, bands)];
            lastFrame = -1;
        }

        void Update()
        {
            if (flickerRedraw && Time.unscaledTime >= nextFlick)
            {
                nextFlick = Time.unscaledTime + 1f / 12f;
                if (graphic != null) graphic.SetVerticesDirty();
            }
            if (lastFrame == TvGlitchClock.FrameId) return;
            lastFrame = TvGlitchClock.FrameId;
            if (TvGlitchClock.Bursting) NewFrame();
            if (graphic != null) graphic.SetVerticesDirty();
        }

        void NewFrame()
        {
            if (bandOffset == null || bandOffset.Length != Mathf.Max(2, bands)) bandOffset = new float[Mathf.Max(2, bands)];
            float t = Time.unscaledTime * 9f;
            float seed = Random.value * 50f;
            float wave = Random.Range(0.25f, 0.6f);
            for (int i = 0; i < bandOffset.Length; i++)
            {
                float n = (Mathf.PerlinNoise(seed + i * 0.32f, t * 0.4f) - 0.5f) * 2f * wave;
                float jag = Random.value < 0.18f ? Random.Range(-1f, 1f) : 0f;
                bandOffset[i] = (n + jag * 0.9f) * glitchStrength * intensity;
            }
            rollY = Random.value < 0.35f ? Random.Range(-1f, 1f) * glitchStrength * 0.12f * intensity : 0f;
            split = Random.Range(1.5f, 6f) * intensity;
            dim = Random.value < 0.3f ? Random.Range(0.45f, 0.8f) : 1f;
        }

        protected static UIVertex Lerp(UIVertex a, UIVertex b, float t)
        {
            UIVertex v = a;
            v.position = Vector3.Lerp(a.position, b.position, t);
            v.uv0 = Vector4.Lerp(a.uv0, b.uv0, t);
            return v;
        }

        protected static void AddQuad(List<UIVertex> list, UIVertex l0, UIVertex r0, UIVertex r1, UIVertex l1, Vector3 shift, Color mul)
        {
            UIVertex[] q = { l0, r0, r1, r1, l1, l0 };
            for (int k = 0; k < 6; k++)
            {
                UIVertex v = q[k];
                v.position += shift;
                Color c = v.color;
                v.color = new Color(c.r * mul.r, c.g * mul.g, c.b * mul.b, c.a * mul.a);
                list.Add(v);
            }
        }
    }

    /// <summary>Falha em texto: letras fatiadas em faixas que escorregam (a mesma faixa em todas as letras), com ghosting; também centraliza e espaça as letras.</summary>
    [RequireComponent(typeof(Text))]
    public class GlitchText : GlitchBase
    {
        public float tracking = 0f;
        public Color bottomTint = Color.white;
        public Color glowColor = new Color(0f, 0f, 0f, 0f);
        public float glowRadius = 2f;
        public float glowBoost = 1f;

        public void SetGlowBoost(float v)
        {
            if (Mathf.Approximately(v, glowBoost)) return;
            glowBoost = v;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public void Configure(float trackingPx, float strength, float inten)
        {
            tracking = trackingPx; glitchStrength = strength; intensity = inten;
        }

        static Color32 Tint(Color32 c, Color m)
        {
            return new Color32((byte)(c.r * m.r), (byte)(c.g * m.g), (byte)(c.b * m.b), c.a);
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            var stream = new List<UIVertex>();
            vh.GetUIVertexStream(stream);
            bool glitch = TvGlitchClock.Bursting && bandOffset != null;

            string txt = (graphic as Text) != null ? ((Text)graphic).text : "";
            var idx = new List<int>();
            for (int c = 0; c < txt.Length; c++) if (!char.IsWhiteSpace(txt[c])) idx.Add(c);
            float mid = (txt.Length - 1) * 0.5f;

            Rect rect = graphic.rectTransform.rect;
            int nb = bandOffset != null ? bandOffset.Length : 1;
            var main = new List<UIVertex>(stream.Count * (glitch ? 6 : 1));
            var ghostR = glitch ? new List<UIVertex>() : null;
            var ghostC = glitch ? new List<UIVertex>() : null;
            const int strips = 14;

            int glyph = 0;
            for (int i = 0; i + 5 < stream.Count; i += 6, glyph++)
            {
                UIVertex tl = stream[i], tr = stream[i + 1], br = stream[i + 2], bl = stream[i + 4];
                float dx = tracking * ((glyph < idx.Count ? idx[glyph] : glyph) - mid);

                if (!glitch)
                {
                    for (int k = 0; k < 6; k++)
                    {
                        UIVertex v = stream[i + k];
                        v.position += new Vector3(dx, 0f, 0f);
                        bool top = k == 0 || k == 1 || k == 5;
                        if (!top) v.color = Tint(v.color, bottomTint);
                        main.Add(v);
                    }
                    continue;
                }

                for (int s = 0; s < strips; s++)
                {
                    float t0 = s / (float)strips, t1 = (s + 1) / (float)strips;
                    UIVertex l0 = Lerp(tl, bl, t0), r0 = Lerp(tr, br, t0), l1 = Lerp(tl, bl, t1), r1 = Lerp(tr, br, t1);
                    Color g0 = Color.Lerp(Color.white, bottomTint, t0), g1 = Color.Lerp(Color.white, bottomTint, t1);
                    l0.color = Tint(l0.color, g0); r0.color = Tint(r0.color, g0); l1.color = Tint(l1.color, g1); r1.color = Tint(r1.color, g1);
                    float yc = (l0.position.y + l1.position.y) * 0.5f;
                    int band = Mathf.Clamp(Mathf.FloorToInt((yc - rect.yMin) / Mathf.Max(1f, rect.height) * nb), 0, nb - 1);
                    float lum = dim * (((s + glyph) & 1) == 0 ? 1f : 0.62f);
                    Vector3 shift = new Vector3(dx + bandOffset[band], rollY, 0f);
                    AddQuad(main, l0, r0, r1, l1, shift, new Color(lum, lum, lum, 1f));
                    AddQuad(ghostR, l0, r0, r1, l1, shift + new Vector3(-split * 0.6f, 0f, 0f), new Color(0.8f, 0.8f, 0.8f, 0.18f * dim));
                }
            }

            var all = new List<UIVertex>();
            if (glowColor.a > 0.001f && !glitch && main.Count * 56 < 60000)
            {
                float flick = flickerRedraw ? 0.85f + 0.3f * Mathf.PerlinNoise(Time.unscaledTime * 6f, 2.7f) : 1f;
                float[] ringMul = { 1f, 2f, 3.2f, 4.6f };
                float[] ringAlpha = { 0.022f, 0.014f, 0.009f, 0.005f };
                const int dirsPerRing = 14;
                for (int ring = 0; ring < ringMul.Length; ring++)
                {
                    float r = glowRadius * ringMul[ring];
                    float a = glowColor.a * ringAlpha[ring] * flick * glowBoost;
                    for (int k = 0; k < dirsPerRing; k++)
                    {
                        float ang = k * Mathf.PI * 2f / dirsPerRing + ring * 0.3f;
                        Vector3 off = new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f);
                        foreach (UIVertex v in main)
                        {
                            UIVertex g = v;
                            g.position += off;
                            g.color = new Color(glowColor.r, glowColor.g, glowColor.b, Mathf.Clamp01(a) * (v.color.a / 255f));
                            all.Add(g);
                        }
                    }
                }
            }
            if (glitch) all.AddRange(ghostR);
            all.AddRange(main);
            if (all.Count > 64000) { all = main; }
            vh.Clear();
            vh.AddUIVertexTriangleStream(all);
        }
    }
}
