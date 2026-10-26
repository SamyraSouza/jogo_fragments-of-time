using UnityEngine;
using UnityEngine.SceneManagement;

namespace RiftOfTime
{
    /// <summary>
    /// As tochas do pacote (Torchfire.prefab) trazem uma luz pontual com sombra suave: cada uma desenha 6 mapas de sombra por quadro.
    /// Com 6 tochas na Fase 1 o URP avisava "31 shadow maps" no atlas de 2048 px, o que derruba o desempenho em GPU integrada.
    /// Ao carregar cada cena, as luzes pontuais ficam sem sombra; a sombra da lanterna do jogador (spot, 1 mapa) continua.
    /// </summary>
    public static class LightShadowBudget
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Apply(); }

        public static int Apply()
        {
            int changed = 0;
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.type != LightType.Point || l.shadows == LightShadows.None) continue;
                l.shadows = LightShadows.None;
                changed++;
            }
            if (changed > 0) Debug.Log("[Sombras] " + changed + " luz(es) pontual(is) sem sombra (desempenho)");
            return changed;
        }
    }
}
