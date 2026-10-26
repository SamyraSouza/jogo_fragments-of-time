using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RiftOfTime
{
    /// <summary>Fade/flash de tela e cartelas de título ("Fase 3").</summary>
    public class ScreenFader : MonoBehaviour
    {
        Image overlay;
        Text cardTitle, cardSub;

        public static ScreenFader Create(Transform parent)
        {
            var go = new GameObject("ScreenFader");
            go.transform.SetParent(parent, false);
            return go.AddComponent<ScreenFader>();
        }

        void Awake()
        {
            Canvas canvas = UIFactory.CreateCanvas("FaderCanvas", 1000);
            canvas.transform.SetParent(transform, false);

            overlay = UIFactory.CreateImage(canvas.transform, new Color(0, 0, 0, 0), "Overlay");
            UIFactory.Stretch(overlay.rectTransform);
            overlay.raycastTarget = false;

            cardTitle = UIFactory.CreateText(canvas.transform, "", 78, new Color(1f, 0.85f, 0.6f), TextAnchor.MiddleCenter, FontStyle.Bold, "CardTitle");
            UIFactory.SetAnchors(cardTitle.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 10), new Vector2(0, 130));
            cardSub = UIFactory.CreateText(canvas.transform, "", 36, new Color(0.9f, 0.85f, 0.75f), TextAnchor.MiddleCenter, FontStyle.Italic, "CardSub");
            UIFactory.SetAnchors(cardSub.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -80), new Vector2(0, 0));
            cardTitle.gameObject.SetActive(false);
            cardSub.gameObject.SetActive(false);
        }

        /// <summary>A tela ainda está (quase) coberta: preta, branca ou com o cartão de fase?</summary>
        public bool Covered
        {
            get { return overlay != null && (overlay.color.a > 0.45f || cardTitle.gameObject.activeSelf); }
        }

        void SetAlpha(float a)
        {
            Color c = overlay.color;
            c.a = a;
            overlay.color = c;
        }

        public IEnumerator FadeTo(Color color, float seconds)
        {
            float a0 = overlay.color.a;
            overlay.color = new Color(color.r, color.g, color.b, a0);
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(a0, 1f, t / seconds));
                yield return null;
            }
            SetAlpha(1f);
        }

        public IEnumerator FadeFrom(float seconds)
        {
            float a0 = overlay.color.a;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(a0, 0f, t / seconds));
                yield return null;
            }
            SetAlpha(0f);
        }

        public IEnumerator Flash(Color color, float up, float down)
        {
            yield return FadeTo(color, up);
            yield return FadeFrom(down);
        }

        public void ShowCard(string title, string sub)
        {
            cardTitle.text = title;
            cardSub.text = sub ?? "";
            cardTitle.gameObject.SetActive(true);
            cardSub.gameObject.SetActive(true);
        }

        public void HideCard()
        {
            cardTitle.gameObject.SetActive(false);
            cardSub.gameObject.SetActive(false);
        }
    }
}
