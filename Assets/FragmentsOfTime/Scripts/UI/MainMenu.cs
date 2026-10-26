using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

namespace RiftOfTime
{
    /// <summary>Tela 00 – Tela inicial.</summary>
    public class MainMenu : MonoBehaviour
    {
        public AudioClip windClip;

        [Header("Vídeo de fundo (Assets/StreamingAssets)")]
        const string videoFile = "video-menu-pingpong.mp4";

        VideoPlayer vp;
        RawImage videoImage;
        RenderTexture videoRT;
        bool videoReady, starting;
        float videoW, videoH;
        Material glitchMat;
        float prepareStart;

        Button startBtn;

        void Start()
        {
            GameManager gm = GameManager.Ensure();
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            MenuCursor.Set();
            UIFactory.EnsureEventSystem();

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.cullingMask = 0;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
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

                HorrorTitleStyle.CreateDarkening(root, 1);
            }

            GameObject mainGroup = UIFactory.CreateRect("Main", root).gameObject;
            UIFactory.Stretch(mainGroup.GetComponent<RectTransform>());
            Transform m = mainGroup.transform;

            HorrorTitleStyle.CreateTitle(m, "FRAGMENTS", "OF TIME");
            startBtn = HorrorTitleStyle.CreateEntry(m, "Iniciar Expedição", 32, OnStart, "Iniciar", 0.375f);
            HorrorTitleStyle.CreateEntry(m, "Sair", 32, OnQuit, "Sair", 0.315f);
            HorrorTitleStyle.CreateCopyright(m, "©FRAGMENTS OF TIME. TODOS OS DIREITOS RESERVADOS.");

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(startBtn.gameObject);
        }

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

        void OnQuit()
        {
            if (vp != null) vp.Stop();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
