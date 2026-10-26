using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Materiais URP criados em tempo de execução.</summary>
    public static class RotMaterials
    {
        static Material litBase, unlitBase;

        static Material Base(ref Material cache, string resName, string shaderName)
        {
            if (cache != null) return cache;
            cache = Resources.Load<Material>("RoT/" + resName);
            if (cache == null)
            {
                Shader sh = Shader.Find(shaderName);
                if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                cache = new Material(sh);
            }
            return cache;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static Color GetColor(Material m)
        {
            if (m == null) return Color.white;
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty("_Color")) return m.GetColor("_Color");
            return Color.white;
        }

        /// <summary>Material com iluminação (nova instância).</summary>
        public static Material Lit(Color c)
        {
            var m = new Material(Base(ref litBase, "RoT_Lit", "Universal Render Pipeline/Lit"));
            SetColor(m, c);
            return m;
        }

        /// <summary>Material sem iluminação, usado para brilho (fragmentos, anéis, hologramas).</summary>
        public static Material Unlit(Color c)
        {
            var m = new Material(Base(ref unlitBase, "RoT_Unlit", "Universal Render Pipeline/Unlit"));
            SetColor(m, c);
            return m;
        }
    }
}
