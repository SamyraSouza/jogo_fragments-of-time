using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RiftOfTime.EditorTools
{
    /// <summary>Acha e conserta tudo que aparece ROSA na cena aberta.</summary>
    public static class FixPinkMaterials
    {
        const string Dir = "Assets/FragmentsOfTime/Materials_URP";

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp == null) { EditorUtility.DisplayDialog("Rosa", "Shader URP/Lit não encontrado.", "OK"); return; }
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FragmentsOfTime", "Materials_URP");

            var cache = new Dictionary<Material, Material>();
            Material fallback = null;
            int nulls = 0, converted = 0, objects = 0;
            var report = new System.Text.StringBuilder();

            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null)
                    {
                        if (fallback == null) fallback = Save(NewLit(urp, "Padrao_cinza", null, new Color(0.5f, 0.5f, 0.5f)));
                        mats[i] = fallback; changed = true; nulls++;
                        report.AppendLine("  [vazio] " + Path(r.transform) + " (material " + i + ")");
                    }
                    else if (IsBad(m))
                    {
                        if (!cache.TryGetValue(m, out Material c)) { c = Save(Convert(urp, m)); cache[m] = c; }
                        mats[i] = c; changed = true; converted++;
                        report.AppendLine("  [shader '" + (m.shader != null ? m.shader.name : "nenhum") + "'] " + Path(r.transform) + " material '" + m.name + "'");
                    }
                }
                if (changed)
                {
                    Undo.RecordObject(r, "Corrigir material rosa");
                    r.sharedMaterials = mats;
                    EditorUtility.SetDirty(r);
                    objects++;
                }
            }

            if (objects > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            string msg = objects + " objeto(s) corrigido(s): " + nulls + " material(is) vazio(s), " + converted + " de shader antigo.";
            Debug.Log("[Rosa] " + msg + "\n" + report);
            EditorUtility.DisplayDialog("Materiais rosas", msg + (objects > 0 ? "\nSalve a cena (Ctrl+S). A lista está no Console." : "\nNada rosa por material nesta cena. Se ainda vir rosa, pode ser partícula ou outra cena."), "OK");
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

        static Material NewLit(Shader urp, string name, Texture main, Color color)
        {
            var m = new Material(urp) { name = name };
            if (main != null) m.SetTexture("_BaseMap", main);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.25f);
            return m;
        }

        static Material Convert(Shader urp, Material src)
        {
            Texture main = Tex(src, "_MainTex") ?? Tex(src, "_BaseMap");
            Color col = src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
            var m = NewLit(urp, src.name + "_URP", main, col);
            if (main != null && src.HasProperty("_MainTex"))
            {
                m.SetTextureScale("_BaseMap", src.GetTextureScale("_MainTex"));
                m.SetTextureOffset("_BaseMap", src.GetTextureOffset("_MainTex"));
            }
            Texture bump = Tex(src, "_BumpMap");
            if (bump != null) { m.SetTexture("_BumpMap", bump); m.EnableKeyword("_NORMALMAP"); }
            Texture mg = Tex(src, "_MetallicGlossMap");
            if (mg != null) { m.SetTexture("_MetallicGlossMap", mg); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            if (src.HasProperty("_Metallic")) m.SetFloat("_Metallic", src.GetFloat("_Metallic"));
            if (src.HasProperty("_Glossiness")) m.SetFloat("_Smoothness", src.GetFloat("_Glossiness"));
            Texture occ = Tex(src, "_OcclusionMap");
            if (occ != null) m.SetTexture("_OcclusionMap", occ);
            Texture em = Tex(src, "_EmissionMap");
            if (src.HasProperty("_EmissionColor") && (em != null || src.GetColor("_EmissionColor").maxColorComponent > 0.01f))
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetColor("_EmissionColor", src.GetColor("_EmissionColor"));
                if (em != null) m.SetTexture("_EmissionMap", em);
            }
            if (src.HasProperty("_Mode"))
            {
                float mode = src.GetFloat("_Mode");
                if (mode == 1f) { m.SetFloat("_AlphaClip", 1f); m.EnableKeyword("_ALPHATEST_ON"); if (src.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", src.GetFloat("_Cutoff")); }
                else if (mode >= 2f)
                {
                    m.SetFloat("_Surface", 1f);
                    m.SetFloat("_Blend", 0f);
                    m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_ZWrite", 0f);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
            }
            return m;
        }

        static Texture Tex(Material m, string prop) { return m.HasProperty(prop) ? m.GetTexture(prop) : null; }

        static Material Save(Material m)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(Dir + "/" + SafeName(m.name) + ".mat");
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static string SafeName(string n)
        {
            foreach (char c in Path_InvalidChars()) n = n.Replace(c, '_');
            return n;
        }

        static char[] Path_InvalidChars() { return System.IO.Path.GetInvalidFileNameChars(); }

        static string Path(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
    }
}
