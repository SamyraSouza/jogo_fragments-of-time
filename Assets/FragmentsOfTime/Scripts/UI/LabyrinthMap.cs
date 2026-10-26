using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RiftOfTime
{
    /// <summary>Mapa do labirinto da Fase 2, desenhado no papiro.</summary>
    public class LabyrinthMap : MonoBehaviour
    {
        public int cols, rows;
        public float x0, z0, cell = 5f;
        public bool[] hWalls;
        public bool[] vWalls;
        public Vector2Int startCell;
        public Vector2Int exitCell;

        const int S = 14;
        const float ScaleX = 2.2f, ScaleY = 2.2f;

        public static LabyrinthMap Instance { get; private set; }
        public bool Unlocked { get { return unlocked; } }

        Texture2D tex;
        Color32[] basePx, baseRoute, px;
        Text legend;
        float routeUntil;
        int[] navDist;
        int navTarget = -1;
        int texW, texH;
        Canvas canvas;
        Texture2D parchment;
        GameObject panel;
        Text label;
        Transform player;
        bool unlocked, open;
        float nextRedraw;
        readonly List<Vector2Int> route = new List<Vector2Int>();

        static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        static readonly Color32 WallInk = new Color32(84, 52, 26, 255);
        static readonly Color32 RouteInk = new Color32(150, 98, 52, 255);
        static readonly Color32 ExitFill = new Color32(120, 78, 36, 255);
        static readonly Color32 StartInk = new Color32(140, 100, 60, 255);

        public bool HasH(int c, int r) { return H(c, r); }
        public bool HasV(int c, int r) { return V(c, r); }
        bool H(int c, int r) { return hWalls[c + r * cols]; }
        bool V(int c, int r) { return vWalls[c + r * (cols + 1)]; }

        void Awake() { Instance = this; }

        void Start()
        {
            if (hWalls == null || vWalls == null || cols <= 0 || rows <= 0) { enabled = false; return; }
            texW = cols * S + 2;
            texH = rows * S + 2;
            tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            basePx = new Color32[texW * texH];
            baseRoute = new Color32[texW * texH];
            px = new Color32[texW * texH];
            ComputeRoute();
            DrawBase(basePx, false);
            DrawBase(baseRoute, true);

            canvas = UIFactory.CreateCanvas("LabyrinthMapCanvas", 90);
            Color ink = new Color(0.30f, 0.18f, 0.08f);
            float mapW = texW * ScaleX, mapH = texH * ScaleY;
            float w = mapW + 44f, h = mapH + 124f;

            panel = UIFactory.CreateRect("Papiro", canvas.transform).gameObject;
            UIFactory.Place(panel.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -16f), new Vector2(w, h));
            parchment = MakeParchment(96, Mathf.RoundToInt(96f * h / w));
            RectTransform bgRt = UIFactory.CreateRect("Pergaminho", panel.transform);
            var bg = bgRt.gameObject.AddComponent<RawImage>();
            bg.texture = parchment;
            bg.raycastTarget = false;
            UIFactory.Stretch(bgRt);

            Text title = UIFactory.CreateText(panel.transform, "MAPA DO LABIRINTO", 22, ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.font = AncientFont(22);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(w - 24f, 30f));
            Image rule = UIFactory.CreateImage(panel.transform, new Color(ink.r, ink.g, ink.b, 0.7f), "Linha");
            UIFactory.Place(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(w - 50f, 2f));

            RectTransform imgRt = UIFactory.CreateRect("Mapa", panel.transform);
            var raw = imgRt.gameObject.AddComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            UIFactory.Place(imgRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(mapW, mapH));

            legend = UIFactory.CreateText(panel.transform,
                "<color=#4A2C14>▲</color> você      <color=#4A2C14>◎</color> saída      <color=#96623A>···</color> caminho",
                16, ink, TextAnchor.MiddleCenter);
            UIFactory.Place(legend.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(w - 16f, 24f));
            Text hide = UIFactory.CreateText(panel.transform, "", 14, new Color(ink.r, ink.g, ink.b, 0.75f), TextAnchor.MiddleCenter);
            UIFactory.Place(hide.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(w - 16f, 20f));
            panel.SetActive(false);

            label = UIFactory.CreateText(canvas.transform, "", 26, new Color(1f, 0.93f, 0.75f),
                TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.Place(label.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -28f), new Vector2(300f, 34f));
            label.gameObject.SetActive(false);

            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            HealingPlant.SpawnInMaze(this, 14);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (canvas != null) Destroy(canvas.gameObject);
            if (tex != null) Destroy(tex);
            if (parchment != null) Destroy(parchment);
        }

        /// <summary>Chamado pelo papiro ao ser coletado: libera o mapa e já abre a leitura.</summary>
        public void Unlock()
        {
            unlocked = true;
            if (label != null) label.gameObject.SetActive(true);
            SetOpen(true);
        }

        /// <summary>Mostra o caminho até a saída no mapa por alguns segundos (quando uma múmia é derrotada) e abre o mapa.</summary>
        public void RevealRoute(float seconds)
        {
            if (!unlocked) return;
            routeUntil = Time.time + seconds;
            SetOpen(true);
        }

        int CellIndexOf(Vector3 p)
        {
            int c = Mathf.Clamp(Mathf.FloorToInt((p.x - x0) / cell), 0, cols - 1);
            int r = Mathf.Clamp(Mathf.FloorToInt((p.z - z0) / cell), 0, rows - 1);
            return c + r * cols;
        }

        /// <summary>Próximo ponto (centro da próxima célula) no caminho de "from" até "to" pelos corredores do labirinto.</summary>
        public Vector3 Waypoint(Vector3 from, Vector3 to)
        {
            if (hWalls == null || vWalls == null) return to;
            int f = CellIndexOf(from), t = CellIndexOf(to);
            if (f == t) return to;
            if (navDist == null || navTarget != t) BuildDistanceField(t);

            int c = f % cols, r = f / cols, best = f;
            int bestDist = navDist[f];
            if (c + 1 < cols && !V(c + 1, r) && navDist[f + 1] < bestDist) { best = f + 1; bestDist = navDist[best]; }
            if (c - 1 >= 0 && !V(c, r) && navDist[f - 1] < bestDist) { best = f - 1; bestDist = navDist[best]; }
            if (r + 1 < rows && !H(c, r + 1) && navDist[f + cols] < bestDist) { best = f + cols; bestDist = navDist[best]; }
            if (r - 1 >= 0 && !H(c, r) && navDist[f - cols] < bestDist) { best = f - cols; }
            return new Vector3(x0 + (best % cols + 0.5f) * cell, from.y, z0 + (best / cols + 0.5f) * cell);
        }

        void BuildDistanceField(int target)
        {
            navDist = new int[cols * rows];
            for (int i = 0; i < navDist.Length; i++) navDist[i] = int.MaxValue;
            var q = new Queue<int>();
            q.Enqueue(target); navDist[target] = 0;
            while (q.Count > 0)
            {
                int cur = q.Dequeue(), c = cur % cols, r = cur / cols, d = navDist[cur] + 1;
                if (c + 1 < cols && !V(c + 1, r) && navDist[cur + 1] > d) { navDist[cur + 1] = d; q.Enqueue(cur + 1); }
                if (c - 1 >= 0 && !V(c, r) && navDist[cur - 1] > d) { navDist[cur - 1] = d; q.Enqueue(cur - 1); }
                if (r + 1 < rows && !H(c, r + 1) && navDist[cur + cols] > d) { navDist[cur + cols] = d; q.Enqueue(cur + cols); }
                if (r - 1 >= 0 && !H(c, r) && navDist[cur - cols] > d) { navDist[cur - cols] = d; q.Enqueue(cur - cols); }
            }
            navTarget = target;
        }

        void SetOpen(bool value)
        {
            open = value;
            if (panel != null) panel.SetActive(open);
            if (label != null) label.text = "";
            nextRedraw = 0f;
        }

        void Update()
        {
            if (!unlocked || panel == null) return;
            Keyboard k = Keyboard.current;
            if (k != null && k.pKey.wasPressedThisFrame) SetOpen(!open);
            if (!open) return;

            if (player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) player = p.transform;
            }
            if (Time.unscaledTime >= nextRedraw)
            {
                nextRedraw = Time.unscaledTime + 0.1f;
                Redraw();
            }
        }

        /// <summary>Primeira fonte de aparência antiga que existir no computador (cai numa fonte comum se nenhuma existir).</summary>
        static Font AncientFont(int size)
        {
            try
            {
                string[] names = { "Papyrus", "Copperplate Gothic Bold", "Copperplate Gothic Light", "Trajan Pro", "Palatino Linotype", "Book Antiqua", "Constantia", "Georgia" };
                Font f = Font.CreateDynamicFontFromOSFont(names, size);
                if (f != null) return f;
            }
            catch (System.Exception) { }
            return UIFactory.DefaultFont;
        }

        /// <summary>Textura de pergaminho: fibras de ruído, borda queimada e recorte irregular nas pontas.</summary>
        public static Texture2D MakeParchment(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (w - 1f), v = y / (h - 1f);
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    float n = Mathf.PerlinNoise(x * 0.09f + 3.1f, y * 0.09f + 7.7f) * 0.6f + Mathf.PerlinNoise(x * 0.31f, y * 0.31f) * 0.4f;
                    float cut = 0.008f + Mathf.PerlinNoise(x * 0.22f, y * 0.22f) * 0.03f;
                    float burn = Mathf.Clamp01(1f - edge / 0.1f);
                    // papiro: fibras amareladas, manchas suaves de tempo e borda levemente tostada
                    Color c = Color.Lerp(new Color(0.90f, 0.79f, 0.55f), new Color(0.80f, 0.66f, 0.40f), n);
                    float fiber = Mathf.PerlinNoise(x * 0.9f, y * 0.06f);
                    c = Color.Lerp(c, new Color(0.72f, 0.58f, 0.34f), fiber * 0.18f);
                    float stain = Mathf.PerlinNoise(x * 0.05f + 11f, y * 0.05f + 4f);
                    c = Color.Lerp(c, new Color(0.68f, 0.52f, 0.30f), Mathf.SmoothStep(0.6f, 0.9f, stain) * 0.4f);
                    c = Color.Lerp(c, new Color(0.52f, 0.36f, 0.18f), Mathf.Clamp01(burn * burn));
                    px[x + y * w] = new Color(c.r, c.g, c.b, edge < cut ? 0f : 1f);
                }
            t.SetPixels32(px);
            t.Apply(false);
            return t;
        }

        public List<Vector2Int> ComputeRoute()
        {
            route.Clear();
            var prev = new int[cols * rows];
            for (int i = 0; i < prev.Length; i++) prev[i] = -2;
            int start = startCell.x + startCell.y * cols, goal = exitCell.x + exitCell.y * cols;
            var q = new Queue<int>();
            q.Enqueue(start); prev[start] = -1;
            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                if (cur == goal) break;
                int c = cur % cols, r = cur / cols;
                if (c + 1 < cols && !V(c + 1, r) && prev[cur + 1] == -2) { prev[cur + 1] = cur; q.Enqueue(cur + 1); }
                if (c - 1 >= 0 && !V(c, r) && prev[cur - 1] == -2) { prev[cur - 1] = cur; q.Enqueue(cur - 1); }
                if (r + 1 < rows && !H(c, r + 1) && prev[cur + cols] == -2) { prev[cur + cols] = cur; q.Enqueue(cur + cols); }
                if (r - 1 >= 0 && !H(c, r) && prev[cur - cols] == -2) { prev[cur - cols] = cur; q.Enqueue(cur - cols); }
            }
            if (prev[goal] == -2) return route;
            for (int n = goal; n != -1; n = prev[n]) route.Add(new Vector2Int(n % cols, n / cols));
            route.Reverse();
            return route;
        }

        void Fill(Color32[] a, int x, int y, int w, int h, Color32 col)
        {
            for (int j = y; j < y + h; j++)
            {
                if (j < 0 || j >= texH) continue;
                for (int i = x; i < x + w; i++)
                    if (i >= 0 && i < texW) a[i + j * texW] = col;
            }
        }

        // seta marrom apontando para onde o jogador olha
        void Arrow(Color32[] a, float cx, float cy, float fx, float fy, float size, Color32 col)
        {
            Vector2 f = new Vector2(fx, fy).normalized;
            Vector2 n = new Vector2(-f.y, f.x);
            Vector2 p0 = new Vector2(cx, cy) + f * size;
            Vector2 p1 = new Vector2(cx, cy) - f * size * 0.8f + n * size * 0.7f;
            Vector2 p2 = new Vector2(cx, cy) - f * size * 0.8f - n * size * 0.7f;
            Vector2 p3 = new Vector2(cx, cy) - f * size * 0.25f;     // entalhe na traseira
            for (int j = Mathf.FloorToInt(cy - size - 1f); j <= Mathf.CeilToInt(cy + size + 1f); j++)
            {
                if (j < 0 || j >= texH) continue;
                for (int i = Mathf.FloorToInt(cx - size - 1f); i <= Mathf.CeilToInt(cx + size + 1f); i++)
                {
                    if (i < 0 || i >= texW) continue;
                    Vector2 q = new Vector2(i, j);
                    if (InTri(q, p0, p1, p3) || InTri(q, p0, p3, p2)) a[i + j * texW] = col;
                }
            }
        }

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        // círculo suave (borda com meio-tom) em vez de quadrado
        void Disc(Color32[] a, float cx, float cy, float rad, Color32 col)
        {
            int x0i = Mathf.FloorToInt(cx - rad - 1f), x1 = Mathf.CeilToInt(cx + rad + 1f);
            int y0i = Mathf.FloorToInt(cy - rad - 1f), y1 = Mathf.CeilToInt(cy + rad + 1f);
            for (int j = y0i; j <= y1; j++)
            {
                if (j < 0 || j >= texH) continue;
                for (int i = x0i; i <= x1; i++)
                {
                    if (i < 0 || i >= texW) continue;
                    float d = Mathf.Sqrt((i - cx) * (i - cx) + (j - cy) * (j - cy));
                    float cover = Mathf.Clamp01(rad + 0.5f - d);
                    if (cover <= 0f) continue;
                    Color32 old = a[i + j * texW];
                    if (old.a == 0) old = new Color32(col.r, col.g, col.b, 0);
                    float na = Mathf.Max(old.a / 255f, cover);
                    float k = cover;
                    a[i + j * texW] = new Color32(
                        (byte)Mathf.Lerp(old.r, col.r, k), (byte)Mathf.Lerp(old.g, col.g, k),
                        (byte)Mathf.Lerp(old.b, col.b, k), (byte)(na * 255f));
                }
            }
        }

        Vector2Int CellCenter(Vector2Int c) { return new Vector2Int(c.x * S + 1 + S / 2, c.y * S + 1 + S / 2); }

        void DrawBase(Color32[] basePx, bool withRoute)
        {
            for (int i = 0; i < basePx.Length; i++) basePx[i] = Clear;

            Vector2Int ec = CellCenter(exitCell);
            Disc(basePx, ec.x, ec.y, 5.4f, new Color32(74, 44, 20, 255));
            Disc(basePx, ec.x, ec.y, 3.6f, new Color32(226, 196, 140, 255));
            Disc(basePx, ec.x, ec.y, 1.8f, new Color32(74, 44, 20, 255));
            Vector2Int sc = CellCenter(startCell);
            Disc(basePx, sc.x, sc.y, 2.6f, StartInk);
            Disc(basePx, sc.x, sc.y, 1.2f, new Color32(226, 196, 140, 255));

            for (int i = 0; withRoute && i + 1 < route.Count; i++)
            {
                Vector2Int a = CellCenter(route[i]), b = CellCenter(route[i + 1]);
                int dx = b.x - a.x, dy = b.y - a.y, steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                for (int s = 0; s <= steps; s += 3)
                    Disc(basePx, a.x + dx * s / Mathf.Max(1, steps), a.y + dy * s / Mathf.Max(1, steps), 1.5f, RouteInk);
            }

            for (int r = 0; r <= rows; r++)
                for (int c = 0; c < cols; c++)
                    if (H(c, r)) Fill(basePx, c * S, r * S, S + 2, 2, WallInk);
            for (int c = 0; c <= cols; c++)
                for (int r = 0; r < rows; r++)
                    if (V(c, r)) Fill(basePx, c * S, r * S, 2, S + 2, WallInk);
        }

        void Redraw()
        {
            bool showRoute = Time.time < routeUntil;
            System.Array.Copy(showRoute ? baseRoute : basePx, px, basePx.Length);
            if (legend != null)
                legend.text = showRoute
                    ? "<color=#96623A>CAMINHO REVELADO!  " + Mathf.CeilToInt(routeUntil - Time.time) + "s</color>"
                    : "<color=#4A2C14>▲</color> você      <color=#4A2C14>◎</color> saída      <color=#96623A>···</color> caminho";
            if (player != null)
            {
                float fx = (player.position.x - x0) / cell * S + 1f;
                float fy = (player.position.z - z0) / cell * S + 1f;
                int cx = Mathf.RoundToInt(fx), cy = Mathf.RoundToInt(fy);
                Vector3 f = player.forward; f.y = 0f; f.Normalize();
                Arrow(px, cx, cy, f.x, f.z, 7.5f, new Color32(60, 34, 14, 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false);
        }
    }
}
