using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RiftOfTime
{
    /// <summary>Créditos finais (tela 05 → créditos), no mesmo estilo do menu do jogo: fundo escuro com vinheta, títulos em serifa com falha de TV.</summary>
    public class CreditsScreen : MonoBehaviour
    {
        enum Kind { Title, Subtitle, Heading, Name, Body, Gap, Thanks }

        RectTransform scroll;
        float speed = 60f;
        float startY, endY;
        float y;
        bool done;

        public static CreditsScreen Show()
        {
            return new GameObject("Credits").AddComponent<CreditsScreen>();
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            MenuCursor.Set();
            UIFactory.EnsureEventSystem();
            GameManager.Ensure().MarkCompleted();
            TvGlitchClock.Reset();

            Canvas canvas = UIFactory.CreateCanvas("CreditsCanvas", 500);
            canvas.transform.SetParent(transform, false);
            Transform root = canvas.transform;

            Image bg = UIFactory.CreateImage(root, new Color(0.015f, 0.015f, 0.02f, 1f), "Bg");
            UIFactory.Stretch(bg.rectTransform);
            HorrorTitleStyle.CreateDarkening(root, 1);

            BuildScroll(root);

            Button menu = HorrorTitleStyle.CreateEntry(root, "Voltar ao menu", 30, () => { MenuCursor.Reset(); GameManager.Ensure().ToMenu(); }, "Menu", 0.17f);
            RecenterEntry(menu);
            HorrorTitleStyle.CreateCopyright(root, "©FRAGMENTS OF TIME. TODOS OS DIREITOS RESERVADOS.");
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(menu.gameObject);

            if (!MenuSounds.PlayMusic()) AudioDirector.Instance.Loop("credits", "calm", 0.5f);
        }

        static void RecenterEntry(Button b)
        {
            var rt = b.GetComponent<RectTransform>();
            Vector2 a = rt.anchorMin;
            a.x = 0.5f;
            rt.anchorMin = rt.anchorMax = a;
        }

        void BuildScroll(Transform root)
        {
            RectTransform view = UIFactory.CreateRect("Janela", root);
            view.anchorMin = new Vector2(0.1f, 0.22f);
            view.anchorMax = new Vector2(0.9f, 0.78f);
            view.offsetMin = view.offsetMax = Vector2.zero;
            view.gameObject.AddComponent<RectMask2D>();

            var lines = new List<KeyValuePair<Kind, string>>
            {
                Line(Kind.Title, "VIAGEM TEMPORAL"),
                Line(Kind.Title, "CONCLUÍDA"),
                Line(Kind.Gap, ""),
                Line(Kind.Subtitle, "FRAGMENTS OF TIME"),
                Line(Kind.Gap, ""), Line(Kind.Gap, ""),
                Line(Kind.Heading, "EQUIPE DE DESENVOLVIMENTO"),
                Line(Kind.Name, "Emanuele Ribeiro Moreira dos Santos"),
                Line(Kind.Name, "Larissa Lucas Fonseca"),
                Line(Kind.Name, "Maria Luísa de Oliveira"),
                Line(Kind.Name, "Samyra Natasha Grilanda de Souza"),
                Line(Kind.Gap, ""), Line(Kind.Gap, ""),
                Line(Kind.Body, "De volta ao laboratório, o explorador observa a máquina reconstruída"),
                Line(Kind.Body, "e o papiro que trouxe consigo."),
                Line(Kind.Gap, ""),
                Line(Kind.Body, "Mas quem construiu, de fato, a tecnologia do tempo?"),
                Line(Kind.Gap, ""), Line(Kind.Gap, ""),
                Line(Kind.Thanks, "OBRIGADO POR JOGAR"),
            };

            RectTransform content = UIFactory.CreateRect("Conteudo", view);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            float cursor = 0f;
            foreach (var l in lines)
            {
                float h = AddLine(content, l.Key, l.Value, cursor);
                cursor += h;
            }
            content.sizeDelta = new Vector2(0f, cursor);

            scroll = content;
            float viewH = 1080f * (0.78f - 0.22f);
            startY = -viewH;
            const float thanksH = 110f;
            endY = cursor - thanksH * 0.5f - viewH * 0.5f;
            y = startY;
            content.anchoredPosition = new Vector2(0f, y);
        }

        static KeyValuePair<Kind, string> Line(Kind k, string s) { return new KeyValuePair<Kind, string>(k, s); }

        float AddLine(RectTransform parent, Kind kind, string text, float top)
        {
            float h; int size; Color color; bool serif; float glitch = 0f;
            switch (kind)
            {
                case Kind.Title:    h = 100f; size = 72; color = HorrorTitleStyle.Bone; serif = true; glitch = 1f; break;
                case Kind.Subtitle: h = 90f;  size = 50;  color = new Color(0.74f, 0.72f, 0.68f); serif = true; glitch = 0.9f; break;
                case Kind.Heading:  h = 90f;  size = 34;  color = HorrorTitleStyle.Orange; serif = true; break;
                case Kind.Name:     h = 64f;  size = 38;  color = new Color(0.93f, 0.91f, 0.86f); serif = false; break;
                case Kind.Body:     h = 56f;  size = 30;  color = new Color(0.7f, 0.68f, 0.64f); serif = false; break;
                case Kind.Thanks:   h = 110f; size = 56;  color = HorrorTitleStyle.Bone; serif = true; glitch = 0.9f; break;
                default:            return 50f;
            }
            Text t = UIFactory.CreateText(parent, text, size, color, TextAnchor.MiddleCenter, FontStyle.Normal, "Linha");
            t.font = serif ? HorrorTitleStyle.TitleFont : HorrorTitleStyle.SansFont;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            RectTransform rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, h);
            rt.anchoredPosition = new Vector2(0f, -top);
            if (serif)
            {
                var sh = t.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.8f);
                sh.effectDistance = new Vector2(3f, -3f);
                if (glitch > 0f) t.gameObject.AddComponent<GlitchText>().Configure(12f, 30f, glitch);
            }
            return h;
        }

        void Update()
        {
            TvGlitchClock.Tick();
            if (scroll == null || done) return;
            y = Mathf.Min(y + speed * Time.unscaledDeltaTime, endY);
            scroll.anchoredPosition = new Vector2(0f, y);
            if (y >= endY) done = true;
        }
    }
}
