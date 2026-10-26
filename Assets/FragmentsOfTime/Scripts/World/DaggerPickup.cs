using System;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Adaga de Tutancâmon jogada no chão da Fase 1: ao pegar, ela vai para a mão direita do jogador.</summary>
    public class DaggerPickup : Interactable
    {
        const float Length = 0.42f;

        public event Action Taken;
        bool taken;

        public static DaggerPickup Create(Vector3 pos)
        {
            var g = new GameObject("Adaga");
            g.transform.position = pos;
            return g.AddComponent<DaggerPickup>();
        }

        public override bool IsAvailable { get { return !taken; } }
        public override float Radius { get { return 2.2f; } }
        public override string Prompt { get { return "[E] Pegar a adaga"; } }

        void Awake()
        {
            GameObject prefab = Resources.Load<GameObject>("Dagger/Dagger");
            Transform model;
            if (prefab != null)
            {
                model = Instantiate(prefab, transform).transform;
                foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);
                Material mat = RotMaterials.Lit(Color.white);
                Texture2D tex = Resources.Load<Texture2D>("Dagger/DaggerTexture");
                if (tex != null) { mat.SetTexture("_BaseMap", tex); mat.mainTexture = tex; }
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                {
                    var ms = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                    for (int i = 0; i < ms.Length; i++) ms[i] = mat;
                    r.sharedMaterials = ms;
                }
                // deita a adaga: o maior eixo da malha fica ao longo de X
                MeshFilter mf = model.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Bounds b = mf.sharedMesh.bounds;
                    int axis = b.size.x >= b.size.y && b.size.x >= b.size.z ? 0 : (b.size.y >= b.size.z ? 1 : 2);
                    Quaternion rot = axis == 0 ? Quaternion.identity : (axis == 1 ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(0f, 90f, 0f));
                    float s = Length / Mathf.Max(0.0001f, b.size[axis]);
                    model.localRotation = rot;
                    model.localScale = Vector3.one * s;
                    model.localPosition = new Vector3(0f, b.size[axis == 1 ? 0 : 1] * s * 0.5f + 0.02f, 0f) - rot * (b.center * s);
                }
            }
            else
            {
                Material steel = RotMaterials.Lit(new Color(0.78f, 0.8f, 0.84f));
                model = ProcBuild.Cube(transform, "Lamina", new Vector3(0f, 0.03f, 0f), new Vector3(0.42f, 0.02f, 0.05f), steel).transform;
            }

            var l = new GameObject("Brilho").AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.85f, 0.5f);
            l.range = 3.5f;
            l.intensity = 2.2f;
        }

        public override void Interact(PlayerInteractor who)
        {
            if (taken) return;
            taken = true;

            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                Transform arms = null;
                foreach (Transform t in p.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Arms") { arms = t; break; }
                if (arms != null)
                {
                    if (arms.GetComponent<HeldDagger>() == null) arms.gameObject.AddComponent<HeldDagger>();
                    PunchAnimator pa = arms.GetComponentInChildren<PunchAnimator>(true);
                    if (pa != null) pa.GiveDagger();
                }
            }

            if (HUD.Instance != null)
                HUD.Instance.ShowDialogue("Clique com o botão esquerdo do mouse (ou Espaço) para atacar.", 6f, false);
            if (Taken != null) Taken();
            gameObject.SetActive(false);
        }
    }
}
