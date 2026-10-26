using System;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Ícones do HUD (coração e folha) desenhados em código, como o anel do cronômetro: o jogo não depende de arquivos de imagem para eles.</summary>
    public static class HudIcons
    {
        static Sprite heart, leaf;

        public static Sprite Heart
        {
            get
            {
                if (heart == null)
                    heart = Make(64, 64, (x, y) => HeartSdf((x - 32f) / 32f * 0.66f, y / 64f * 1.26f - 0.08f) / 0.02f);
                return heart;
            }
        }

        /// <summary>Folha apontando para cima e para a direita, com a nervura central vazada.</summary>
        public static Sprite Leaf
        {
            get
            {
                if (leaf == null)
                    leaf = Make(64, 64, (x, y) =>
                    {
                        float u = (x - 32f) / 32f, v = (y - 32f) / 32f;
                        float along = (u + v) * 0.70710678f, across = (u - v) * 0.70710678f;
                        float d = VesicaSdf(across, along, 1.125f, 0.675f) * 32f;
                        if (along > -0.85f && along < 0.55f) d = Mathf.Max(d, (0.045f - Mathf.Abs(across)) * 32f);
                        return d;
                    });
                return leaf;
            }
        }

        /// <summary>Textura branca com alfa a partir de uma distância assinada (em pixels, negativa dentro): bordas suaves de 1 px.</summary>
        static Sprite Make(int w, int h, Func<float, float, float> sdf)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - sdf(x + 0.5f, y + 0.5f)));
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static float Sq(float v) { return v * v; }

        // coração exato (Inigo Quilez): ponta em (0,0), reentrância do topo em (0,1)
        static float HeartSdf(float x, float y)
        {
            x = Mathf.Abs(x);
            if (y + x > 1f) return Mathf.Sqrt(Sq(x - 0.25f) + Sq(y - 0.75f)) - 0.35355339f;
            float m = 0.5f * Mathf.Max(x + y, 0f);
            float a = Sq(x) + Sq(y - 1f), b = Sq(x - m) + Sq(y - m);
            return Mathf.Sqrt(Mathf.Min(a, b)) * Mathf.Sign(x - y);
        }

        // vesica (interseção de dois círculos): a forma da folha. Pontas em (0, ±sqrt(r²-d²)).
        static float VesicaSdf(float x, float y, float r, float d)
        {
            x = Mathf.Abs(x); y = Mathf.Abs(y);
            float b = Mathf.Sqrt(r * r - d * d);
            return ((y - b) * d > x * b) ? Mathf.Sqrt(Sq(x) + Sq(y - b)) : Mathf.Sqrt(Sq(x + d) + Sq(y)) - r;
        }
    }
}
