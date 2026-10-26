using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RiftOfTime.EditorTools
{
    /// <summary>Põe colisão em tudo que é visível na cena e não tem collider (tochas, pedras, paredes pintadas, objetos soltos...).</summary>
    public static class AddMissingColliders
    {
        const float MinSize = 0.03f;
        const int MaxMeshVerts = 15000;

        static readonly string[] SkipNames = { "particle", "sky", "horizon", "flame", "smoke", "dust", "sand", "decal", "papel", "folha" };
        static readonly string[] SkipExact = { "fire" };

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.GetActiveScene();
            var prefabsToFix = new HashSet<string>();
            var sceneObjects = new List<MeshRenderer>();

            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!Eligible(mr)) continue;
                var root = PrefabUtility.GetNearestPrefabInstanceRoot(mr.gameObject);
                string path = root != null ? AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(root)) : null;
                if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/")) prefabsToFix.Add(path);
                else sceneObjects.Add(mr);
            }

            int fixedPrefabs = 0, addedInPrefabs = 0, addedInScene = 0;
            foreach (string path in prefabsToFix)
            {
                var contents = PrefabUtility.LoadPrefabContents(path);
                int added = 0;
                foreach (var mr in contents.GetComponentsInChildren<MeshRenderer>(true))
                    if (Eligible(mr) && AddCollider(mr, false)) added++;
                if (added > 0) { PrefabUtility.SaveAsPrefabAsset(contents, path); fixedPrefabs++; addedInPrefabs += added; }
                PrefabUtility.UnloadPrefabContents(contents);
            }
            foreach (var mr in sceneObjects)
                if (mr != null && AddCollider(mr, true)) addedInScene++;

            if (addedInScene > 0) EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            string msg = "Colisão adicionada: " + addedInPrefabs + " em " + fixedPrefabs + " prefab(s), " + addedInScene + " objeto(s) soltos na cena.";
            Debug.Log("[Colisão] " + msg);
            EditorUtility.DisplayDialog("Colisão", msg + (addedInScene > 0 ? "\nSalve a cena (Ctrl+S)." : ""), "OK");
        }

        static bool Eligible(MeshRenderer mr)
        {
            if (mr == null || !mr.enabled) return false;
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return false;
            if (mr.GetComponentInParent<Camera>(true) != null) return false;
            if (mr.GetComponentInParent<Collider>(true) != null) return false;
            if (mr.GetComponentInChildren<Collider>(true) != null) return false;
            if (mr.GetComponentInParent<ParticleSystem>(true) != null) return false;
            if (mr.GetComponentInParent<Canvas>(true) != null) return false;
            string n = mr.name.ToLower();
            foreach (string s in SkipNames) if (n.Contains(s)) return false;
            if (SkipExact.Contains(n)) return false;
            Vector3 size = mf.sharedMesh.bounds.size;
            Vector3 ls = mr.transform.lossyScale;
            float longest = Mathf.Max(size.x * Mathf.Abs(ls.x), size.y * Mathf.Abs(ls.y), size.z * Mathf.Abs(ls.z));
            return longest >= MinSize;
        }

        static bool AddCollider(MeshRenderer mr, bool undo)
        {
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return false;
            if (mf.sharedMesh.vertexCount > MaxMeshVerts)
            {
                var box = undo ? Undo.AddComponent<BoxCollider>(mr.gameObject) : mr.gameObject.AddComponent<BoxCollider>();
                return box != null;
            }
            var mc = undo ? Undo.AddComponent<MeshCollider>(mr.gameObject) : mr.gameObject.AddComponent<MeshCollider>();
            if (mc == null) return false;
            mc.sharedMesh = mf.sharedMesh;
            return true;
        }
    }
}
