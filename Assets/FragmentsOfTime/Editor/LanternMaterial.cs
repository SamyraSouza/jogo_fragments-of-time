using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RiftOfTime.EditorTools
{
    /// <summary>Cria Assets/Lantern/Lantern_URP.mat (URP/Lit com as texturas da pasta) e a aplica no wawa.fbx.</summary>
    public static class LanternMaterial
    {
        const string Model = "Assets/Lantern/source/wawa.fbx";
        const string Source = "Assets/Lantern/source/";
        const string MatPath = "Assets/Lantern/Lantern_URP.mat";

        [InitializeOnLoadMethod]
        static void Auto()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<Material>(MatPath) == null) Apply();
            };
        }

        public static void Apply()
        {
            var importer = AssetImporter.GetAtPath(Model) as ModelImporter;
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            if (importer == null || urp == null) return;

            Texture2D Tex(string kind) => AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "Lanterna_standardSurface1_" + kind + ".1001.png");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null) { mat = new Material(urp) { name = "Lantern_URP" }; AssetDatabase.CreateAsset(mat, MatPath); }
            mat.shader = urp;

            var baseTex = Tex("BaseColor");
            if (baseTex != null) { mat.SetTexture("_BaseMap", baseTex); mat.SetColor("_BaseColor", Color.white); }
            var normal = Tex("Normal");
            if (normal != null)
            {
                var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;
                if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            var emissive = Tex("Emissive");
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            mat.SetTexture("_EmissionMap", emissive != null ? emissive : baseTex);
            mat.SetColor("_EmissionColor", emissive != null ? Color.white : new Color(0.3f, 0.3f, 0.3f));
            mat.SetFloat("_Metallic", 0.4f);
            mat.SetFloat("_Smoothness", 0.5f);
            EditorUtility.SetDirty(mat);

            foreach (var src in AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Material>())
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), mat);
            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
            Debug.Log("[Lantern] material URP aplicado ao wawa.fbx");
        }
    }
}
