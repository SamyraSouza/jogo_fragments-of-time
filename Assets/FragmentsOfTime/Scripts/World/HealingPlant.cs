using System.Collections.Generic;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Vaso de planta curandeira escondido num canto do labirinto da Fase 2: [E] recupera uma parte da vida.</summary>
    public class HealingPlant : Interactable
    {
        const string ModelPath = "Plantas/PlantBanjoFig001";
        const string TexturePath = "Plantas/PlantBanjoFig001_COL";

        public float healFraction = 0.25f;
        public float height = 0.7f;

        public override string Prompt { get { return "<color=#EDE8DC>[E] pegar    [H] usar</color>"; } }
        public override float Radius { get { return 2.2f; } }

        /// <summary>Espalha vasos em cantos aleatórios do labirinto (becos sem saída primeiro), longe da entrada, da saída e do papiro.</summary>
        public static void SpawnInMaze(LabyrinthMap map, int count)
        {
            GameObject prefab = Resources.Load<GameObject>(ModelPath);
            if (prefab == null) return;
            Texture2D tex = Resources.Load<Texture2D>(TexturePath);

            Vector3 papyrus = Vector3.positiveInfinity;
            MazePapyrus pap = Object.FindFirstObjectByType<MazePapyrus>();
            if (pap != null) papyrus = pap.transform.position;

            var deadEnds = new List<Vector3>();
            var corners = new List<Vector3>();
            for (int r = 0; r < map.rows; r++)
            {
                for (int c = 0; c < map.cols; c++)
                {
                    if (c == map.startCell.x && r == map.startCell.y) continue;
                    if (c == map.exitCell.x && r == map.exitCell.y) continue;
                    // paredes: sul, norte, oeste, leste
                    bool[] w = { map.HasH(c, r), map.HasH(c, r + 1), map.HasV(c, r), map.HasV(c + 1, r) };
                    int n = 0; for (int i = 0; i < 4; i++) if (w[i]) n++;
                    if (n < 1) continue;

                    float cx = map.x0 + (c + 0.5f) * map.cell, cz = map.z0 + (r + 0.5f) * map.cell;
                    float off = map.cell * 0.32f;
                    // encosta no canto formado pelas paredes
                    float dx = (w[2] ? -off : 0f) + (w[3] ? off : 0f);
                    float dz = (w[0] ? -off : 0f) + (w[1] ? off : 0f);
                    var pos = new Vector3(cx + dx, 0f, cz + dz);
                    if (new Vector2(pos.x - papyrus.x, pos.z - papyrus.z).magnitude < map.cell * 1.5f) continue;
                    (n >= 3 ? deadEnds : corners).Add(pos);
                }
            }

            var picks = new List<Vector3>();
            Shuffle(deadEnds); Shuffle(corners);
            foreach (Vector3 p in deadEnds) { if (picks.Count >= count) break; picks.Add(p); }
            foreach (Vector3 p in corners) { if (picks.Count >= count) break; picks.Add(p); }

            Transform player = null;
            GameObject pg = GameObject.FindGameObjectWithTag("Player");
            if (pg != null) player = pg.transform;
            float baseY = player != null ? player.position.y : 0.2f;

            foreach (Vector3 p in picks)
            {
                Vector3 pos = p;
                RaycastHit hit;
                if (Physics.Raycast(new Vector3(p.x, baseY + 3f, p.z), Vector3.down, out hit, 8f, ~0, QueryTriggerInteraction.Ignore)) pos.y = hit.point.y;
                else pos.y = baseY - 0.9f;
                Make(prefab, tex, pos);
                MossOnWalls(pos, map);
            }
        }

        static void Shuffle(List<Vector3> l)
        {
            for (int i = l.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                Vector3 t = l[i]; l[i] = l[j]; l[j] = t;
            }
        }

        static void Make(GameObject prefab, Texture2D tex, Vector3 pos)
        {
            var root = new GameObject("PlantaCurandeira");
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var plant = root.AddComponent<HealingPlant>();

            GameObject g = Instantiate(prefab, root.transform);
            foreach (Collider c in g.GetComponentsInChildren<Collider>()) Destroy(c);

            // aspecto lustroso/refletivo, parado
            Material mat = RotMaterials.Lit(Color.white);
            mat.SetFloat("_Metallic", 0.6f);
            mat.SetFloat("_Smoothness", 0.95f);
            if (tex != null) { mat.SetTexture("_BaseMap", tex); mat.mainTexture = tex; }
            Renderer[] rs = g.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in rs)
            {
                var ms = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < ms.Length; i++) ms[i] = mat;
                r.sharedMaterials = ms;
            }

            g.transform.localPosition = Vector3.zero;
            g.transform.localRotation = Quaternion.identity;
            g.transform.localScale = Vector3.one;
            Bounds b = BoundsOf(rs);
            if (b.size.y > 0.0001f) g.transform.localScale = Vector3.one * (plant.height / b.size.y);
            b = BoundsOf(rs);
            // base do vaso no chão, centrado no ponto
            g.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);

            ShineStripes.Apply(g);
        }

        static Material mossMat;

        /// <summary>Musgo cobrindo por inteiro as paredes da célula do vaso, do chão ao teto, rente à parede.</summary>
        static void MossOnWalls(Vector3 pos, LabyrinthMap map)
        {
            Texture2D tex = Resources.Load<Texture2D>("RoT/Moss_Wall");
            if (tex == null) return;
            if (mossMat == null)
            {
                mossMat = RotMaterials.Lit(Color.white);
                mossMat.SetTexture("_BaseMap", tex);
                mossMat.mainTexture = tex;
                mossMat.SetFloat("_AlphaClip", 1f);
                mossMat.SetFloat("_Cutoff", 0.45f);
                mossMat.EnableKeyword("_ALPHATEST_ON");
                mossMat.mainTextureScale = new Vector2(Mathf.Max(1f, map.cell / 3f), 1f);
                mossMat.SetTextureScale("_BaseMap", mossMat.mainTextureScale);
                mossMat.EnableKeyword("_EMISSION");
                mossMat.SetTexture("_EmissionMap", tex);
                mossMat.SetColor("_EmissionColor", new Color(0.22f, 0.22f, 0.22f));
                mossMat.SetFloat("_Smoothness", 0.1f);
                mossMat.SetFloat("_Metallic", 0f);
                mossMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }

            // todas as paredes da célula da planta (ao redor, dos lados e atrás), cobertas por inteiro
            Vector3 cc = new Vector3(map.x0 + (Mathf.Floor((pos.x - map.x0) / map.cell) + 0.5f) * map.cell, pos.y, map.z0 + (Mathf.Floor((pos.z - map.z0) / map.cell) + 0.5f) * map.cell);
            Vector3 from = cc + Vector3.up * 0.5f;
            Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            foreach (Vector3 d in dirs)
            {
                RaycastHit hit;
                if (!Physics.Raycast(from, d, out hit, map.cell * 0.8f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(hit.normal.y) > 0.3f) continue;          // só paredes

                Vector3 n = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
                Vector3 c = hit.point + n * 0.012f;
                c.y = pos.y;
                float top = pos.y + 3f;
                RaycastHit up;
                if (Physics.Raycast(c + n * 0.3f + Vector3.up * 0.5f, Vector3.up, out up, 8f, ~0, QueryTriggerInteraction.Ignore)) top = up.point.y;
                float bottom = pos.y - 0.05f;
                float h = Mathf.Clamp(top - bottom, 1.5f, 5f);
                c.y = bottom + h * 0.5f;

                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Musgo";
                Destroy(q.GetComponent<Collider>());
                q.transform.position = c;
                q.transform.rotation = Quaternion.LookRotation(-n, Vector3.up);
                q.transform.localScale = new Vector3(map.cell, h, 1f);
                Renderer r = q.GetComponent<Renderer>();
                r.sharedMaterial = mossMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
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

        public override void Interact(PlayerInteractor who)
        {
            PlantUser.Count++;
            PlantUser.Ensure();
            gameObject.SetActive(false);
        }
    }

    /// <summary>Guarda as plantas coletadas; [H] usa uma e recupera parte da vida.</summary>
    public class PlantUser : MonoBehaviour
    {
        public static int Count;
        static PlantUser inst;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Count = 0; inst = null; }

        public static void Ensure()
        {
            if (inst != null) return;
            inst = new GameObject("PlantUser").AddComponent<PlantUser>();
        }

        void Update()
        {
            if (Count <= 0) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.hKey.wasPressedThisFrame) return;
            PlayerHealth hp = Object.FindFirstObjectByType<PlayerHealth>();
            if (hp == null || hp.IsDead) return;
            if (hp.Current >= hp.maxHealth - 0.01f) return;
            Count--;
            hp.Heal(hp.maxHealth * 0.25f);
        }
    }
}
