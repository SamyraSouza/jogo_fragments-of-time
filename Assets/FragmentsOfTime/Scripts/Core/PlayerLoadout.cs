using UnityEngine.SceneManagement;

namespace RiftOfTime
{
    /// <summary>O que o jogador carrega em cada fase.</summary>
    public static class PlayerLoadout
    {
        public static bool DaggerPhase
        {
            get { string n = SceneManager.GetActiveScene().name; return n.Contains("Fase2") || n.Contains("Fase3"); }
        }

        /// <summary>Fases 2 e 3: a lanterna fica na mão esquerda, sem walkie-talkie, e a mão direita fica livre para golpear (ou segura a adaga).</summary>
        public static bool LanternLeft
        {
            get { string n = SceneManager.GetActiveScene().name; return n.Contains("Fase1") || n.Contains("Fase2") || n.Contains("Fase3"); }
        }
    }
}
