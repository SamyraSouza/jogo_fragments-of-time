using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RiftOfTime
{
    /// <summary>Nomes das cenas (uma por tela do storyboard).</summary>
    public static class SceneNames
    {
        public const string Menu = "00_Menu";
        public const string Prologo = "01_Prologo";
        public const string Fase1 = "02_Fase1_TemploPerdido";
        public const string Fase2 = "03_Fase2_RuinasDoDeserto";
        public const string Fase3 = "04_Fase3_CamaraDoTempo";
        public const string Final = "05_Final";

        public static readonly string[] Ordem = { Menu, Prologo, Fase1, Fase2, Fase3, Final };
    }

    /// <summary>Estado global do jogo (persiste entre cenas): fragmentos, checkpoint, transições e volume.</summary>
    public class GameManager : MonoBehaviour
    {
        public const int TotalFragments = 3;
        public const string KeyCheckpoint = "ROT_Checkpoint";
        public const string KeyFragments = "ROT_Fragments";
        public const string KeyCompleted = "ROT_Completed";
        public const string KeyVolume = "ROT_Volume";

        public static GameManager Instance { get; private set; }

        /// <summary>Ligado ao sair da Fase 3: o Prólogo abre como epílogo (máquina sumida, walkie no chão).</summary>
        public static bool Epilogue;

        public int Fragments { get; private set; }
        public event Action<int> FragmentsChanged;
        public ScreenFader Fader { get; private set; }

        bool loading;

        /// <summary>Verdadeiro enquanto uma troca de cena está em andamento (inclusive o fade de entrada da cena nova): GoTo ignora pedidos nesse período.</summary>
        public bool IsLoading { get { return loading; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Epilogue = false; }

        public static GameManager Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("GameManager");
            return go.AddComponent<GameManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Fader = ScreenFader.Create(transform);
            AudioDirector.Create(transform);
            ApplyVolume();
        }

        public void SetFragments(int n)
        {
            Fragments = Mathf.Clamp(n, 0, TotalFragments);
            FragmentsChanged?.Invoke(Fragments);
        }

        public void AddFragment() { SetFragments(Fragments + 1); }

        public static bool HasSave { get { return PlayerPrefs.HasKey(KeyCheckpoint); } }
        public static string SavedScene { get { return PlayerPrefs.GetString(KeyCheckpoint, SceneNames.Fase1); } }
        public static bool Completed { get { return PlayerPrefs.GetInt(KeyCompleted, 0) == 1; } }
        public static int SavedFragments { get { return PlayerPrefs.GetInt(KeyFragments, 0); } }

        public void SaveCheckpoint(string scene)
        {
            PlayerPrefs.SetString(KeyCheckpoint, scene);
            PlayerPrefs.SetInt(KeyFragments, Fragments);
            PlayerPrefs.Save();
        }

        public void MarkCompleted()
        {
            PlayerPrefs.SetInt(KeyCompleted, 1);
            PlayerPrefs.SetInt(KeyFragments, TotalFragments);
            PlayerPrefs.Save();
        }

        public static float MasterVolume
        {
            get { return PlayerPrefs.GetFloat(KeyVolume, 0.8f); }
            set { PlayerPrefs.SetFloat(KeyVolume, Mathf.Clamp01(value)); AudioListener.volume = Mathf.Clamp01(value); }
        }

        public static void ApplyVolume() { AudioListener.volume = MasterVolume; }

        public void NewGame()
        {
            PlayerPrefs.DeleteKey(KeyCheckpoint);
            SetFragments(0);
            GoTo(SceneNames.Prologo, Color.black, null, null, 0f, 0.25f);
        }

        public void ContinueGame() { GoTo(SavedScene, Color.black); }

        public void Reload() { GoTo(SceneManager.GetActiveScene().name, Color.black); }

        public void ToMenu() { GoTo(SceneNames.Menu, Color.black); }

        public void GoTo(string scene, Color fadeColor, string cardTitle = null, string cardSub = null, float cardSeconds = 1.2f, float fadeOut = 0.35f)
        {
            if (loading) { Debug.LogWarning("[Carregar] já está carregando outra cena; pedido para '" + scene + "' ignorado"); return; }
            StartCoroutine(LoadRoutine(scene, fadeColor, cardTitle, cardSub, cardSeconds, fadeOut));
        }

        IEnumerator LoadRoutine(string scene, Color fadeColor, string cardTitle, string cardSub, float cardSeconds, float fadeOut)
        {
            loading = true;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            yield return Fader.FadeTo(fadeColor, fadeOut);
            AudioDirector.Instance.StopAllLoops(0.1f);
            bool hasCard = !string.IsNullOrEmpty(cardTitle);
            if (hasCard) Fader.ShowCard(cardTitle, cardSub);

            float loadStart = Time.realtimeSinceStartup;
            AsyncOperation op = SceneManager.LoadSceneAsync(scene);
            while (op != null && !op.isDone) yield return null;
            Debug.Log("[Carregar] cena '" + scene + "' carregada em " + (Time.realtimeSinceStartup - loadStart).ToString("F1") + " s");
            Time.timeScale = 1f;
            ApplyVolume();

            if (hasCard)
            {
                yield return new WaitForSecondsRealtime(cardSeconds);
                Fader.HideCard();
            }
            yield return Fader.FadeFrom(0.45f);
            loading = false;
        }
    }
}
