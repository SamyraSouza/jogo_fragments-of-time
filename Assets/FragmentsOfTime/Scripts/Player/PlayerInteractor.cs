using UnityEngine;
using UnityEngine.InputSystem;

namespace RiftOfTime
{
    /// <summary>Procura o Interactable mais próximo, mostra o prompt e reage ao [E].</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        void Update()
        {
            if (Time.timeScale <= 0f) return;
            HUD hud = HUD.Instance;

            if (hud != null && (hud.IsReading || Time.unscaledTime < hud.InteractBlockedUntil))
            {
                hud.SetPrompt(null);
                return;
            }

            Interactable best = null;
            float bestSq = float.MaxValue;
            Vector3 p = transform.position;
            for (int i = 0; i < Interactable.All.Count; i++)
            {
                Interactable it = Interactable.All[i];
                if (it == null || !it.IsAvailable) continue;
                Vector3 d = it.transform.position - p;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                float r = it.Radius;
                if (sq <= r * r && sq < bestSq) { bestSq = sq; best = it; }
            }

            if (hud != null) hud.SetPrompt(best != null ? best.Prompt : null);

            if (best != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                best.Interact(this);
        }

        void OnDisable()
        {
            if (HUD.Instance != null) HUD.Instance.SetPrompt(null);
        }
    }
}
