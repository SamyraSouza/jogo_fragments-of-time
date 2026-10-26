using System;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Fragmento do circuito temporal (diamante brilhante).</summary>
    public class FragmentPickup : Interactable
    {
        [Tooltip("Número do fragmento (1 a 3), só informativo.")]
        public int fragmentIndex = 1;
        public bool autoCollect;
        [Tooltip("Enquanto verdadeiro, o fragmento não pode ser pego (chefe da Fase 3 vivo).")]
        public bool locked;
        public float autoRadius = 1.8f;
        [Tooltip("Se preenchido, o fragmento só pode ser pego depois que este guardião cair.")]
        public MummyGuardian guardian;

        public event Action<FragmentPickup> Collected;

        bool collected;
        Transform visual;
        Vector3 basePos;
        bool started;

        [Tooltip("Fala do jogador na primeira vez que ele enxerga o fragmento (vazio = não fala).")]
        public string seenLine;
        bool spoke;
        Transform cam;

        const string GatePath = "Assets/Ancient Modular Tombs/Meshes/portal_gate_sci-fi.glb";
        string pieceNode = "Gsf_mat2-017_low001";     // peça mecânica da máquina (conector com haste)

        /// <summary>Uma das peças quebradas da máquina do tempo (só no Editor, onde o modelo da máquina é carregado); null se não der.</summary>
        GameObject BuildPiece()
        {
#if UNITY_EDITOR
            GameObject gate = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(GatePath);
            if (gate == null) return null;
            GameObject inst = Instantiate(gate);
            Transform node = null;
            foreach (Transform t in inst.GetComponentsInChildren<Transform>(true)) if (t.name == pieceNode) { node = t; break; }
            if (node == null) { Destroy(inst); return null; }

            var holder = new GameObject("Diamond");
            holder.transform.SetParent(transform, false);
            GameObject piece = Instantiate(node.gameObject, holder.transform);
            Destroy(inst);
            piece.SetActive(true);
            foreach (Collider c in piece.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (Light l in piece.GetComponentsInChildren<Light>(true)) Destroy(l);
            foreach (Animation a in piece.GetComponentsInChildren<Animation>(true)) a.enabled = false;
            foreach (Animator a in piece.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            piece.transform.localPosition = Vector3.zero;
            piece.transform.localRotation = Quaternion.identity;
            piece.transform.localScale = Vector3.one;

            Renderer[] rs = piece.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { Destroy(holder); return null; }
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (longest > 0.0001f) piece.transform.localScale = Vector3.one * (0.5f / longest);
            b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            piece.transform.position += holder.transform.position - b.center;
            ShineStripes.Apply(holder);
            return holder;
#else
            return null;
#endif
        }

        void Awake()
        {
            GameObject d = BuildPiece();
            if (d == null) d = ProcBuild.DiamondObject(transform, "Diamond", new Color(1f, 0.82f, 0.3f), 0.55f);
            visual = d.transform;
            var l = new GameObject("Glow").AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.8f, 0.4f);
            l.range = 7f;
            l.intensity = 3f;
            basePos = visual.localPosition;
        }

        public override bool IsAvailable { get { return !collected && !locked && isActiveAndEnabled; } }

        public override string Prompt
        {
            get
            {
                if (guardian != null && !guardian.IsDead) return "Derrote o guardião para pegar o fragmento";
                return "[E] Coletar fragmento";
            }
        }

        public override void Interact(PlayerInteractor who)
        {
            if (guardian != null && !guardian.IsDead)
            {
                if (HUD.Instance != null) HUD.Instance.ShowSubtitle("O guardião ainda protege o fragmento.", 2.5f);
                return;
            }
            Collect();
        }

        void Update()
        {
            if (!started) { started = true; basePos = visual.localPosition; }
            if (!spoke && !string.IsNullOrEmpty(seenLine) && !collected && Seen())
            {
                spoke = true;
                if (HUD.Instance != null) HUD.Instance.ShowDialogue(seenLine, 4f);
            }

            if (autoCollect && !collected)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null && Vector3.Distance(p.transform.position + Vector3.up, transform.position) < autoRadius)
                    Collect();
            }
        }

        bool Seen()
        {
            if (cam == null) { Camera c = Camera.main; if (c == null) return false; cam = c.transform; }
            Vector3 to = transform.position - cam.position;
            if (to.sqrMagnitude > 16f * 16f) return false;
            Vector3 vp = cam.GetComponent<Camera>().WorldToViewportPoint(transform.position);
            if (vp.z <= 0f || vp.x < 0.1f || vp.x > 0.9f || vp.y < 0.1f || vp.y > 0.9f) return false;
            RaycastHit hit;
            if (Physics.Linecast(cam.position, transform.position, out hit, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform)) return false;
            return true;
        }

        public void Collect()
        {
            if (collected) return;
            collected = true;
            GameManager.Ensure().AddFragment();
            CameraShake.Kick(0.03f, 0.3f);
            if (Collected != null) Collected(this);
            gameObject.SetActive(false);
        }
    }
}
