using System.Collections.Generic;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Faixa de lâminas no chão que atravessa o corredor (Fase 3). Sempre sobra uma passagem livre; as lâminas sobem e descem num ritmo.</summary>
    public class BladeTrap : MonoBehaviour
    {
        public float damage = 12f;
        public float halfWidth = 0.55f;          // metade da espessura da faixa (eixo X)
        public float safeHalf = 1.0f;            // metade da passagem livre (eixo Z)
        public float zMin = 10.45f, zMax = 14.55f;
        public float gapZ = 12.5f;
        public float cycle = 2.6f, phase;

        const float Tip = 0.42f;
        readonly List<Transform> blades = new List<Transform>();
        Transform player;
        PlayerHealth health;
        float floorY, nextHit, up;
        static bool armed;
        public static void Arm() { armed = true; }
        float lowY = -0.3f, highY = Tip * 0.5f - 0.02f;

        /// <summary>Espalha as faixas pelo corredor da Câmara do Tempo.</summary>
        public static void SpawnCorridor()
        {
            armed = false;     // as pontas só se erguem quando a câmara começa a desmoronar
            float[] xs = { 17f, 25f, 33f, 41f };
            float[] gaps = { 11.7f, 13.3f, 11.9f, 13.1f };
            var root = new GameObject("Laminas");
            for (int i = 0; i < xs.Length; i++)
            {
                var g = new GameObject("FaixaLaminas " + (i + 1));
                g.transform.SetParent(root.transform, false);
                g.transform.position = new Vector3(xs[i], 0f, 12.5f);
                var t = g.AddComponent<BladeTrap>();
                t.gapZ = gaps[i];
                t.phase = i * 0.65f;
            }
        }

        void Start()
        {
            RaycastHit hit;
            floorY = Physics.Raycast(new Vector3(transform.position.x, 3f, gapZ), Vector3.down, out hit, 8f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;
            float oy = transform.position.y, oz = transform.position.z;

            Material metal = RotMaterials.Lit(new Color(0.78f, 0.78f, 0.8f));
            metal.SetFloat("_Metallic", 0.9f);
            metal.SetFloat("_Smoothness", 0.8f);
            // o chão do trecho dos espinhos: só uma pedra de outra textura, sem borda
            Renderer floorR = hit.collider != null ? hit.collider.GetComponentInParent<Renderer>() : null;
            if (floorR == null && hit.collider != null) floorR = hit.collider.GetComponentInChildren<Renderer>();
            Material plate = floorR != null ? floorR.sharedMaterial : RotMaterials.Lit(new Color(0.55f, 0.5f, 0.45f));
            Texture2D tile = Resources.Load<Texture2D>("RoT/Sandstone_A");
            if (tile != null && floorR == null)
            {
                plate.color = new Color(0.45f, 0.4f, 0.36f);
                plate.SetColor("_BaseColor", plate.color);
                plate.mainTexture = tile;
                plate.SetTexture("_BaseMap", tile);
                plate.mainTextureScale = new Vector2(1.2f, 0.5f);
            }

            for (float z = zMin + 0.25f; z < zMax; z += 0.5f)
            {
                if (Mathf.Abs(z - gapZ) <= safeHalf + 0.2f) continue;
                // base escura no chão + lâmina em losango (a ponta fica para cima)
                ProcBuild.Primitive(transform, "Piso", PrimitiveType.Cube, new Vector3(0f, floorY + 0.004f - oy, z - oz), new Vector3(1.3f, 0.016f, 0.5f), plate);
                Transform sp = BuildSpikes(z - oz);
                if (sp != null) { blades.Add(sp); continue; }
                Transform b = ProcBuild.Primitive(transform, "Lamina", PrimitiveType.Cube, new Vector3(0f, floorY - 0.3f - oy, z - oz), new Vector3(0.3f, 0.3f, 0.06f), metal);
                b.localRotation = Quaternion.Euler(0f, 0f, 45f);
                blades.Add(b);
            }

            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) { player = p.transform; health = p.GetComponent<PlayerHealth>(); }
        }

        /// <summary>Espinhos do pacote (Metal_Spikes) em duas colunas por fileira; null fora do Editor.</summary>
        Transform BuildSpikes(float localZ)
        {
            var holder = new GameObject("Espinhos").transform;
            holder.SetParent(transform, false);
            holder.localPosition = new Vector3(0f, 0f, localZ);
            float h = 0f;
            for (int i = 0; i < 2; i++)
            {
                GameObject m = PackageAssets.Spawn("Interior modular meshes/Metal_Spikes.fbx", holder);
                if (m == null) { Destroy(holder.gameObject); return null; }
                m.transform.localPosition = Vector3.zero;
                m.transform.localRotation = Quaternion.identity;
                m.transform.localScale = Vector3.one;
                Bounds b = PackageAssets.BoundsOf(m);
                float foot = Mathf.Max(b.size.x, b.size.z);
                if (foot < 0.0001f) { Destroy(holder.gameObject); return null; }
                float s = 0.5f / foot;
                m.transform.localScale = Vector3.one * s;
                b = PackageAssets.BoundsOf(m);
                // base na altura do holder, centrada na coluna
                m.transform.position += new Vector3((i == 0 ? -0.28f : 0.28f) * transform.lossyScale.x, 0f, 0f) + holder.position - new Vector3(b.center.x, b.min.y, b.center.z);
                foreach (Renderer r in m.GetComponentsInChildren<Renderer>())      // some a placa/base cinza do modelo
                    if (r.bounds.size.y < b.size.y * 0.25f && r.bounds.size.x > b.size.x * 0.8f && r.bounds.size.z > b.size.z * 0.8f) r.enabled = false;
                h = Mathf.Max(h, b.size.y);
            }
            lowY = -h - 0.05f;
            highY = -0.01f;
            return holder;
        }

        void Update()
        {
            // 0 = recolhida · sobe · 1 = erguida · desce
            float c = Mathf.Repeat(Time.time + phase, cycle) / cycle;
            if (!armed) up = 0f;
            else if (c < 0.35f) up = 0f;
            else if (c < 0.5f) up = Mathf.SmoothStep(0f, 1f, (c - 0.35f) / 0.15f);
            else if (c < 0.85f) up = 1f;
            else up = 1f - Mathf.SmoothStep(0f, 1f, (c - 0.85f) / 0.15f);

            foreach (Transform b in blades)
            {
                Vector3 lp = b.localPosition;
                lp.y = (floorY - transform.position.y) + Mathf.Lerp(lowY, highY, up);
                b.localPosition = lp;
            }

            if (player == null || health == null || health.IsDead || !armed || up < 0.6f || Time.time < nextHit) return;
            Vector3 d = player.position - new Vector3(transform.position.x, floorY, gapZ);
            if (Mathf.Abs(d.x) > halfWidth + 0.15f) return;
            if (d.y > 0.7f || d.y < -1.5f) return;                         // pulou por cima
            float pz = player.position.z;
            if (pz < zMin || pz > zMax || Mathf.Abs(pz - gapZ) <= safeHalf + 0.15f) return;   // na passagem livre
            nextHit = Time.time + 1f;
            health.TakeDamage(damage);
            if (HUD.Instance != null) HUD.Instance.FlashRed(0.55f);
        }
    }
}
