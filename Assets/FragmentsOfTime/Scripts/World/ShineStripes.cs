using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Brilho em faixas diagonais ("/ /") que varre o objeto, ao estilo dos itens de Resident Evil.</summary>
    public static class ShineStripes
    {
        static Material shared;

        public static void Apply(GameObject root)
        {
            Material m = Get();
            if (m == null) return;
            foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>())
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || r.gameObject.name == "Brilho_Faixas") continue;
                var go = new GameObject("Brilho_Faixas");
                go.transform.SetParent(r.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var nr = go.AddComponent<MeshRenderer>();
                var ms = new Material[Mathf.Max(1, mf.sharedMesh.subMeshCount)];
                for (int i = 0; i < ms.Length; i++) ms[i] = m;
                nr.sharedMaterials = ms;
                nr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                nr.receiveShadows = false;
            }
        }

        static Material Get()
        {
            if (shared != null) return shared;
            Shader sh = Resources.Load<Shader>("Shaders/ShineStripes");
            if (sh == null) sh = Shader.Find("RiftOfTime/ShineStripes");
            if (sh == null) return null;
            shared = new Material(sh);
            return shared;
        }
    }
}
