using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Papiro escondido no labirinto da Fase 2.</summary>
    public class MazePapyrus : PapyrusPickup
    {
        const string ModelPath = "Assets/Movels/paper/source/old_papers.fbx";
        const string TexturePath = "Assets/Movels/paper/textures/texture_pbr_20250901.png";

        [Tooltip("Modelo do papel (vazio = o visual de rolo gerado por código).")]
        public GameObject model;
        [Tooltip("Textura do papel.")]
        public Texture2D texture;

        public override string Prompt { get { return "<color=#EDE8DC>[E]</color>"; } }

        protected override void Awake()
        {
            base.Awake();

            bool folder = false;
            GameObject folderModel = Resources.Load<GameObject>("Papiro/folder");     // pasta da Fase 2 (vale mesmo que a cena guarde o modelo antigo)
            if (folderModel != null) { model = folderModel; folder = true; }
#if UNITY_EDITOR
            if (model == null) model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (texture == null) texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
#endif
            if (model == null) return;

            foreach (string n in new[] { "Scroll", "RodL", "RodR" })
            {
                Transform t = transform.Find(n);
                if (t != null) t.gameObject.SetActive(false);
            }

            GameObject g = Instantiate(model, transform);
            g.name = "PapelAntigo";
            foreach (Collider c in g.GetComponentsInChildren<Collider>()) Destroy(c);

            Material mat = RotMaterials.Lit(folder ? new Color(0.62f, 0.45f, 0.26f) : Color.white);
            if (folder)
            {
                // brilho "espelhado" de item colecionável (estilo Resident Evil): metálico e liso, parado
                mat.SetFloat("_Metallic", 1f);
                mat.SetFloat("_Smoothness", 1f);
                RotMaterials.SetColor(mat, new Color(0.85f, 0.78f, 0.66f));
                texture = null;
            }
            if (texture != null) { mat.SetTexture("_BaseMap", texture); mat.mainTexture = texture; }
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
            float biggest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (biggest > 0.0001f) g.transform.localScale = Vector3.one * (0.45f / biggest);
            b = BoundsOf(rs);
            float topY = 1.1f;
            Transform ped = transform.Find("Pedestal");
            GameObject stone = BuildStone(transform, transform.position, 1.1f);
            if (stone != null)
            {
                if (ped != null) ped.gameObject.SetActive(false);
                Bounds sb = BoundsOf(stone.GetComponentsInChildren<Renderer>());
                topY = sb.max.y - transform.position.y;
            }
            g.transform.position += transform.position + Vector3.up * (topY + b.extents.y + 0.02f) - b.center;

            ShineStripes.Apply(g);

            // reflexo de espelho: sonda que captura o entorno uma vez
            var probeGo = new GameObject("ReflexoPapiro");
            probeGo.transform.SetParent(transform, false);
            probeGo.transform.localPosition = Vector3.up * (topY + 0.6f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 64;
            probe.size = new Vector3(6f, 4f, 6f);
            probe.intensity = 1.5f;
            probe.RenderProbe();
        }

        /// <summary>Pedra de Roseta deitada no chão (a face mais larga para cima), servindo de base para o papiro.</summary>
        public static GameObject BuildStone(Transform parent, Vector3 pos, float length)
        {
            GameObject src = Resources.Load<GameObject>("Papiro/RosettaStone");
            if (src == null) return null;
            Texture2D tex = Resources.Load<Texture2D>("Papiro/RosettaStone_COL");

            GameObject s = Instantiate(src, parent);
            s.name = "PedraRoseta";
            foreach (Collider c in s.GetComponentsInChildren<Collider>()) Destroy(c);
            Material m = RotMaterials.Lit(Color.white);
            m.SetFloat("_Smoothness", 0.25f);
            if (tex != null) { m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
            Renderer[] rs = s.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in rs)
            {
                var ms = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < ms.Length; i++) ms[i] = m;
                r.sharedMaterials = ms;
            }

            s.transform.localPosition = Vector3.zero;
            s.transform.localRotation = Quaternion.identity;
            s.transform.localScale = Vector3.one;
            Vector3 sz = BoundsOf(rs).size;
            // eixo mais fino vira o "para cima"
            if (sz.z <= sz.x && sz.z <= sz.y) s.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            else if (sz.x <= sz.y && sx(sz)) s.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Bounds b = BoundsOf(rs);
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (longest > 0.0001f) s.transform.localScale = Vector3.one * (length / longest);
            b = BoundsOf(rs);
            s.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
            return s;
        }

        static bool sx(Vector3 sz) { return sz.x <= sz.z; }

        bool spoke;
        Transform playerT;

        void Update()
        {
            if (spoke) return;
            if (playerT == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p == null) return;
                playerT = p.transform;
            }
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 target = transform.position + Vector3.up * 0.6f;
            if ((cam.transform.position - target).sqrMagnitude > 16f * 16f) return;
            Vector3 vp = cam.WorldToViewportPoint(target);
            if (vp.z <= 0f || vp.x < 0.2f || vp.x > 0.8f || vp.y < 0.15f || vp.y > 0.85f) return;
            RaycastHit hit;
            if (Physics.Linecast(cam.transform.position, target, out hit, ~0, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(playerT)) return;      // parede no meio: ainda não viu
            spoke = true;
            if (HUD.Instance != null) HUD.Instance.ShowDialogue("Eles estão brincando comigo...", 4f);
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
            LabyrinthMap map = LabyrinthMap.Instance;
            if (map == null) return;
            map.Unlock();
            gameObject.SetActive(false);
        }
    }
}
