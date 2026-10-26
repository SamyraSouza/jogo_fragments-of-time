using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Peças do pacote Ancient Modular Tombs. Só carregam no Editor (AssetDatabase); numa build devolvem null e quem chama usa o visual procedural.</summary>
    public static class PackageAssets
    {
        const string Root = "Assets/Ancient Modular Tombs/";

        /// <summary>Instancia o prefab/modelo (caminho relativo ao pacote) sem colisores, animadores e scripts do pacote; null se não der.</summary>
        public static GameObject Spawn(string relPath, Transform parent)
        {
#if UNITY_EDITOR
            GameObject src = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(Root + relPath);
            if (src == null) { Debug.LogWarning("[RiftOfTime] Peça do pacote não encontrada: " + Root + relPath); return null; }
            GameObject g = Object.Instantiate(src, parent);
            foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (Animator a in g.GetComponentsInChildren<Animator>(true)) Object.Destroy(a);
            foreach (AudioSource a in g.GetComponentsInChildren<AudioSource>(true)) Object.Destroy(a);
            foreach (MonoBehaviour m in g.GetComponentsInChildren<MonoBehaviour>(true)) Object.Destroy(m);
            return g;
#else
            return null;
#endif
        }

        public static Bounds BoundsOf(GameObject g)
        {
            Renderer[] rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
