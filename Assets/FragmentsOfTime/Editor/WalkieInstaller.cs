using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RiftOfTime.EditorTools
{
    /// <summary>Coloca o walkie-talkie (Assets/Movels/walkie-talkie) como filho da câmera do Player.prefab e cria o material URP dele.</summary>
    public static class WalkieInstaller
    {
        const string Model = "Assets/Movels/walkie-talkie/source/krotkofalowka/krotkofal.fbx";
        const string TexDir = "Assets/Movels/walkie-talkie/source/krotkofalowka/textures/";
        const string MatPath = "Assets/Movels/walkie-talkie/Walkie_URP.mat";
        const string PlayerPrefab = "Assets/Ancient Modular Tombs/Prefabs/Player.prefab";

        [InitializeOnLoadMethod]
        static void Auto()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Model) == null) return;
                if (AssetDatabase.LoadAssetAtPath<Material>(MatPath) == null) Install();
            };
        }

        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            MakeMaterial();
            AddToPlayer();
        }

        static void MakeMaterial()
        {
            var importer = AssetImporter.GetAtPath(Model) as ModelImporter;
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            if (importer == null || urp == null) return;

            Texture2D Tex(string kind) => AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "krotkofal_low3_KROTKOFAL_" + kind + ".png");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null) { mat = new Material(urp) { name = "Walkie_URP" }; AssetDatabase.CreateAsset(mat, MatPath); }
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
            mat.SetFloat("_Metallic", 0.3f);
            mat.SetFloat("_Smoothness", 0.4f);
            EditorUtility.SetDirty(mat);

            foreach (var src in AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Material>())
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), mat);
            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
        }

        static bool IsWalkie(string n)
        {
            n = n.ToLower();
            return n.Contains("krotkofal") || n.Contains("walkie") || n.Contains("talkie");
        }

        static void AddToPlayer()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if (model == null) return;
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var cam = root.GetComponentInChildren<Camera>(true);
                if (cam == null) return;
                if (cam.GetComponentsInChildren<Transform>(true).Any(t => IsWalkie(t.name))) return;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(model, cam.transform);
                go.name = "Walkie";
                go.transform.localPosition = new Vector3(-0.15f, -0.25f, 0.5f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.1f;
                foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
                Debug.Log("[Walkie] walkie-talkie colocado no Player.prefab (filho da câmera)");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
