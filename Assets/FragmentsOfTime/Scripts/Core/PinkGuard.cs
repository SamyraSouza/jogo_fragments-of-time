using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Rede de segurança contra "rosa": percorre a cena e troca materiais vazios (null) ou com shader que o URP não desenha</summary>
    public static class PinkGuard
    {
        public static int Fix(string when)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader particle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (lit == null) return 0;

            var cache = new Dictionary<Material, Material>();
            Material fallback = null;
            int fixedMats = 0, listed = 0;
            var sb = new StringBuilder();

            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    Material repl = null;
                    string why = null;
                    if (m == null)
                    {
                        if (fallback == null) fallback = Make(lit, null, new Color(0.5f, 0.5f, 0.5f), "Padrao_cinza");
                        repl = fallback; why = "vazio";
                    }
                    else if (IsBad(m))
                    {
                        if (!cache.TryGetValue(m, out repl))
                        {
                            Shader sh = (r is ParticleSystemRenderer && particle != null) ? particle : lit;
                            repl = Make(sh, m, Color.white, m.name + "_corrigido");
                            cache[m] = repl;
                        }
                        why = "shader " + (m.shader != null ? m.shader.name : "nenhum");
                    }
                    if (repl != null)
                    {
                        mats[i] = repl; changed = true; fixedMats++;
                        if (listed++ < 25) sb.AppendLine("  " + Path(r.transform) + " [" + why + "]");
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }
            if (fixedMats > 0) Debug.Log("[Rosa] (" + when + ") " + fixedMats + " material(is) trocado(s):\n" + sb);
            return fixedMats;
        }

        static bool IsBad(Material m)
        {
            Shader s = m.shader;
            if (s == null) return true;
            string n = s.name;
            if (n.StartsWith("Hidden/InternalErrorShader")) return true;
            if (n == "Standard" || n == "Standard (Specular setup)" || n.StartsWith("Legacy Shaders/") || n.StartsWith("Mobile/") || n.StartsWith("Nature/")) return true;
            return !s.isSupported;
        }

        static Material Make(Shader shader, Material src, Color fallbackColor, string name)
        {
            var m = new Material(shader) { name = name };
            Texture tex = null;
            Color col = fallbackColor;
            if (src != null)
            {
                if (src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
                if (tex == null && src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");
                if (src.HasProperty("_Color")) col = src.GetColor("_Color");
                else if (src.HasProperty("_BaseColor")) col = src.GetColor("_BaseColor");
            }
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
            return m;
        }

        static string Path(Transform t)
        {
            string p = t.name;
            for (int i = 0; i < 3 && t.parent != null; i++) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
    }
}
