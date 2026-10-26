using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>A máquina do tempo.</summary>
    public class TimeMachine : Interactable
    {
        const string GatePath = "Assets/Ancient Modular Tombs/Meshes/portal_gate_sci-fi.glb";
        const float GateScale = 0.5f;

        public string prompt = "[E] Reconstruir a máquina";
        public bool canInteract;
        public event Action<TimeMachine> Activated;

        readonly List<Transform> slots = new List<Transform>();
        readonly List<GameObject> portalParts = new List<GameObject>();
        bool rebuilt;

        public bool IsRebuilt { get { return rebuilt; } }

        Animation legacyAnim;
        AnimationState legacyState;
        Animator animator;
        bool animCached;

        void Awake()
        {
#if UNITY_EDITOR
            // a cena já pode trazer o modelo da máquina: sem este teste nasce uma segunda, menor, por cima
            if (transform.Find("Modelo") == null && GetComponentInChildren<Renderer>(true) == null)
            {
                GameObject gate = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(GatePath);
                if (gate == null) { Debug.LogWarning("[RiftOfTime] Modelo da máquina não encontrado em " + GatePath + " (o glTFast precisa ter importado o arquivo).", this); return; }
                GameObject model = Instantiate(gate, transform);
                model.name = "Modelo";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * GateScale;
                foreach (AudioListener a in model.GetComponentsInChildren<AudioListener>(true)) Destroy(a);
                foreach (Camera c in model.GetComponentsInChildren<Camera>(true)) Destroy(c);
                foreach (Light l in model.GetComponentsInChildren<Light>(true)) Destroy(l);
                if (model.GetComponentInChildren<Collider>() == null)
                    foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>())
                        if (mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>();
            }
#endif
        }

        void CacheAnimation()
        {
            if (animCached) return;
            animCached = true;
            legacyAnim = GetComponentInChildren<Animation>(true);
            if (legacyAnim != null)
            {
                foreach (AnimationState s in legacyAnim) { legacyState = s; break; }
                if (legacyState != null) legacyState.wrapMode = WrapMode.Loop;
            }
            else animator = GetComponentInChildren<Animator>(true);

            if (legacyState == null && animator == null)
                Debug.LogWarning("[RiftOfTime] A máquina não tem animação (Animation/Animator) para controlar.", this);
        }

        /// <summary>Duração do clipe em segundos na velocidade 1 (0 se não achou o clipe).</summary>
        public float AnimationClipLength { get { CacheAnimation(); return legacyState != null ? legacyState.length : 0f; } }

        /// <summary>Velocidade da animação do portal: 1 = original, 0 = parada, 2 = o dobro.</summary>
        public void SetAnimationSpeed(float speed)
        {
            CacheAnimation();
            if (legacyState != null)
            {
                if (!legacyAnim.IsPlaying(legacyState.name)) legacyAnim.Play(legacyState.name);
                legacyState.speed = speed;
            }
            else if (animator != null) animator.speed = speed;
        }

        public override bool IsAvailable { get { return canInteract && isActiveAndEnabled; } }
        public override string Prompt { get { return prompt; } }
        public override float Radius { get { return 3.2f; } }

        public override void Interact(PlayerInteractor who)
        {
            if (!canInteract) return;
            canInteract = false;
            if (Activated != null) Activated(this);
        }

        /// <summary>Deixa a máquina quebrada e apagada: sem 3 segmentos do anel, sem portal/brilho, sem o chão de areia do modelo e sem animação.</summary>
        public void Break()
        {
            string[] missing = { "Object430", "Gsf_mat2-003_low002", "Object433", "Gsf_mat2-003_low003", "Object434", "Gsf_mat2-003_low004" };
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                foreach (string n in missing) if (t.name == n) t.gameObject.SetActive(false);
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    string mn = m.name.ToLowerInvariant();
                    bool portalMat = mn.Contains("portal") || mn.Contains("center");
                    if (mn.Contains("desert") || mn.Contains("winter") || mn.Contains("grass") || portalMat)
                    {
                        if (portalMat) portalParts.Add(r.gameObject);
                        r.gameObject.SetActive(false);
                        break;
                    }
                }
            }
            foreach (Animation a in GetComponentsInChildren<Animation>(true)) { a.playAutomatically = false; a.enabled = false; }
            foreach (Animator a in GetComponentsInChildren<Animator>(true)) a.enabled = false;
            foreach (Light l in GetComponentsInChildren<Light>(true)) l.enabled = false;
            animCached = true;
        }

        static readonly string[][] RingPieces =
        {
            new[] { "Object430", "Gsf_mat2-003_low002" },
            new[] { "Object433", "Gsf_mat2-003_low003" },
            new[] { "Object434", "Gsf_mat2-003_low004" },
        };

        /// <summary>A mão do jogador devolve ao anel as três peças que faltavam: cada uma sai da frente da mão e vai para o lugar de onde saiu.</summary>
        public IEnumerator PlacePieces(Transform body, SimpleFPSController fps, Transform cam, PunchAnimator punch)
        {
            foreach (string[] group in RingPieces)
            {
                var ts = new List<Transform>();
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                    foreach (string n in group) if (t.name == n) ts.Add(t);
                if (ts.Count == 0) continue;

                var target = new Vector3[ts.Count];
                Vector3 center = Vector3.zero;
                for (int i = 0; i < ts.Count; i++) { target[i] = ts[i].position; center += target[i]; }
                center /= ts.Count;

                // anda sozinho até a frente do vão (do lado da máquina em que está)
                Vector3 side = transform.forward;
                side.y = 0f;
                side.Normalize();
                Vector3 flat = new Vector3(center.x, body.position.y, center.z);
                if (Vector3.Dot(body.position - flat, side) < 0f) side = -side;
                Vector3 stand = flat + side * 1.3f;
                while ((body.position - stand).sqrMagnitude > 0.01f)
                {
                    Vector3 d = stand - body.position;
                    d.y = 0f;
                    Vector3 np = Vector3.MoveTowards(body.position, stand, 2.4f * Time.deltaTime);
                    if (fps != null) { fps.TeleportTo(np, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg); fps.LookAtPoint(center, Mathf.Clamp01(Time.deltaTime * 5f)); }
                    else body.position = np;
                    yield return null;
                }
                for (float w = 0f; w < 0.7f; w += Time.deltaTime)
                {
                    if (fps != null) { fps.TeleportTo(body.position, Mathf.Atan2(-side.x, -side.z) * Mathf.Rad2Deg); fps.LookAtPoint(center, Mathf.Clamp01(Time.deltaTime * 6f)); }
                    yield return null;
                }

                // a peça aparece na mão e a mão leva até o lugar de onde ela saiu
                Vector3 from = cam.position + cam.forward * 0.7f + cam.right * 0.25f - cam.up * 0.2f;
                foreach (Transform t in ts) t.gameObject.SetActive(true);

                if (punch != null) punch.Play();
                const float Dur = 1.3f;
                for (float k = 0f; k < 1f; k += Time.deltaTime / Dur)
                {
                    if (fps != null) fps.LookAtPoint(center, Mathf.Clamp01(Time.deltaTime * 6f));
                    float s = Mathf.SmoothStep(0f, 1f, k);
                    for (int i = 0; i < ts.Count; i++)
                        ts[i].position = target[i] + (from - center) * (1f - s) + Vector3.up * Mathf.Sin(s * Mathf.PI) * 0.25f;
                    yield return null;
                }
                for (int i = 0; i < ts.Count; i++) ts[i].position = target[i];
                AudioDirector.Sfx("hit", 0.35f);
                CameraShake.Kick(0.03f, 0.2f);
                yield return new WaitForSeconds(0.4f);
            }
        }

        /// <summary>Depois das peças no lugar: o portal se acende e a animação volta.</summary>
        public void Restore()
        {
            foreach (GameObject g in portalParts) if (g != null) g.SetActive(true);
            foreach (Animation a in GetComponentsInChildren<Animation>(true)) a.enabled = true;
            foreach (Animator a in GetComponentsInChildren<Animator>(true)) a.enabled = true;
            foreach (Light l in GetComponentsInChildren<Light>(true)) l.enabled = true;
            SetAnimationSpeed(1.2f);
            // abre uma vez e fica aberta (em loop o portal apagava e acendia de novo)
            if (legacyState != null) legacyState.wrapMode = WrapMode.ClampForever;
            SetRebuilt(true);
        }

        public void SetRebuilt(bool value)
        {
            rebuilt = value;
            if (value) foreach (Transform s in slots) s.gameObject.SetActive(true);
        }

        /// <summary>Centro visual da máquina no mundo (o meio do modelo; sem modelo, um ponto acima do chão).</summary>
        public Vector3 CenterWorld()
        {
            Bounds b = default(Bounds);
            bool first = true;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return first ? transform.position + Vector3.up * 1.8f : b.center + Vector3.up * (b.extents.y * 0.3f);
        }

        /// <summary>Os três fragmentos saem do jogador, sobem em arco até a máquina e giram cada vez mais rápido em volta do portal, que se acende</summary>
        public IEnumerator DockFragments(Vector3 fromWorld, float seconds)
        {
            Vector3 center = CenterWorld();
            Color gold = new Color(1f, 0.82f, 0.3f);
            var flying = new List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                GameObject d = ProcBuild.DiamondObject(null, "Fragmento em voo", gold, 0.28f);
                d.transform.position = fromWorld;
                flying.Add(d.transform);
            }

            var lightGo = new GameObject("BrilhoDaMaquina");
            lightGo.transform.position = center;
            Light glow = lightGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.45f, 0.92f, 1f);
            glow.range = 14f;
            glow.intensity = 0f;
            ParticleSystem sparks = Fx.NewSystem("FaiscasDaMaquina", center);
            var main = sparks.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.95f, 1f, 1f), new Color(1f, 0.9f, 0.5f, 1f));
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.2f;
            var em = sparks.emission;
            em.rateOverTime = 0f;
            var fade = sparks.colorOverLifetime;
            fade.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            fade.color = grad;
            sparks.Play();

            AudioDirector.Sfx("crackle", 0.8f);
            float orbitRadius = 1.35f;
            float t = 0f, angle = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / seconds);
                float fly = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.35f));
                float spin = 90f + 900f * k * k;
                angle += spin * Time.deltaTime;
                float collapse = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.82f) / 0.18f));
                float radius = Mathf.Lerp(orbitRadius, 0.05f, collapse);
                for (int i = 0; i < flying.Count; i++)
                {
                    float a = (angle + i * 120f) * Mathf.Deg2Rad;
                    Vector3 orbit = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a * 0.5f) * 0.35f * (1f - collapse), Mathf.Sin(a) * radius);
                    Vector3 start = Vector3.Lerp(fromWorld, orbit, fly) + Vector3.up * Mathf.Sin(fly * Mathf.PI) * (0.8f + i * 0.3f);
                    flying[i].position = fly < 1f ? start : orbit;
                    flying[i].Rotate(0f, 300f * Time.deltaTime, 0f, Space.World);
                    flying[i].localScale = Vector3.one * Mathf.Lerp(1f, 0.5f, collapse);
                }
                glow.intensity = Mathf.Lerp(0f, 9f, k) * (0.85f + 0.15f * Mathf.Sin(t * 30f));
                em.rateOverTime = Mathf.Lerp(0f, 140f, k);
                SetAnimationSpeed(Mathf.Lerp(0.4f, 2.6f, k));
                yield return null;
            }

            foreach (Transform f in flying) if (f != null) Destroy(f.gameObject);
            AudioDirector.Sfx("chime", 1f);
            Fx.Flash(this, center, new Color(0.7f, 1f, 1f), 24f, 18f, 0.6f);
            CameraShake.Kick(0.1f, 0.6f);
            em.rateOverTime = 0f;
            Destroy(sparks.gameObject, 2f);
            Destroy(lightGo);
            SetAnimationSpeed(1.2f);
            SetRebuilt(true);
        }
    }
}
