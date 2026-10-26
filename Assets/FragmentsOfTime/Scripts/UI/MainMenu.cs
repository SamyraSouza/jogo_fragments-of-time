using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections;

namespace RiftOfTime
{
    /// <summary>Tela 00 – Tela inicial.</summary>
    public class MainMenu : MonoBehaviour
    {
        public AudioClip windClip;

        [Header("Vídeo de fundo (Assets/StreamingAssets)")]
        const string videoFile = "video-menu-pingpong.mp4";
        public float skipAfter = 1.5f;

        bool reversing, seeking;
        float reverseFrom, reverseStartTime, seekSetAt;

        VideoPlayer vp;
        RawImage videoImage;
        RenderTexture videoRT;
        AudioSource videoAudio;
        bool videoReady, starting, videoEnded;
        float videoW, videoH;
        RectTransform darkening;
        Material glitchMat;
        float prepareStart;

        Button startBtn, continueBtn, optionsBtn, quitBtn;
        GameObject optionsPanel, mainGroup;
        Text volumeText;
        Camera cam;
        Vector3 camBasePos;
        Quaternion camBaseRot;

        void Start()
        {
            GameManager gm = GameManager.Ensure();
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            MenuCursor.Set();
            UIFactory.EnsureEventSystem();

            cam = Camera.main;
            if (cam != null)
            {
                cam.cullingMask = 0;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                camBasePos = cam.transform.position; camBaseRot = cam.transform.rotation;
            }

            HideOldBackground();
            TvGlitchClock.Reset();
            BuildVideo();
            BuildUI(gm);

            gm.SetFragments(0);
            if (!MenuSounds.PlayMusic(0.5f))
            {
                AudioDirector.Instance.Loop("ambient", "ambient", 0.35f);
                if (windClip != null) AudioDirector.Instance.Loop("wind", windClip, 0.4f);
            }
        }

        void Update()
        {
            TvGlitchClock.Tick();
            if (glitchMat != null)
            {
                glitchMat.SetFloat("_Glitch", TvGlitchClock.Bursting ? 1f : 0f);
                glitchMat.SetFloat("_Frame", TvGlitchClock.FrameId);
            }
            UpdateVideo();
        }

        void HideOldBackground()
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (root == gameObject) continue;
                if (root.GetComponentInChildren<MainMenu>(true) != null) continue;
                if (root.GetComponentInChildren<Camera>(true) != null) continue;
                if (root.GetComponentInChildren<AudioListener>(true) != null) continue;
                if (root.GetComponentInChildren<EventSystem>(true) != null) continue;
                if (root.GetComponentInChildren<GameManager>(true) != null) continue;
                if (root.GetComponentInChildren<Renderer>(true) != null || root.GetComponentInChildren<Light>(true) != null)
                    root.SetActive(false);
            }
        }

        void BuildVideo()
        {
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, videoFile);
            if (!System.IO.File.Exists(path)) { Debug.LogWarning("[Menu] vídeo não encontrado: " + path); return; }

            var go = new GameObject("VideoDeFundo");
            go.transform.SetParent(transform, false);
            videoAudio = go.AddComponent<AudioSource>();
            videoAudio.playOnAwake = false;
            videoAudio.spatialBlend = 0f;
            vp = go.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.source = VideoSource.Url;
            vp.url = path;
            vp.isLooping = true;
            vp.skipOnDrop = true;
            vp.waitForFirstFrame = true;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.audioOutputMode = VideoAudioOutputMode.None;
            vp.prepareCompleted += OnVideoPrepared;
            vp.errorReceived += (v, msg) =>
            {
                Debug.LogWarning("[Menu] erro no vídeo: " + msg);
                videoReady = false;
                if (videoImage != null) videoImage.gameObject.SetActive(false);
            };
            vp.loopPointReached += v => videoEnded = true;
            prepareStart = Time.realtimeSinceStartup;
            vp.Prepare();
        }

        void OnVideoPrepared(VideoPlayer v)
        {
            videoW = Mathf.Max(16f, v.width); videoH = Mathf.Max(16f, v.height);
            videoRT = new RenderTexture((int)videoW, (int)videoH, 0);
            v.targetTexture = videoRT;
            if (videoImage != null) { videoImage.texture = videoRT; videoImage.gameObject.SetActive(true); }
            v.time = 0;
            v.Play();
            videoReady = true;
            Debug.Log("[Menu] vídeo pronto em " + (Time.realtimeSinceStartup - prepareStart).ToString("F1") + " s: " + videoW + "x" + videoH + ", " + v.length.ToString("F1") + " s, loop com ida e volta");
        }

        void UpdateVideo()
        {
            if (vp == null || !videoReady) return;
            FitVideo();
        }

        void FitVideo()
        {
            if (videoImage == null || videoW <= 0f) return;
            float screenA = (float)Screen.width / Mathf.Max(1, Screen.height), vidA = videoW / videoH;
            Rect r = new Rect(0f, 0f, 1f, 1f);
            if (screenA > vidA) { r.height = vidA / screenA; r.y = (1f - r.height) * 0.5f; }
            else { r.width = screenA / vidA; r.x = (1f - r.width) * 0.5f; }
            videoImage.uvRect = r;
        }

        void OnDestroy()
        {
            if (videoRT != null) videoRT.Release();
        }

        void BuildUI(GameManager gm)
        {
            Canvas canvas = UIFactory.CreateCanvas("MenuCanvas", 10);
            canvas.transform.SetParent(transform, false);
            Transform root = canvas.transform;

            if (vp != null)
            {
                var go = new GameObject("Video", typeof(RectTransform));
                go.transform.SetParent(root, false);
                videoImage = go.AddComponent<RawImage>();
                videoImage.raycastTarget = false;
                videoImage.color = Color.white;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                go.transform.SetSiblingIndex(0);
                go.SetActive(false);
                if (videoRT != null) { videoImage.texture = videoRT; go.SetActive(true); }

                Shader sh = Shader.Find("RiftOfTime/TvGlitchUI");
                if (sh != null) { glitchMat = new Material(sh); videoImage.material = glitchMat; }

                darkening = HorrorTitleStyle.CreateDarkening(root, 1);
            }

            mainGroup = UIFactory.CreateRect("Main", root).gameObject;
            UIFactory.Stretch(mainGroup.GetComponent<RectTransform>());
            Transform m = mainGroup.transform;

            HorrorTitleStyle.CreateTitle(m, "FRAGMENTS", "OF TIME");
            startBtn = HorrorTitleStyle.CreateEntry(m, "Iniciar Expedição", 32, OnStart, "Iniciar", 0.375f);
            quitBtn = HorrorTitleStyle.CreateEntry(m, "Sair", 32, OnQuit, "Sair", 0.315f);
            HorrorTitleStyle.CreateCopyright(m, "©FRAGMENTS OF TIME. TODOS OS DIREITOS RESERVADOS.");

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(startBtn.gameObject);
        }

        void BuildOptions(Transform root)
        {
            Image p = UIFactory.CreateImage(root, new Color(0.08f, 0.1f, 0.2f, 0.92f), "Options", true);
            optionsPanel = p.gameObject;
            UIFactory.Place(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 760));

            Text t = UIFactory.CreateText(p.transform, "Opções", 80, new Color(0.99f, 0.84f, 0.62f), TextAnchor.MiddleCenter, FontStyle.Bold, "Title");
            UIFactory.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(900, 110));

            volumeText = UIFactory.CreateText(p.transform, "", 44, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, "Volume");
            UIFactory.Place(volumeText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(500, 80));
            Button minus = UIFactory.CreateButton(p.transform, "-", new Vector2(110, 90), () => ChangeVolume(-0.1f), 56, "VolMinus");
            UIFactory.Place(minus.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-330, 180), new Vector2(110, 90));
            Button plus = UIFactory.CreateButton(p.transform, "+", new Vector2(110, 90), () => ChangeVolume(0.1f), 56, "VolPlus");
            UIFactory.Place(plus.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(330, 180), new Vector2(110, 90));

            Button full = UIFactory.CreateButton(p.transform, "Tela cheia: alternar", new Vector2(640, 100), () => Screen.fullScreen = !Screen.fullScreen, 38, "Full");
            UIFactory.Place(full.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(640, 100));

            Button back = UIFactory.CreateButton(p.transform, "Voltar", new Vector2(420, 100), CloseOptions, 40, "Back");
            UIFactory.Place(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 50), new Vector2(420, 100));

            RefreshVolume();
            optionsPanel.SetActive(false);
        }

        void ChangeVolume(float delta)
        {
            GameManager.MasterVolume = GameManager.MasterVolume + delta;
            RefreshVolume();
        }

        void RefreshVolume() { volumeText.text = "Volume: " + Mathf.RoundToInt(GameManager.MasterVolume * 100f) + "%"; }

        void OnStart()
        {
            Debug.Log("[Menu] Iniciar Expedição clicado");
            if (starting) return;
            starting = true;
            MenuCursor.Reset();
            MenuSounds.StopMusic();
            MenuSounds.Bell();
            if (vp != null) vp.Stop();
            GameManager.Ensure().NewGame();
        }

        IEnumerator PlayIntroThenStart()
        {
            var cg = mainGroup.GetComponent<CanvasGroup>();
            if (cg == null) cg = mainGroup.AddComponent<CanvasGroup>();
            cg.interactable = false; cg.blocksRaycasts = false;
            if (AudioDirector.Instance != null) { AudioDirector.Instance.StopLoop("ambient"); AudioDirector.Instance.StopLoop("wind"); }
            Cursor.visible = false;
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime) { cg.alpha = 1f - t / 0.6f; yield return null; }
            cg.alpha = 0f; mainGroup.SetActive(false);
            if (darkening != null) darkening.gameObject.SetActive(false);

            videoEnded = false;
            if (reversing) { reversing = false; seeking = false; if (videoAudio != null) videoAudio.mute = false; }
            vp.Play();
            float started = Time.unscaledTime;
            while (!videoEnded)
            {
                if (Time.unscaledTime - started > skipAfter && AnyInput()) break;
                if (!vp.isPlaying && Time.unscaledTime - started > 1f && vp.time >= vp.length - 0.2f) break;
                yield return null;
            }
            vp.Stop();
            GameManager.Ensure().NewGame();
        }

        static bool AnyInput()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var ms = UnityEngine.InputSystem.Mouse.current;
            return (kb != null && kb.anyKey.wasPressedThisFrame) || (ms != null && ms.leftButton.wasPressedThisFrame);
        }

        void OnContinue() { GameManager.Ensure().ContinueGame(); }

        void OnQuit()
        {
            if (vp != null) vp.Stop();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnOptions()
        {
            mainGroup.SetActive(false);
            optionsPanel.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(optionsPanel.transform.Find("Back").gameObject);
        }

        void CloseOptions()
        {
            optionsPanel.SetActive(false);
            mainGroup.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(startBtn.gameObject);
        }
    }
}
