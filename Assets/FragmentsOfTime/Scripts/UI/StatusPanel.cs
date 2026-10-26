using UnityEngine;
using UnityEngine.UI;

namespace RiftOfTime
{
    /// <summary>
    /// Canto inferior esquerdo do HUD: vida (barra vermelho sangue que sobe devagar e brilha quando ele usa uma planta curadora)
    /// e as plantas curadoras guardadas.
    /// </summary>
    public class StatusPanel : MonoBehaviour
    {
        static readonly Color Cream = new Color(1f, 0.88f, 0.65f);
        static readonly Color Blood = new Color(0.47f, 0f, 0.015f);          // vermelho sangue, escuro e puro (sem rosado)
        static readonly Color DarkBlood = new Color(0.27f, 0f, 0.01f);       // com a vida quase no fim
        static readonly Color Glow = new Color(1f, 0.42f, 0.38f);            // brilho do trecho recuperado
        static readonly Color HeartRed = new Color(0.78f, 0.08f, 0.12f);
        static readonly Color LeafGreen = new Color(0.55f, 0.78f, 0.30f);

        // vida
        RectTransform fill, ghost;
        Image fillImg, ghostImg, heartImg;
        RectTransform heartRt;
        float shown = 1f, target = 1f, healPulse, ghostAlpha;

        // plantas
        Image leafImg;
        RectTransform leafRt;
        Text plantText;
        int plants = -1;
        float plantPulse;

        public static StatusPanel Create(Transform parent)
        {
            RectTransform root = UIFactory.CreateRect("Status", parent);
            UIFactory.Place(root, Vector2.zero, Vector2.zero, new Vector2(30f, 30f), new Vector2(270f, 128f));
            var panel = root.gameObject.AddComponent<StatusPanel>();
            panel.Build(root);
            return panel;
        }

        static Image Icon(Transform parent, string name, Sprite sprite, Color color, Vector2 center, Vector2 size)
        {
            Image img = UIFactory.CreateImage(parent, color, name);
            img.sprite = sprite;
            img.raycastTarget = false;
            UIFactory.Place(img.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), center, size);
            return img;
        }

        void Build(RectTransform root)
        {
            Image bg = UIFactory.CreateImage(root, new Color(0f, 0f, 0f, 0.38f), "Fundo", true);
            bg.raycastTarget = false;
            UIFactory.Stretch(bg.rectTransform);

            // vida: coração + barra
            heartImg = Icon(root, "Coracao", HudIcons.Heart, HeartRed, new Vector2(36f, 92f), new Vector2(40f, 40f));
            heartRt = heartImg.rectTransform;
            Image barBg = UIFactory.CreateImage(root, new Color(0f, 0f, 0f, 0.6f), "BarraVida", true);
            barBg.raycastTarget = false;
            UIFactory.Place(barBg.rectTransform, Vector2.zero, new Vector2(0f, 0.5f), new Vector2(66f, 92f), new Vector2(186f, 24f));
            RectTransform inner = UIFactory.CreateRect("Interior", barBg.transform);
            UIFactory.SetAnchors(inner, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            fillImg = UIFactory.CreateImage(inner, Blood, "Vida", true);
            fillImg.raycastTarget = false;
            fill = fillImg.rectTransform;
            UIFactory.SetAnchors(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ghostImg = UIFactory.CreateImage(inner, new Color(Glow.r, Glow.g, Glow.b, 0f), "Cura", true);
            ghostImg.raycastTarget = false;
            ghost = ghostImg.rectTransform;
            UIFactory.SetAnchors(ghost, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);

            // plantas curadoras: folha + quantidade
            leafImg = Icon(root, "Folha", HudIcons.Leaf, LeafGreen, new Vector2(36f, 36f), new Vector2(40f, 40f));
            leafRt = leafImg.rectTransform;
            plantText = UIFactory.CreateText(root, "×0", 34, Cream, TextAnchor.MiddleLeft, FontStyle.Bold, "Plantas");
            UIFactory.Place(plantText.rectTransform, Vector2.zero, new Vector2(0f, 0.5f), new Vector2(62f, 36f), new Vector2(90f, 44f));
            plantText.gameObject.AddComponent<Outline>().effectColor = Color.black;
            ApplyPlants();
        }

        // ---- vida

        public void SetHealth(float t01)
        {
            t01 = Mathf.Clamp01(t01);
            if (t01 - target > 0.03f)       // uma planta (+25%); a regeneração lenta não passa disso por chamada
            {
                ghost.anchorMin = new Vector2(target, 0f);
                ghost.anchorMax = new Vector2(t01, 1f);
                ghost.offsetMin = ghost.offsetMax = Vector2.zero;
                ghostAlpha = 1f;
                healPulse = 1f;
            }
            target = t01;
        }

        // ---- plantas curadoras

        public void SetPlants(int n)
        {
            n = Mathf.Max(0, n);
            if (plants >= 0 && n != plants) plantPulse = 1f;       // só pulsa quando muda de verdade (não na primeira leitura)
            plants = n;
            ApplyPlants();
        }

        void ApplyPlants()
        {
            int n = Mathf.Max(0, plants);
            plantText.text = "×" + n;
            float a = n > 0 ? 1f : 0.3f;       // apagada enquanto não tem nenhuma
            leafImg.color = new Color(LeafGreen.r, LeafGreen.g, LeafGreen.b, a);
            plantText.color = new Color(Cream.r, Cream.g, Cream.b, a);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // vida: sobe devagar (para ver a cura), desce rápido
            shown = Mathf.MoveTowards(shown, target, (shown < target ? 0.4f : 3f) * dt);
            healPulse = Mathf.MoveTowards(healPulse, 0f, dt * 0.9f);
            ghostAlpha = Mathf.MoveTowards(ghostAlpha, 0f, dt * 0.7f);
            fill.anchorMax = new Vector2(Mathf.Clamp01(shown), 1f);
            float low = Mathf.InverseLerp(0.45f, 0.15f, shown);
            fillImg.color = Color.Lerp(Color.Lerp(Blood, DarkBlood, low), Glow, healPulse * 0.35f);
            Color g = Glow; g.a = ghostAlpha * 0.9f;
            ghostImg.color = g;
            float beat = shown < 0.4f ? Mathf.Pow(0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(5f, 9f, low)), 3f) : 0f;     // o coração acelera com a vida baixa
            heartRt.localScale = Vector3.one * (1f + 0.4f * healPulse + 0.12f * beat);
            heartImg.color = Color.Lerp(HeartRed, Glow, healPulse);

            // plantas
            plantPulse = Mathf.MoveTowards(plantPulse, 0f, dt * 2f);
            leafRt.localScale = Vector3.one * (1f + 0.35f * plantPulse);
        }
    }
}
