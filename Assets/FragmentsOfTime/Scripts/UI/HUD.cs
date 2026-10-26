using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace RiftOfTime
{
    /// <summary>HUD do storyboard: contador de fragmentos, barra de vida vertical, cronômetro, dica "Clique".</summary>
    public class HUD : MonoBehaviour
    {
        public static HUD Instance { get; private set; }

        public bool IsReading { get; private set; }
        public float InteractBlockedUntil { get; private set; }

        public event Action ResumeRequested;
        public event Action MenuRequested;

        Canvas canvas;
        GameObject gameplayRoot;
        Text objectiveText;
        Text fragText, timerText, bannerText, subtitleText, promptText, messageText, bossName, readTitle, readBody;
        RectTransform healthFill, bossFill;
        Image timerFill;
        float timerMax;
        GameObject timerPanel, bossRoot, readPanel, pausePanel;
        RawImage readImage;
        Image damageFlash;
        GameObject gameOverPanel;
        float flashAlpha;
        Coroutine subtitleRoutine;

        // dano: sem clarão vermelho a cada golpe; só com a vida baixa, respingos de sangue (vinho) e, perto da morte, visão turva
        const float SplatterBelow = 0.4f, BlurFrom = 0.4f, BlurFull = 0.12f;
        float health01 = 1f;
        bool pendingHit;
        Texture2D[] splatTex;
        Volume lowVolume;
        DepthOfField lowDof;
        Vignette lowVig;
        static readonly Color Wine = new Color(0.27f, 0.012f, 0.045f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        public static HUD Create()
        {
            return new GameObject("HUD").AddComponent<HUD>();
        }

        void Awake()
        {
            Instance = this;
            UIFactory.EnsureEventSystem();
            Build();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (lowVolume != null) Destroy(lowVolume.gameObject);
        }

        static readonly Color Cream = new Color(1f, 0.88f, 0.65f);
        static readonly Color Dark = new Color(0f, 0f, 0f, 0.55f);

        void Build()
        {
            canvas = UIFactory.CreateCanvas("HUDCanvas", 100);
            canvas.transform.SetParent(transform, false);
            Transform root = canvas.transform;

            gameplayRoot = UIFactory.CreateRect("Gameplay", root).gameObject;
            UIFactory.Stretch(gameplayRoot.GetComponent<RectTransform>());
            Transform g = gameplayRoot.transform;

            Image fragPanel = UIFactory.CreateImage(g, Dark, "Fragments", true);
            UIFactory.Place(fragPanel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -30), new Vector2(200, 66));
            Image diamond = UIFactory.CreateImage(fragPanel.transform, new Color(1f, 0.82f, 0.3f), "Diamond");
            UIFactory.Place(diamond.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40, 0), new Vector2(26, 26));
            diamond.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
            fragText = UIFactory.CreateText(fragPanel.transform, "0/3", 40, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold, "Count");
            UIFactory.SetAnchors(fragText.rectTransform, Vector2.zero, Vector2.one, new Vector2(74, 0), new Vector2(0, 0));

            fragPanel.gameObject.SetActive(false);      // sem a contagem de fragmentos

            Image bar = UIFactory.CreateImage(g, new Color(0.96f, 0.93f, 0.84f, 0.92f), "HealthBar", true);
            UIFactory.Place(bar.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 130), new Vector2(32, 430));
            RectTransform inner = UIFactory.CreateRect("Inner", bar.transform);
            UIFactory.SetAnchors(inner, Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -5));
            Image fill = UIFactory.CreateImage(inner, new Color(0.4f, 0.44f, 0.12f), "Fill");
            healthFill = fill.rectTransform;
            UIFactory.SetAnchors(healthFill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bar.gameObject.SetActive(false);            // sem a barra de vida na tela

            // contagem do colapso: só um círculo vinho no canto direito que se esvazia
            RectTransform tp = UIFactory.CreateRect("TimerPanel", g);
            timerPanel = tp.gameObject;
            UIFactory.Place(tp, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-48, 48), new Vector2(190, 190));
            Image ringBack = UIFactory.CreateImage(tp, new Color(0f, 0f, 0f, 0.5f), "RingBack");
            ringBack.sprite = RingSprite();
            ringBack.raycastTarget = false;
            UIFactory.Stretch(ringBack.rectTransform);
            timerFill = UIFactory.CreateImage(tp, new Color(0.55f, 0.06f, 0.16f, 1f), "RingFill");
            timerFill.sprite = RingSprite();
            timerFill.type = Image.Type.Filled;
            timerFill.fillMethod = Image.FillMethod.Radial360;
            timerFill.fillOrigin = (int)Image.Origin360.Top;
            timerFill.fillClockwise = false;
            timerFill.raycastTarget = false;
            UIFactory.Stretch(timerFill.rectTransform);
            timerText = UIFactory.CreateText(tp, "03:00", 40, new Color(0.9f, 0.82f, 0.82f), TextAnchor.MiddleCenter, FontStyle.Bold, "Timer");
            UIFactory.Stretch(timerText.rectTransform);
            timerText.enabled = false;      // só o anel, sem números
            timerPanel.SetActive(false);

            objectiveText = UIFactory.CreateText(g, "", 32, Color.white, TextAnchor.MiddleRight, FontStyle.Normal, "Objective");
            objectiveText.font = HorrorTitleStyle.SansFont;
            UIFactory.Place(objectiveText.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(900, 70));
            objectiveText.gameObject.AddComponent<Outline>().effectColor = Color.black;
            objectiveText.raycastTarget = false;

            bannerText = UIFactory.CreateText(g, "", 72, Cream, TextAnchor.MiddleCenter, FontStyle.Bold, "Banner");
            UIFactory.Place(bannerText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(1500, 110));
            bannerText.gameObject.AddComponent<Outline>().effectColor = new Color(0.15f, 0.08f, 0.02f, 1f);

            bossRoot = UIFactory.CreateRect("Boss", g).gameObject;
            UIFactory.Place(bossRoot.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(700, 60));
            bossName = UIFactory.CreateText(bossRoot.transform, "Guardiã", 30, Color.white, TextAnchor.UpperCenter, FontStyle.Bold, "Name");
            UIFactory.Stretch(bossName.rectTransform);
            Image bbg = UIFactory.CreateImage(bossRoot.transform, new Color(0, 0, 0, 0.6f), "Bg", true);
            UIFactory.SetAnchors(bbg.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 22));
            RectTransform binner = UIFactory.CreateRect("Inner", bbg.transform);
            UIFactory.SetAnchors(binner, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
            Image bfill = UIFactory.CreateImage(binner, new Color(0.7f, 0.2f, 0.15f), "Fill");
            bossFill = bfill.rectTransform;
            UIFactory.SetAnchors(bossFill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bossRoot.SetActive(false);

            Image dot = UIFactory.CreateImage(g, new Color(1, 1, 1, 0.7f), "Crosshair", true);
            UIFactory.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8, 8));
            dot.gameObject.SetActive(false);            // sem o ponto no meio da tela

            Image hint = UIFactory.CreateImage(g, Dark, "Hint", true);
            UIFactory.Place(hint.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 30), new Vector2(380, 150));
            Image chip = UIFactory.CreateImage(hint.transform, new Color(0.99f, 0.84f, 0.67f), "Chip", true);
            UIFactory.Place(chip.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(180, 54));
            Text chipT = UIFactory.CreateText(chip.transform, "Clique", 32, new Color(0.45f, 0.36f, 0.33f), TextAnchor.MiddleCenter, FontStyle.Bold, "Label");
            UIFactory.Stretch(chipT.rectTransform);
            Text hintT = UIFactory.CreateText(hint.transform, "golpear  •  [Q / Ctrl] esquivar  •  [E] interagir", 22, Color.white, TextAnchor.LowerCenter, FontStyle.Normal, "Text");
            UIFactory.SetAnchors(hintT.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 8), new Vector2(-10, -70));
            hint.gameObject.SetActive(false);           // sem o quadro "Clique / golpear / esquivar / interagir"

            subtitleText = UIFactory.CreateText(root, "", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal, "Subtitle");
            UIFactory.Place(subtitleText.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 55), new Vector2(1300, 130));
            subtitleText.gameObject.AddComponent<Outline>().effectColor = Color.black;

            promptText = UIFactory.CreateText(root, "", 38, Cream, TextAnchor.MiddleCenter, FontStyle.Bold, "Prompt");
            UIFactory.Place(promptText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(1000, 70));
            promptText.gameObject.AddComponent<Outline>().effectColor = Color.black;

            damageFlash = UIFactory.CreateImage(root, new Color(0.36f, 0.025f, 0.06f, 0f), "Damage");
            UIFactory.Stretch(damageFlash.rectTransform);
            damageFlash.raycastTarget = false;

            messageText = UIFactory.CreateText(root, "", 80, new Color(0.9f, 0.2f, 0.15f), TextAnchor.MiddleCenter, FontStyle.Bold, "Message");
            UIFactory.Place(messageText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1600, 140));
            messageText.gameObject.AddComponent<Outline>().effectColor = Color.black;

            Image rp = UIFactory.CreateImage(root, new Color(1f, 1f, 1f, 0f), "Readable", true);
            readPanel = rp.gameObject;
            UIFactory.Place(rp.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 640));
            RectTransform paperRt = UIFactory.CreateRect("Papiro", rp.transform);
            var paper = paperRt.gameObject.AddComponent<RawImage>();
            paper.texture = LabyrinthMap.MakeParchment(220, 128);
            paper.raycastTarget = false;
            UIFactory.Stretch(paperRt);
            readTitle = UIFactory.CreateText(rp.transform, "", 54, new Color(0.3f, 0.18f, 0.08f), TextAnchor.UpperCenter, FontStyle.Bold, "Title");
            UIFactory.SetAnchors(readTitle.rectTransform, Vector2.zero, Vector2.one, new Vector2(40, 40), new Vector2(-40, -30));
            readBody = UIFactory.CreateText(rp.transform, "", 32, new Color(0.25f, 0.15f, 0.08f), TextAnchor.UpperLeft, FontStyle.Italic, "Body");
            UIFactory.SetAnchors(readBody.rectTransform, Vector2.zero, Vector2.one, new Vector2(60, 70), new Vector2(-60, -120));
            Text close = UIFactory.CreateText(rp.transform, "[E] fechar", 26, new Color(0.4f, 0.28f, 0.15f), TextAnchor.LowerCenter, FontStyle.Normal, "Close");
            UIFactory.SetAnchors(close.rectTransform, Vector2.zero, Vector2.one, new Vector2(0, 16), new Vector2(0, 0));
            GameObject qr = new GameObject("Imagem", typeof(RectTransform), typeof(RawImage));
            qr.transform.SetParent(rp.transform, false);
            readImage = qr.GetComponent<RawImage>();
            readImage.raycastTarget = false;
            UIFactory.Place(readImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(460, 460));
            qr.SetActive(false);
            readPanel.SetActive(false);

            BuildPause(root);
        }

        void BuildPause(Transform root)
        {
            Image p = UIFactory.CreateImage(root, new Color(0f, 0f, 0f, 0.82f), "Pause");
            pausePanel = p.gameObject;
            UIFactory.Stretch(p.rectTransform);
            HorrorTitleStyle.CreateTitle(p.transform, "FRAGMENTS", "OF TIME");
            HorrorTitleStyle.CreateEntry(p.transform, "Continuar", 32, () => { MenuSounds.Bell(); if (ResumeRequested != null) ResumeRequested(); }, "Continuar", 0.375f);
            HorrorTitleStyle.CreateEntry(p.transform, "Voltar ao menu", 32, () => { if (MenuRequested != null) MenuRequested(); }, "VoltarMenu", 0.315f);
            pausePanel.SetActive(false);
        }

        void Update()
        {
            if ((pausePanel != null && pausePanel.activeSelf) || (gameOverPanel != null && gameOverPanel.activeSelf)) TvGlitchClock.Tick();
            if (flashAlpha > 0f)
            {
                flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.unscaledDeltaTime * 1.8f);
                Color c = damageFlash.color;
                c.a = flashAlpha;
                damageFlash.color = c;
            }

            if (IsReading && Keyboard.current != null &&
                (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame ||
                 (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)))
                HideReadable();
        }

        public void SetGameplayVisible(bool visible) { gameplayRoot.SetActive(visible); }

        public void SetFragments(int n) { fragText.text = n + "/" + GameManager.TotalFragments; }

        public void SetHealth(float t01)
        {
            t01 = Mathf.Clamp01(t01);
            bool lost = t01 < health01 - 0.001f;
            health01 = t01;
            healthFill.anchorMax = new Vector2(1f, t01);
            if (pendingHit && lost && t01 <= SplatterBelow) SpawnSplatters(t01);
            pendingHit = false;
            UpdateLowHealth();
        }

        /// <summary>Levou um golpe. Só mostra sangue na tela se a vida ficar baixa (decidido em SetHealth).</summary>
        public void FlashDamage() { pendingHit = true; }

        /// <summary>Clarão vermelho curto, igual ao da vida baixa (armadilhas).</summary>
        public void FlashRed(float alpha)
        {
            flashAlpha = Mathf.Max(flashAlpha, alpha);
            if (damageFlash != null) { Color c = damageFlash.color; c.a = flashAlpha; damageFlash.color = c; }
        }

        // ---- respingos de sangue na tela: muitos, pequenos, grossos e cor de vinho
        void SpawnSplatters(float t01)
        {
            if (splatTex == null) splatTex = MakeSplatTextures();
            int count = t01 < 0.15f ? UnityEngine.Random.Range(26, 34) : UnityEngine.Random.Range(16, 24);
            // 2 ou 3 pontos de impacto: as gotas se agrupam em volta deles, com algumas soltas pela tela
            int centers = UnityEngine.Random.Range(2, 4);
            var cs = new Vector2[centers];
            for (int c = 0; c < centers; c++) cs[c] = new Vector2(UnityEngine.Random.Range(0.12f, 0.88f), UnityEngine.Random.Range(0.2f, 0.85f));
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Respingo", typeof(RectTransform));
                go.transform.SetParent(canvas.transform, false);
                go.transform.SetSiblingIndex(damageFlash.transform.GetSiblingIndex());
                var img = go.AddComponent<RawImage>();
                img.texture = splatTex[UnityEngine.Random.Range(0, splatTex.Length)];
                float shade = UnityEngine.Random.Range(0.8f, 1.1f);
                img.color = new Color(shade, shade, shade, UnityEngine.Random.Range(0.93f, 1f));
                img.raycastTarget = false;
                var rt = img.rectTransform;
                Vector2 pos = UnityEngine.Random.value < 0.75f
                    ? cs[UnityEngine.Random.Range(0, centers)] + new Vector2(UnityEngine.Random.Range(-0.14f, 0.14f), UnityEngine.Random.Range(-0.2f, 0.2f))
                    : new Vector2(UnityEngine.Random.value, UnityEngine.Random.value);
                rt.anchorMin = rt.anchorMax = new Vector2(Mathf.Clamp01(pos.x), Mathf.Clamp01(pos.y));
                float size = Mathf.Pow(UnityEngine.Random.value, 1.8f) * 130f + 36f;        // quase todas pequenas, poucas maiores
                size *= Screen.height / 1080f;
                bool drip = UnityEngine.Random.value < 0.18f;
                rt.sizeDelta = new Vector2(size, size * (drip ? UnityEngine.Random.Range(1.3f, 1.8f) : 1f));
                rt.localRotation = Quaternion.identity;   // o reflexo fica sempre no alto
                StartCoroutine(FadeSplat(img, UnityEngine.Random.Range(7f, 11f), drip));
            }
        }

        IEnumerator FadeSplat(RawImage img, float seconds, bool drip)
        {
            Color c0 = img.color;
            for (float t = 0f; t < seconds && img != null; t += Time.unscaledDeltaTime)
            {
                float k = t / seconds;
                var c = c0; c.a = c0.a * (k < 0.5f ? 1f : 1f - (k - 0.5f) / 0.5f);
                img.color = c;
                img.rectTransform.anchoredPosition -= new Vector2(0f, (drip ? 14f : 4f) * Time.unscaledDeltaTime * (0.4f + k));   // escorre devagar
                yield return null;
            }
            if (img != null) Destroy(img.gameObject);
        }

        // gotas redondas, cheias e brilhantes: vinho no miolo, mais escuro na borda e um reflexo claro no alto
        static Texture2D[] MakeSplatTextures()
        {
            var list = new Texture2D[5];
            for (int v = 0; v < list.Length; v++)
            {
                const int N = 96;
                var rng = new System.Random(300 + v * 17);
                float ph1 = (float)(rng.NextDouble() * 6.28f), ph2 = (float)(rng.NextDouble() * 6.28f);
                var pix = new Color32[N * N];
                float c = N / 2f, radius = N * 0.42f;
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = x - c, dy = y - c;
                        float ang = Mathf.Atan2(dy, dx);
                        float r = radius * (1f + 0.035f * Mathf.Sin(ang * 3f + ph1) + 0.02f * Mathf.Sin(ang * 5f + ph2));
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                        float a = 1f - Mathf.SmoothStep(0.94f, 1f, d);
                        // cor: miolo vinho vivo, borda escura
                        float edge = Mathf.SmoothStep(0.55f, 1f, d);
                        Color col = Color.Lerp(new Color(0.50f, 0.035f, 0.08f), new Color(0.22f, 0.01f, 0.035f), edge);
                        // reflexo molhado no alto à esquerda
                        float hx = dx + radius * 0.32f, hy = dy - radius * 0.34f;
                        float h = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy * 1.6f) / (radius * 0.3f));
                        col = Color.Lerp(col, new Color(0.95f, 0.45f, 0.5f), h * h * 0.65f);
                        pix[y * N + x] = new Color32((byte)(col.r * 255f), (byte)(col.g * 255f), (byte)(col.b * 255f), (byte)(a * 255f));
                    }
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, true);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.SetPixels32(pix);
                tex.Apply();
                list[v] = tex;
            }
            return list;
        }

        // ---- visão turva perto da morte (Volume global do URP, criado só quando precisa)
        bool calmVision;
        float dazeUntil;

        /// <summary>Visão turva passageira (golpe forte): some devagar.</summary>
        public void Daze(float seconds) { dazeUntil = Time.time + seconds; }

        /// <summary>Na morte a visão turva fica fixa (num nível em que a cena ainda aparece), inclusive no game over.</summary>
        public void ClearLowHealth()
        {
            calmVision = true;
        }

        void UpdateLowHealth()
        {
            float w = calmVision ? 0.7f : Mathf.Clamp01((BlurFrom - health01) / (BlurFrom - BlurFull));
            w = Mathf.Max(w, Mathf.Clamp01(dazeUntil - Time.time) * 0.7f);
            if (w <= 0.001f && lowVolume == null) return;
            if (lowVolume == null)
            {
                var vgo = new GameObject("VolumeVidaBaixa");
                lowVolume = vgo.AddComponent<Volume>();
                lowVolume.isGlobal = true;
                lowVolume.priority = 60;
                var prof = ScriptableObject.CreateInstance<VolumeProfile>();
                lowDof = prof.Add<DepthOfField>(true);
                lowDof.mode.Override(DepthOfFieldMode.Gaussian);
                lowDof.gaussianStart.Override(0f);
                lowDof.gaussianEnd.Override(0.2f);
                lowDof.gaussianMaxRadius.Override(1.5f);
                lowVig = prof.Add<Vignette>(true);
                lowVig.color.Override(new Color(0.2f, 0f, 0.03f));
                lowVig.intensity.Override(0.3f);
                lowVig.smoothness.Override(0.6f);
                lowVolume.sharedProfile = prof;
            }
            lowVolume.weight = w;
            lowDof.active = w > 0.02f;
        }

        void LateUpdate()
        {
            if (lowVolume != null && lowVolume.weight > 0.01f)
            {
                // a visão pulsa com o coração, cada vez mais rápido
                float w = lowVolume.weight;
                float beat = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(5f, 9f, w));
                lowVig.intensity.value = Mathf.Lerp(0.3f, 0.5f, w) + beat * 0.1f * w;
                lowDof.gaussianMaxRadius.value = Mathf.Lerp(0.6f, 1.5f, w) + beat * 0.2f * w;
            }
        }

        public void ShowTimer(bool show) { timerPanel.SetActive(show); if (show) timerMax = 0f; }

        public void SetTimer(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            timerText.text = (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
            timerMax = Mathf.Max(timerMax, seconds);
            timerFill.fillAmount = timerMax > 0f ? Mathf.Clamp01(seconds / timerMax) : 1f;
        }

        static Sprite ringSprite;
        static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01((1f - r) * c) * Mathf.Clamp01((r - 0.78f) * c);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            ringSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return ringSprite;
        }

        /// <summary>Objetivo no canto inferior direito (vazio = some).</summary>
        public void SetObjective(string text) { if (objectiveText != null) objectiveText.text = (text ?? "").ToUpper(); }

        public void ShowBanner(string text) { bannerText.text = text; }

        public void ShowBoss(string displayName, float t01)
        {
            bossName.text = displayName;
            bossRoot.SetActive(true);
            SetBoss(t01);
        }

        public void SetBoss(float t01) { bossFill.anchorMax = new Vector2(Mathf.Clamp01(t01), 1f); }

        public void HideBoss() { bossRoot.SetActive(false); }

        public void SetPrompt(string text)
        {
            if (promptText == null) return;
            promptText.text = text ?? "";
        }

        public void ShowSubtitle(string text, float seconds)
        {
            if (subtitleRoutine != null) { StopCoroutine(subtitleRoutine); subtitleRoutine = null; }
            subtitleText.text = ""; if (voice != null) voice.Stop();
        }

        /// <summary>Legenda de diálogo (a ShowSubtitle continua desligada): mostra o texto por alguns segundos.</summary>
        public void ShowDialogue(string text, float seconds, bool withVoice = true)
        {
            if (subtitleRoutine != null) StopCoroutine(subtitleRoutine);
            subtitleRoutine = StartCoroutine(SubtitleRoutine(text, seconds));
            if (withVoice && !text.StartsWith("Leo:")) PlayVoice(seconds);
        }

        AudioSource voice;
        AudioClip voiceClip;

        /// <summary>Voz do personagem: toca um trecho da gravação enquanto a legenda está na tela.</summary>
        void PlayVoice(float seconds)
        {
            if (voiceClip == null) voiceClip = Resources.Load<AudioClip>("Voz/voz_marco");
            if (voiceClip == null) return;
            if (voice == null)
            {
                voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0f;
                voice.volume = 0.9f;
            }
            voice.clip = voiceClip;
            voice.time = UnityEngine.Random.Range(20f, Mathf.Max(21f, voiceClip.length - 60f));
            voice.Play();
        }

        IEnumerator SubtitleRoutine(string text, float seconds)
        {
            subtitleText.text = text;
            yield return new WaitForSecondsRealtime(seconds);
            subtitleText.text = "";
            if (voice != null) voice.Stop();
            subtitleRoutine = null;
        }

        /// <summary>Véu escuro translúcido (a cena turva continua visível ao fundo) com GAME OVER na fonte do título, a música do menu e as opções de tentar de novo ou voltar ao menu.</summary>
        public void ShowGameOver(Action retry, Action menu)
        {
            if (gameOverPanel != null) return;
            Image p = UIFactory.CreateImage(canvas.transform, new Color(0f, 0f, 0f, 0.55f), "GameOver");
            gameOverPanel = p.gameObject;
            UIFactory.Stretch(p.rectTransform);
            HorrorTitleStyle.CreateTitle(p.transform, "GAME OVER", " ");
            HorrorTitleStyle.CreateEntry(p.transform, "Tentar de novo", 32, () => { MenuSounds.Bell(); MenuSounds.StopMusic(false); if (retry != null) retry(); }, "TentarDeNovo", 0.375f);
            HorrorTitleStyle.CreateEntry(p.transform, "Voltar ao menu", 32, () => { MenuSounds.StopMusic(false); if (menu != null) menu(); }, "VoltarMenuGO", 0.315f);
            var group = gameOverPanel.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            MenuCursor.Set();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            MenuSounds.PlayMusic();
            StartCoroutine(FadeIn(group, 1.2f));
        }

        IEnumerator FadeIn(CanvasGroup g, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                g.alpha = t / seconds;
                yield return null;
            }
            g.alpha = 1f;
        }

        public void ShowMessage(string text) { messageText.text = text; }

        public void ShowReadable(string title, string body)
        {
            readTitle.text = title;
            readBody.text = body;
            readImage.gameObject.SetActive(false);
            PlaceReadPanel(false);
            readPanel.SetActive(true);
            IsReading = true;
        }

        // com o QR, o papel sobe e fica menor para a legenda aparecer embaixo dele
        void PlaceReadPanel(bool qr)
        {
            RectTransform rt = readPanel.GetComponent<RectTransform>();
            rt.anchoredPosition = qr ? new Vector2(0f, 130f) : Vector2.zero;
            rt.sizeDelta = qr ? new Vector2(700f, 560f) : new Vector2(1100f, 640f);
            readImage.rectTransform.sizeDelta = qr ? new Vector2(400f, 400f) : new Vector2(460f, 460f);
        }

        /// <summary>Papiro só com uma imagem no meio (o QR code do AR), sem texto.</summary>
        public void ShowReadableImage(Texture image)
        {
            readTitle.text = "";
            readBody.text = "";
            readImage.texture = image;
            readImage.gameObject.SetActive(image != null);
            PlaceReadPanel(true);
            readPanel.SetActive(true);
            IsReading = true;
        }

        void HideReadable()
        {
            readPanel.SetActive(false);
            IsReading = false;
            InteractBlockedUntil = Time.unscaledTime + 0.35f;
        }

        public void ShowPause(bool show)
        {
            pausePanel.SetActive(show);
            if (show) MenuCursor.Set(); else MenuCursor.Reset();
        }
    }
}
