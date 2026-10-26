using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Visual do Faraó da Fase 2: o modelo 3D de Assets/Characters/Mummys (model/SK_1057_1057302_Lobby_ao.fbx) no lugar do corpo de primitivas.</summary>
    public class PharaohVisual : MonoBehaviour
    {
        const string ModelPath = "Assets/Characters/Mummys/model/SK_1057_1057302_Lobby_ao.fbx";
        const string TexDir = "Assets/Characters/Mummys/textures/";
        static readonly string[] Tags = { "Body_01", "Body_02", "Body_03", "Equip_01", "Equip_02", "Equip_03" };

        MummyGuardian mummy;
        Transform visual;
        readonly List<Material> materials = new List<Material>();
        readonly Dictionary<string, Material> byName = new Dictionary<string, Material>();
        float animT, lastHealth, hitFlash;
        bool fallen;

        /// <summary>Monta o modelo como filho "Model" do guardião.</summary>
        public static bool Attach(GameObject guardian, float height)
        {
#if UNITY_EDITOR
            GameObject model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) { Debug.LogWarning("[Faraó] modelo não encontrado em " + ModelPath + ": usando o visual procedural."); return false; }
            PharaohVisual pv = guardian.AddComponent<PharaohVisual>();
            pv.Build(model, height);
            return true;
#else
            return false;
#endif
        }

        void Build(GameObject model, float height)
        {
            visual = new GameObject("Model").transform;
            visual.SetParent(transform, false);

            GameObject g = Instantiate(model, visual);
            g.name = "Mesh";
            foreach (Collider c in g.GetComponentsInChildren<Collider>()) Destroy(c);

            Renderer[] rs = g.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in rs)
            {
                Material[] src = r.sharedMaterials;
                var ms = new Material[Mathf.Max(1, src.Length)];
                for (int i = 0; i < ms.Length; i++) ms[i] = MaterialFor(i < src.Length && src[i] != null ? src[i].name : "");
                r.sharedMaterials = ms;
            }

            Bounds b = BoundsOf(rs);
            if (b.size.z > b.size.y * 1.2f && b.size.z > b.size.x)
                g.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            b = BoundsOf(rs);
            if (b.size.y > 0.0001f) g.transform.localScale = Vector3.one * (height / b.size.y);
            b = BoundsOf(rs);
            Vector3 feet = new Vector3(b.center.x, b.min.y, b.center.z);
            g.transform.position += transform.position - feet;
            Debug.Log("[Faraó] modelo de Characters/Mummys montado; " + rs.Length + " malha(s), " + materials.Count + " material(is).");
        }

        Material MaterialFor(string matName)
        {
            Material m;
            if (byName.TryGetValue(matName, out m)) return m;

            string tag = null;
            foreach (string t in Tags) if (matName.Contains(t)) { tag = t; break; }

            m = RotMaterials.Lit(Color.white);
            m.name = "Faraó_" + (matName == "" ? "padrao" : matName);
#if UNITY_EDITOR
            if (tag != null)
            {
                var d = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "T_1057302_" + tag + "_D.png");
                var n = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "T_1057302_" + tag + "_N.png");
                if (d != null) { m.SetTexture("_BaseMap", d); m.mainTexture = d; }
                if (n != null && m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
            }
#endif
            if (tag == null) RotMaterials.SetColor(m, matName.Contains("Eyes") ? new Color(0.9f, 0.85f, 0.75f) : new Color(0.16f, 0.14f, 0.12f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            byName[matName] = m;
            materials.Add(m);
            return m;
        }

        static Bounds BoundsOf(Renderer[] rs)
        {
            Bounds b = default(Bounds);
            bool first = true;
            foreach (Renderer r in rs)
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        void Start()
        {
            mummy = GetComponent<MummyGuardian>();
            if (mummy == null) { enabled = false; return; }
            lastHealth = mummy.Health;
            mummy.Died += OnDied;
        }

        void OnDestroy()
        {
            if (mummy != null) mummy.Died -= OnDied;
        }

        void Update()
        {
            if (visual == null || mummy == null || fallen) return;
            animT += Time.deltaTime;

            if (mummy.Health < lastHealth - 0.01f) hitFlash = 0.12f;
            lastHealth = mummy.Health;
            hitFlash = Mathf.Max(0f, hitFlash - Time.deltaTime);
            Color tint = hitFlash > 0f ? new Color(1f, 0.35f, 0.3f) : Color.white;
            foreach (Material m in materials)
                if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) RotMaterials.SetColor(m, tint);

            float pitch = 0f, bob = 0f, sway = 0f;
            switch (mummy.Current)
            {
                case MummyGuardian.State.Dormant:
                    pitch = 14f;
                    break;
                case MummyGuardian.State.Awakening:
                    pitch = Mathf.Lerp(14f, 0f, Mathf.PingPong(animT * 0.8f, 1f));
                    break;
                case MummyGuardian.State.Chase:
                case MummyGuardian.State.Recover:
                    bob = Mathf.Abs(Mathf.Sin(animT * 6f)) * 0.06f;
                    sway = Mathf.Sin(animT * 6f) * 4f;
                    pitch = 6f;
                    break;
                case MummyGuardian.State.Windup:
                    pitch = -14f;
                    break;
                case MummyGuardian.State.Strike:
                    pitch = 26f;
                    break;
            }
            Quaternion target = Quaternion.Euler(pitch, 0f, sway);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, target, Time.deltaTime * 12f);
            visual.localPosition = new Vector3(0f, bob, 0f);
        }

        void OnDied(MummyGuardian m)
        {
            StartCoroutine(Fall());
        }

        IEnumerator Fall()
        {
            fallen = true;
            if (visual == null) yield break;
            Quaternion from = visual.localRotation, to = Quaternion.Euler(-90f, 0f, 0f);
            float t = 0f;
            while (t < 0.9f)
            {
                t += Time.deltaTime;
                float k = t / 0.9f;
                visual.localRotation = Quaternion.Slerp(from, to, k * k);
                yield return null;
            }
        }
    }
}
