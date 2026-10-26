using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Soco procedural nos braços em primeira pessoa: fecha os dedos, gira o pulso e empurra o braço para frente.</summary>
    public class PunchAnimator : MonoBehaviour
    {
        [Header("Soco")]
        public float duration = 0.45f;
        public float sweepAngle = 40f;
        public float windupAngle = 22f;
        public float thrust = 0.06f;
        public float pullBack = 0.02f;
        public float wristTwist = 25f;
        public float fingerCurlScale = 1f;
        public float otherHandCurl = 0.3f;

        [Header("Lanterna na mão direita")]
        public float holdCurl = 0.85f;
        public float heldLengthInPalms = 1.7f;
        public float gripAlongHandle = 0.62f;
        public float wristYaw = 45f;
        public float heldTilt = 65f;
        public Vector3 heldScreenOffset = new Vector3(-0.04f, 0f, 0f);
        public float beamForward = 0.02f;
        public Vector3 heldOffset;
        public Vector3 heldEuler;

        class Finger
        {
            public Transform[] bones;
            public Quaternion[] rest;
            public Vector3[] axisLocal;
            public float[] angle;
            public float holdSign = 1f;
        }

        class Hand
        {
            public Transform wrist, arm;
            public Quaternion wristRest;
            public Vector3 wristAxisLocal;
            public Vector3 armRestLocalPos;
            public Quaternion armRestRot;
            public Vector3 armAxisLocal;
            public Vector3 thrustLocal;
            public float side;
            public bool holding;
            public Transform elbow;
            public Quaternion elbowRest, elbowTalk, armTalk;
            public bool talkReady;
            public Vector3 talkShift;
            public Vector3 elbowAxisLocal;
            public List<Finger> fingers = new List<Finger>();
        }

        static readonly string[] FingerTokens = { "thumb", "index", "point", "middle", "ring", "pink", "pinky", "pinkie", "little", "finger" };
        static readonly float[] FullAngles = { 75f, 85f, 60f };

        readonly List<Hand> hands = new List<Hand>();
        AnimationCurve curlCurve, thrustCurve, twistCurve, sweepCurve;
        float t = -1f;
        Hand active;
        bool nextRight = true;
        Transform heldBeam, heldLeft;
        float talkTarget, talkAmt, talkEnergy, talkEnergySm;
        float panicT;
        bool flexLeft;
        float flexStart;
        public float flexHz = 2.6f;
        public float flexCurl = 0.95f;
        const float PanicDuration = 0.9f;
        Camera armCam;
        Transform held;
        bool daggerLoadout;
        bool lanternLeft;

        public bool Ready => hands.Count > 0;

        /// <summary>Fase 1: o jogador pegou a adaga do chão; a mão direita passa a segurá-la.</summary>
        public void GiveDagger()
        {
            daggerLoadout = true;
            Hand rightHand = hands.FirstOrDefault(h => h.side > 0f);
            if (rightHand != null) rightHand.holding = true;
        }

        void Awake()
        {
            curlCurve = Smooth(new Keyframe(0f, 0f), new Keyframe(0.09f, 1f), new Keyframe(0.27f, 1f), new Keyframe(0.43f, 0f));
            sweepCurve = Smooth(new Keyframe(0f, 0f), new Keyframe(0.10f, windupAngle), new Keyframe(0.23f, -sweepAngle), new Keyframe(0.30f, -sweepAngle), new Keyframe(0.45f, 0f));
            thrustCurve = Smooth(new Keyframe(0f, 0f), new Keyframe(0.10f, -pullBack), new Keyframe(0.20f, thrust), new Keyframe(0.30f, thrust), new Keyframe(0.45f, 0f));
            twistCurve = Smooth(new Keyframe(0f, 0f), new Keyframe(0.20f, 1f), new Keyframe(0.27f, 1f), new Keyframe(0.45f, 0f));
        }

        static AnimationCurve Smooth(params Keyframe[] k)
        {
            var c = new AnimationCurve(k);
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        void Start()
        {
            Camera cam = GetComponentInParent<Camera>();
            armCam = cam;
            Vector3 down = cam != null ? -cam.transform.up : Vector3.down;
            Vector3 forward = cam != null ? cam.transform.forward : transform.forward;

            Transform[] all = GetComponentsInChildren<Transform>(true);
            var fingerRoots = all.Where(IsFingerBone).Where(b => b.parent == null || !IsFingerBone(b.parent)).ToArray();

            foreach (Transform root in fingerRoots)
            {
                Transform wrist = root.parent;
                if (wrist == null) continue;
                Hand h = hands.FirstOrDefault(x => x.wrist == wrist);
                if (h == null)
                {
                    h = new Hand { wrist = wrist };
                    h.arm = FindArm(wrist, all);
                    h.side = SideOf(wrist, cam);
                    hands.Add(h);
                }
                Finger f = BuildFinger(root, down, root.name.ToLower().Contains("thumb") ? 0.6f : 1f);
                if (f != null) h.fingers.Add(f);
            }

            foreach (Hand h in hands)
            {
                h.wristRest = h.wrist.localRotation;
                Vector3 toFingers = Vector3.zero;
                foreach (Finger f in h.fingers) toFingers += f.bones[0].position - h.wrist.position;
                if (toFingers.sqrMagnitude < 1e-8f) toFingers = forward;
                Vector3 twistAxisW = toFingers.normalized;
                h.wristAxisLocal = h.wrist.parent != null ? Quaternion.Inverse(h.wrist.parent.rotation) * twistAxisW : twistAxisW;

                Transform arm = h.arm != null ? h.arm : h.wrist;
                h.arm = arm;
                h.armRestLocalPos = arm.localPosition;
                h.armRestRot = arm.localRotation;
                Vector3 up = cam != null ? cam.transform.up : Vector3.up;
                h.armAxisLocal = arm.parent != null ? Quaternion.Inverse(arm.parent.rotation) * up : up;
                h.thrustLocal = arm.parent != null ? arm.parent.InverseTransformVector(forward) : forward;
            }

            if (cam != null)
                foreach (Transform c in cam.transform)
                    if (c.name == "armsgloved") { c.gameObject.SetActive(false); Debug.Log("[Punch] cópia solta dos braços escondida: " + c.name); }

            daggerLoadout = PlayerLoadout.DaggerPhase;
            lanternLeft = PlayerLoadout.LanternLeft;
            if (cam != null)
                foreach (Light l in cam.transform.root.GetComponentsInChildren<Light>(true))
                    if (l.name == "ArmsLight") l.enabled = false;
            FixEmptyArmMaterials();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Fase1")) NeutralizeArmColors();   // a manga vem com cor x1,9 e emissão: na Fase 1 (clara) ficava branca/cinza
            if (lanternLeft) FillArms();
            AttachLantern(cam);
            AttachWalkie(cam);
            if (daggerLoadout)
            {
                Hand rightHand = hands.FirstOrDefault(h => h.side > 0f);
                if (rightHand != null) rightHand.holding = true;
            }

            if (transform.parent != null)
            {
                var bob = transform.parent.GetComponent<ArmsWalkBob>();
                if (bob == null) bob = transform.parent.gameObject.AddComponent<ArmsWalkBob>();
                bob.beam = heldBeam;
                if (transform.parent.GetComponent<PlayerFootsteps>() == null) transform.parent.gameObject.AddComponent<PlayerFootsteps>();
                if (cam != null && cam.GetComponent<CameraSway>() == null) cam.gameObject.AddComponent<CameraSway>();
            }

            Debug.Log("[Punch] mãos: " + hands.Count + ", dedos: " + hands.Sum(h => h.fingers.Count) +
                      (hands.Count == 0 ? "  -> nenhum osso de dedo achado. Ossos: " + string.Join(", ", all.Select(a => a.name)) : ""));
        }

        /// <summary>Os materiais dos braços têm cor multiplicada por ~1,9 e emissão, ajustados para as fases escuras: na Fase 2, com a luz quente.</summary>
        void NeutralizeArmColors()
        {
            var tint = Color.white;
            Transform root = transform.parent != null ? transform.parent : transform;
            foreach (SkinnedMeshRenderer r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (Material m in r.materials)
                {
                    if (m == null) continue;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
                    m.DisableKeyword("_EMISSION");
                    if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
                    if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
                    m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
                    if (m.HasProperty("_SpecColor")) m.SetColor("_SpecColor", Color.black);
                }
        }

        /// <summary>Fase 2: sem a luz de apoio dos braços, a luva e a adaga ficam pretas no labirinto escuro.</summary>
        public static void AddSelfFill(Material m, float k)
        {
            if (m == null || !m.HasProperty("_EmissionColor")) return;
            if (m.IsKeywordEnabled("_EMISSION") && m.GetColor("_EmissionColor").maxColorComponent >= k) return;
            Texture t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
            if (t == null && m.HasProperty("_MainTex")) t = m.GetTexture("_MainTex");
            if (t != null && m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", t);
            Color tint = Color.white;
            if (t == null && m.HasProperty("_BaseColor")) tint = m.GetColor("_BaseColor");
            m.SetColor("_EmissionColor", new Color(k * tint.r, k * tint.g, k * tint.b, 1f));
            m.EnableKeyword("_EMISSION");
        }

        /// <summary>O prefab do jogador deixa um dos materiais dos braços vazio (a manga fica rosa): usa o outro material do mesmo braço.</summary>
        void FixEmptyArmMaterials()
        {
            Transform root = transform.parent != null ? transform.parent : transform;
            foreach (SkinnedMeshRenderer r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                Material good = null;
                foreach (Material m in mats) if (m != null && m.shader != null && m.shader.isSupported) { good = m; break; }
                if (good == null) continue;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] == null || mats[i].shader == null || !mats[i].shader.isSupported) { mats[i] = good; changed = true; }
                if (changed) r.sharedMaterials = mats;
            }
        }

        void FillArms()
        {
            Transform root = transform.parent != null ? transform.parent : transform;
            foreach (SkinnedMeshRenderer r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (Material m in r.materials) AddSelfFill(m, ArmsFill);
        }

        const float ArmsFill = 0.3f;

        void AttachLantern(Camera cam)
        {
            Hand right = hands.FirstOrDefault(h => lanternLeft ? h.side < 0f : h.side > 0f);
            if (right == null || cam == null) return;
            Transform item = null;
            foreach (Transform t in cam.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLower();
                if (t.IsChildOf(transform)) continue;
                if (n.Contains("wawa") || n.Contains("lantern") || n.Contains("lanterna")) { item = t; break; }
            }
            if (item == null && lanternLeft)
            {
                GameObject prefab = Resources.Load<GameObject>("Lantern/wawa");
                if (prefab != null)
                {
                    GameObject go = Instantiate(prefab, cam.transform);
                    go.name = "wawa";
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    item = go.transform;
                }
                else Debug.LogWarning("[Punch] não achei Resources/Lantern/wawa (lanterna da Fase 2).");
            }
            if (item == null) return;
            foreach (Collider c in item.GetComponentsInChildren<Collider>()) Destroy(c);
            item.SetParent(right.wrist, true);

            if (wristYaw != 0f)
            {
                Quaternion keep = right.wrist.rotation;
                Vector3 up = cam.transform.up, f0 = cam.transform.forward;
                right.wrist.rotation = Quaternion.AngleAxis(wristYaw, up) * keep;
                float plusDot = Mathf.Abs(Vector3.Dot(CurlAxisWorld(right, cam), f0));
                right.wrist.rotation = Quaternion.AngleAxis(-wristYaw, up) * keep;
                float minusDot = Mathf.Abs(Vector3.Dot(CurlAxisWorld(right, cam), f0));
                right.wrist.rotation = Quaternion.AngleAxis(plusDot >= minusDot ? wristYaw : -wristYaw, up) * keep;
                right.wristRest = right.wrist.localRotation;
            }

            Vector3 roots = Vector3.zero;
            foreach (Finger f in right.fingers) roots += f.bones[0].position;
            Vector3 rootAvg = right.fingers.Count > 0 ? roots / right.fingers.Count : right.wrist.position;
            Vector3 palm = Vector3.Lerp(right.wrist.position, rootAvg, 0.8f);
            float palmLen = Vector3.Distance(right.wrist.position, rootAvg);

            var rends = item.GetComponentsInChildren<Renderer>();
            var mf = item.GetComponentInChildren<MeshFilter>();
            if (rends.Length == 0 || mf == null || mf.sharedMesh == null || palmLen < 1e-4f) { held = item; return; }

            Bounds before = CenterBounds(rends);
            float longest = Mathf.Max(before.size.x, before.size.y, before.size.z);
            if (longest > 1e-4f) item.localScale *= heldLengthInPalms * palmLen / longest;

            Bounds mb = mf.sharedMesh.bounds;
            int ax = mb.size.x >= mb.size.y && mb.size.x >= mb.size.z ? 0 : (mb.size.y >= mb.size.z ? 1 : 2);
            Vector3 axLocal = ax == 0 ? Vector3.right : (ax == 1 ? Vector3.up : Vector3.forward);
            float ext = mb.extents[ax];
            Vector3 gripLocal = mb.center + axLocal * ext * (2f * gripAlongHandle - 1f);

            Vector3 curlAxis = CurlAxisWorld(right, cam);
            Vector3 fwd = cam.transform.forward;
            float tr = heldTilt * Mathf.Deg2Rad;
            Vector3 headDir = (curlAxis * Mathf.Cos(tr) + fwd * Mathf.Sin(tr)).normalized;
            Vector3 headAlt = (-curlAxis * Mathf.Cos(tr) + fwd * Mathf.Sin(tr)).normalized;
            if (Vector3.Dot(headAlt, fwd) > Vector3.Dot(headDir, fwd)) headDir = headAlt;
            Vector3 handleWorld = mf.transform.TransformDirection(axLocal).normalized;
            item.rotation = Quaternion.FromToRotation(handleWorld, -headDir) * item.rotation;

            item.position += palm - mf.transform.TransformPoint(gripLocal);
            Vector3 screenShift = heldScreenOffset;
            if (lanternLeft) screenShift.x = -screenShift.x;
            item.position += cam.transform.TransformVector(screenShift);
            item.localPosition += heldOffset;
            item.localRotation *= Quaternion.Euler(heldEuler);
            held = item;
            if (lanternLeft) foreach (Renderer r in rends) foreach (Material m in r.materials) AddSelfFill(m, ArmsFill);
            right.holding = true;

            foreach (Renderer r in rends) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Light beam = null;
            foreach (Light l in cam.GetComponentsInChildren<Light>(true))
                if (l.name.ToLower().Contains("flashlight")) { beam = l; break; }
            if (beam != null)
            {
                Vector3 headWorld = -mf.transform.TransformDirection(axLocal).normalized;
                Vector3 headTip = mf.transform.TransformPoint(mb.center - axLocal * ext) + headWorld * beamForward;
                beam.transform.SetParent(item, true);
                beam.transform.position = headTip;
                beam.transform.rotation = Quaternion.LookRotation(headWorld, cam.transform.up);
                heldBeam = beam.transform;
                beam.type = LightType.Spot; beam.color = Color.white;
                beam.intensity = 20f; beam.range = 18.363949f; beam.spotAngle = 75.43539f; beam.innerSpotAngle = 21.80208f;
                beam.shadows = LightShadows.Hard;
                beam.enabled = true;
                if (!lanternLeft && beam.GetComponent<FlashlightDust>() == null) beam.gameObject.AddComponent<FlashlightDust>();
                Debug.Log("[Punch] luz '" + beam.name + "' presa à lanterna");
            }

            Bounds after = CenterBounds(rends);
            Debug.Log("[Punch] lanterna: lado maior " + longest.ToString("F3") + " -> " +
                      Mathf.Max(after.size.x, after.size.y, after.size.z).ToString("F3") + " m, palma " + palmLen.ToString("F3") + " m, eixo da malha " + ax);

            Vector3 grip = mf.transform.TransformPoint(gripLocal);
            foreach (Finger f in right.fingers)
            {
                float plus = TipDistance(f, grip, 1f), minus = TipDistance(f, grip, -1f);
                f.holdSign = minus < plus ? -1f : 1f;
            }
        }

        public Vector3 talkPoint = new Vector3(-0.03f, -0.125f, 0.30f);
        public float talkExtraBend = 3f;
        public float talkGestureAmount = 0.2f;
        public float walkieLengthInPalms = 1.7f;
        public float walkieUpTilt = 25f;
        public float walkieSpin = 0f;
        public Vector3 walkieScreenOffset;
        public Vector3 walkieOffset;
        public Vector3 walkieEuler;

        static bool IsWalkieName(string n)
        {
            n = n.ToLower();
            return n.Contains("krotkofal") || n.Contains("walkie") || n.Contains("talkie");
        }

        static Bounds LocalBounds(Transform root, Renderer[] rends)
        {
            bool first = true;
            Bounds b = new Bounds();
            foreach (Renderer r in rends)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 lp = root.InverseTransformPoint(r.transform.TransformPoint(c));
                    if (first) { b = new Bounds(lp, Vector3.zero); first = false; } else b.Encapsulate(lp);
                }
            }
            return b;
        }

        void AttachWalkie(Camera cam)
        {
            if (lanternLeft)
            {
                if (cam != null)
                    foreach (Transform w in cam.GetComponentsInChildren<Transform>(true))
                        if (!w.IsChildOf(transform) && IsWalkieName(w.name)) w.gameObject.SetActive(false);
                return;
            }

            Hand left = hands.FirstOrDefault(h => h.side < 0f);
            if (left == null || cam == null || left.fingers.Count == 0) return;

            var found = new List<Transform>();
            foreach (Transform t in cam.GetComponentsInChildren<Transform>(true))
                if (!t.IsChildOf(transform) && IsWalkieName(t.name) && !found.Any(f => t.IsChildOf(f))) found.Add(t);
            if (found.Count == 0) return;
            Transform item = found[0];
            for (int i = 1; i < found.Count; i++) Destroy(found[i].gameObject);

            foreach (Collider c in item.GetComponentsInChildren<Collider>()) Destroy(c);
            var rends = item.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            item.SetParent(left.wrist, true);
            foreach (Renderer r in rends) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Vector3 roots = Vector3.zero;
            foreach (Finger f in left.fingers) roots += f.bones[0].position;
            Vector3 rootAvg = roots / left.fingers.Count;
            Vector3 up = cam.transform.up, fwd = cam.transform.forward;

            Vector3 fingerDir = (rootAvg - left.wrist.position).normalized;
            Quaternion keep = left.wrist.rotation;
            left.wrist.rotation = Quaternion.AngleAxis(90f, fingerDir) * keep;
            float plus = Mathf.Abs(Vector3.Dot(CurlAxisWorld(left, cam), up));
            left.wrist.rotation = Quaternion.AngleAxis(-90f, fingerDir) * keep;
            float minus = Mathf.Abs(Vector3.Dot(CurlAxisWorld(left, cam), up));
            left.wrist.rotation = Quaternion.AngleAxis(plus >= minus ? 90f : -90f, fingerDir) * keep;
            left.wristRest = left.wrist.localRotation;

            roots = Vector3.zero;
            foreach (Finger f in left.fingers) roots += f.bones[0].position;
            rootAvg = roots / left.fingers.Count;
            Vector3 palm = Vector3.Lerp(left.wrist.position, rootAvg, 0.8f);
            float palmLen = Vector3.Distance(left.wrist.position, rootAvg);
            if (palmLen < 1e-4f) return;

            Renderer body = rends.OrderByDescending(r => r.bounds.size.x * r.bounds.size.y * r.bounds.size.z).First();
            Bounds lb = LocalBounds(item, rends);
            int ax = lb.size.x >= lb.size.y && lb.size.x >= lb.size.z ? 0 : (lb.size.y >= lb.size.z ? 1 : 2);
            Vector3 axLocal = ax == 0 ? Vector3.right : (ax == 1 ? Vector3.up : Vector3.forward);
            Vector3 bodyCenterLocal = item.InverseTransformPoint(body.bounds.center);
            float antennaSign = Vector3.Dot(lb.center - bodyCenterLocal, axLocal) >= 0f ? 1f : -1f;

            Bounds bb = body.bounds;
            float bodyLongest = Mathf.Max(bb.size.x, bb.size.y, bb.size.z);
            if (bodyLongest > 1e-5f) item.localScale *= walkieLengthInPalms * palmLen / bodyLongest;

            float tr = walkieUpTilt * Mathf.Deg2Rad;
            Vector3 want = (up * Mathf.Cos(tr) + fwd * Mathf.Sin(tr)).normalized;
            Vector3 antennaWorld = item.TransformDirection(axLocal * antennaSign).normalized;
            item.rotation = Quaternion.FromToRotation(antennaWorld, want) * item.rotation;
            item.rotation = Quaternion.AngleAxis(walkieSpin, want) * item.rotation;

            item.position += palm - body.bounds.center;
            item.position += cam.transform.TransformVector(walkieScreenOffset);
            item.localPosition += walkieOffset;
            item.localRotation *= Quaternion.Euler(walkieEuler);
            heldLeft = item;
            left.holding = true;

            Vector3 grip = body.bounds.center;
            foreach (Finger f in left.fingers)
            {
                float p = TipDistance(f, grip, 1f), m = TipDistance(f, grip, -1f);
                f.holdSign = m < p ? -1f : 1f;
            }
            ComputeTalkPose(left, cam, body);
            Debug.Log("[Punch] walkie-talkie '" + item.name + "' preso à mão esquerda (corpo " + bodyLongest.ToString("F3") + " m -> " +
                      (walkieLengthInPalms * palmLen).ToString("F3") + " m, eixo " + ax + ", antena " + (antennaSign > 0 ? "+" : "-") + ")");
        }

        public bool CanTalk { get { return heldLeft != null; } }

        Transform lastDropped, droppedParent;
        Vector3 droppedPos, droppedScale;
        Quaternion droppedRot;

        /// <summary>Devolve à mão esquerda o walkie que caiu: ele sobe do chão até a palma, os dedos se fecham e ele volta a servir para falar.</summary>
        public IEnumerator RetrieveWalkie(float seconds)
        {
            Hand left = hands.FirstOrDefault(h => h.side < 0f);
            if (lastDropped == null || heldLeft != null || left == null) yield break;
            Transform item = lastDropped;
            lastDropped = null;
            foreach (DroppedWalkie d in item.GetComponents<DroppedWalkie>()) Destroy(d);
            foreach (Collider c in item.GetComponents<Collider>()) c.enabled = false;
            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            item.SetParent(droppedParent, true);
            Vector3 p0 = item.localPosition, s0 = item.localScale;
            Quaternion r0 = item.localRotation;
            for (float k = 0f; k < 1f; k += Time.deltaTime / Mathf.Max(0.01f, seconds))
            {
                float s = Mathf.SmoothStep(0f, 1f, k);
                item.localPosition = Vector3.Lerp(p0, droppedPos, s);
                item.localRotation = Quaternion.Slerp(r0, droppedRot, s);
                item.localScale = Vector3.Lerp(s0, droppedScale, s);
                yield return null;
            }
            item.localPosition = droppedPos;
            item.localRotation = droppedRot;
            item.localScale = droppedScale;
            if (rb != null) Destroy(rb);
            foreach (Collider c in item.GetComponents<Collider>()) Destroy(c);
            heldLeft = item;
            left.holding = true;
            flexLeft = false;
        }

        /// <summary>O walkie escapa da mão esquerda no desespero: os dedos abrem, o braço dá um tranco e ele cai com física e barulho.</summary>
        public Transform DropWalkie()
        {
            if (heldLeft == null) return null;
            Hand left = hands.FirstOrDefault(h => h.side < 0f);
            Transform item = heldLeft;
            heldLeft = null;
            talkTarget = 0f;
            panicT = PanicDuration;
            flexLeft = true;
            flexStart = Time.time;
            if (left != null) left.holding = false;

            lastDropped = item; droppedParent = item.parent; droppedPos = item.localPosition; droppedRot = item.localRotation; droppedScale = item.localScale;
            Transform c = armCam != null ? armCam.transform : transform;
            Vector3 vel = c.TransformDirection(new Vector3(-0.9f, -0.6f, 0.5f)) + Vector3.up * 0.4f;
            Vector3 spin = new Vector3(Random.Range(-9f, 9f), Random.Range(-6f, 6f), Random.Range(-9f, 9f));
            Transform root = transform.root;
            DroppedWalkie.Drop(item, vel, spin, root.GetComponentsInChildren<Collider>());
            return item;
        }

        /// <summary>Liga/desliga o gesto de falar no rádio (a mão esquerda leva o walkie-talkie até perto da boca).</summary>
        public void SetTalking(bool on) { talkTarget = on ? 1f : 0f; }

        static float N(float t, float seed) { return (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f; }

        void TalkGestures(Hand h, float e)
        {
            float time = Time.time;
            talkEnergySm = Mathf.Lerp(talkEnergySm, talkEnergy, 1f - Mathf.Exp(-16f * Time.deltaTime));
            float g = talkGestureAmount * e;

            h.arm.localRotation *= Quaternion.Euler(N(time * 0.7f, 1.3f) * 3.2f * g, N(time * 0.55f, 4.1f) * 4.5f * g, N(time * 0.9f, 7.7f) * 2.4f * g);

            if (h.elbow != null)
            {
                float nod = talkEnergySm * 9f + N(time * 1.6f, 2.2f) * 3.5f;
                h.elbow.localRotation = Quaternion.AngleAxis(nod * g, h.elbowAxisLocal) * h.elbow.localRotation;
                h.elbow.localRotation *= Quaternion.Euler(N(time * 0.8f, 9.1f) * 2.5f * g, N(time * 0.65f, 5.3f) * 2.5f * g, 0f);
            }

            h.wrist.localRotation *= Quaternion.Euler(N(time * 1.1f, 3.9f) * 3.5f * g, N(time * 0.85f, 6.2f) * 3f * g, N(time * 1.4f, 8.8f) * 2.5f * g);

            foreach (Finger f in h.fingers)
                for (int i = 0; i < f.bones.Length; i++)
                    f.bones[i].localRotation = Quaternion.AngleAxis(f.angle[i] * 0.12f * talkEnergySm * g * f.holdSign, f.axisLocal[i]) * f.bones[i].localRotation;
        }

        void ComputeTalkPose(Hand left, Camera cam, Renderer body)
        {
            Transform arm = left.arm;
            if (arm == null || arm == left.wrist) return;
            Transform elbow = null;
            for (Transform q = left.wrist.parent; q != null && q != arm.parent; q = q.parent)
                if (q != arm && q.name.ToLower().Contains("elbow")) { elbow = q; break; }
            if (elbow == null && left.wrist.parent != arm) elbow = left.wrist.parent;

            Vector3 target = cam.transform.TransformPoint(talkPoint);
            Vector3 bodyLocal = left.wrist.InverseTransformPoint(body.bounds.center);
            Vector3 axis = cam.transform.right;
            Quaternion armRest = arm.localRotation;
            Quaternion elbowRest = elbow != null ? elbow.localRotation : Quaternion.identity;

            Vector3 upAxis = cam.transform.up;
            float best = float.MaxValue;
            int bestSign = 1, bestA = 0, bestE = 0, bestYaw = 0;
            for (int sgn = -1; sgn <= 1; sgn += 2)
                for (int aa = 0; aa <= 40; aa += 8)
                    for (int yaw = -40; yaw <= 40; yaw += 10)
                        for (int ea = 0; ea <= (elbow != null ? 130 : 0); ea += 6)
                        {
                            arm.localRotation = armRest;
                            if (elbow != null) elbow.localRotation = elbowRest;
                            arm.rotation = Quaternion.AngleAxis(yaw, upAxis) * Quaternion.AngleAxis(sgn * aa, axis) * arm.rotation;
                            if (elbow != null) elbow.rotation = Quaternion.AngleAxis(sgn * ea, axis) * elbow.rotation;
                            float d = (left.wrist.TransformPoint(bodyLocal) - target).sqrMagnitude;
                            if (d < best) { best = d; bestSign = sgn; bestA = aa; bestE = ea; bestYaw = yaw; }
                        }

            arm.localRotation = armRest;
            if (elbow != null) elbow.localRotation = elbowRest;
            arm.rotation = Quaternion.AngleAxis(bestYaw, upAxis) * Quaternion.AngleAxis(bestSign * bestA, axis) * arm.rotation;
            left.armTalk = arm.localRotation;
            if (elbow != null)
            {
                elbow.rotation = Quaternion.AngleAxis(bestSign * (bestE + talkExtraBend), axis) * elbow.rotation;
                left.elbowTalk = elbow.localRotation;
                left.elbowAxisLocal = elbow.parent != null ? Quaternion.Inverse(elbow.parent.rotation) * (bestSign * axis) : bestSign * axis;
            }

            Vector3 err = target - left.wrist.TransformPoint(bodyLocal);
            if (err.magnitude > 0.9f) err = err.normalized * 0.9f;
            left.talkShift = arm.parent != null ? arm.parent.InverseTransformVector(err) : err;

            arm.localRotation = armRest;
            if (elbow != null) elbow.localRotation = elbowRest;

            left.elbow = elbow;
            left.elbowRest = elbowRest;
            if (elbow == null) left.elbowTalk = elbowRest;
            left.talkReady = true;
            Debug.Log("[Punch] gesto de falar: braço " + (bestSign * bestA) + "/" + bestYaw + " graus, cotovelo " + (bestSign * bestE) + " + " + talkExtraBend + " graus (" +
                      (elbow != null ? elbow.name : "sem cotovelo") + "), faltava " + Mathf.Sqrt(best).ToString("F3") + " m, braço deslocado " + err.magnitude.ToString("F3") + " m");
        }

        static Vector3 CurlAxisWorld(Hand h, Camera cam)
        {
            Vector3 axis = Vector3.zero;
            foreach (Finger f in h.fingers)
            {
                Transform par = f.bones[0].parent;
                Vector3 w = (par != null ? par.rotation : Quaternion.identity) * f.axisLocal[0];
                axis += Vector3.Dot(w, axis) < 0f ? -w : w;
            }
            return axis.sqrMagnitude < 1e-6f ? cam.transform.right : axis.normalized;
        }

        static Bounds CenterBounds(Renderer[] rends)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        float TipDistance(Finger f, Vector3 target, float sign)
        {
            for (int i = 0; i < f.bones.Length; i++)
                f.bones[i].localRotation = Quaternion.AngleAxis(f.angle[i] * holdCurl * sign, f.axisLocal[i]) * f.rest[i];
            Transform last = f.bones[f.bones.Length - 1];
            Vector3 tip = last.childCount > 0 ? last.GetChild(0).position : last.position;
            float d = (tip - target).magnitude;
            for (int i = 0; i < f.bones.Length; i++) f.bones[i].localRotation = f.rest[i];
            return d;
        }

        static bool IsFingerBone(Transform b)
        {
            string n = b.name.ToLower();
            if (n.Contains("pole") || n.Contains("goal") || n.Contains("ik")) return false;
            return FingerTokens.Any(n.Contains);
        }

        static Transform FindArm(Transform from, Transform[] all)
        {
            for (Transform p = from; p != null; p = p.parent)
            {
                string n = p.name.ToLower();
                if (n == "l_arm" || n == "r_arm" || n == "arm.l" || n == "arm.r" || n == "upperarm.l" || n == "upperarm.r")
                    return p;
            }
            return null;
        }

        static float SideOf(Transform wrist, Camera cam)
        {
            string n = wrist.name.ToLower();
            for (Transform p = wrist; p != null; p = p.parent)
            {
                string q = p.name.ToLower();
                if (q.StartsWith("l_") || q.StartsWith("left") || q.EndsWith(".l") || q.EndsWith("_l")) return -1f;
                if (q.StartsWith("r_") || q.StartsWith("right") || q.EndsWith(".r") || q.EndsWith("_r")) return 1f;
            }
            if (cam != null) return cam.transform.InverseTransformPoint(wrist.position).x < 0f ? -1f : 1f;
            return n.Contains("l") ? -1f : 1f;
        }

        Finger BuildFinger(Transform root, Vector3 down, float scale)
        {
            var bones = new List<Transform> { root };
            Transform cur = root;
            while (true)
            {
                Transform next = null;
                for (int i = 0; i < cur.childCount; i++)
                    if (IsFingerBone(cur.GetChild(i))) { next = cur.GetChild(i); break; }
                if (next == null) break;
                bones.Add(next);
                cur = next;
            }
            if (bones.Count == 1 && root.childCount > 0) bones.Add(root.GetChild(0));

            if (bones.Count < 2) return null;

            var dirs = new List<Vector3>();
            for (int i = 0; i < bones.Count - 1; i++) dirs.Add((bones[i + 1].position - bones[i].position).normalized);

            Vector3 axis = Vector3.zero;
            for (int i = 0; i < dirs.Count - 1; i++) axis += Vector3.Cross(dirs[i], dirs[i + 1]);
            if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(dirs[0], down);
            if (axis.sqrMagnitude < 1e-8f) return null;
            axis.Normalize();

            int n = bones.Count - 1;
            n = Mathf.Max(n, 1);
            var f = new Finger
            {
                bones = bones.Take(n).ToArray(),
                rest = new Quaternion[n],
                axisLocal = new Vector3[n],
                angle = new float[n],
            };
            for (int i = 0; i < n; i++)
            {
                Transform b = f.bones[i];
                f.rest[i] = b.localRotation;
                f.axisLocal[i] = b.parent != null ? Quaternion.Inverse(b.parent.rotation) * axis : axis;
                int fromTip = n - 1 - i;
                f.angle[i] = (fromTip < FullAngles.Length ? FullAngles[FullAngles.Length - 1 - fromTip] : 10f) * scale;
            }
            return f;
        }

        /// <summary>Dispara o soco (alterna esquerda/direita).</summary>
        public bool Play()
        {
            if (hands.Count == 0) return false;
            Hand next = lanternLeft
                ? (hands.FirstOrDefault(h => h.side > 0) ?? hands[0])
                : (hands.FirstOrDefault(h => h.side > 0 == nextRight) ?? hands[0]);
            if (!lanternLeft) nextRight = !nextRight;
            active = next;
            t = 0f;
            return true;
        }

        void LateUpdate()
        {
            talkAmt = Mathf.MoveTowards(talkAmt, talkTarget, Time.deltaTime * 2.2f);
            if (panicT > 0f) panicT = Mathf.Max(0f, panicT - Time.deltaTime);
            if (flexLeft && Time.time - flexStart > 2.5f) flexLeft = false;     // só treme a mão um instante, não para sempre
            bool idle = t < 0f;
            if (idle && held == null && heldLeft == null && !flexLeft) return;
            if (!idle) t += Time.deltaTime;
            float time = Mathf.Min(Mathf.Max(t, 0f), duration);
            bool done = idle || t >= duration;

            float curl = done ? 0f : curlCurve.Evaluate(time) * fingerCurlScale;
            float thr = done ? 0f : thrustCurve.Evaluate(time);
            float sweep = done ? 0f : sweepCurve.Evaluate(time);
            float twist = done ? 0f : twistCurve.Evaluate(time);

            foreach (Hand h in hands)
            {
                bool punching = h == active;
                float c = punching ? curl : curl * otherHandCurl;
                if (h.holding) c = Mathf.Max(c, holdCurl);
                if (flexLeft && h.side < 0f)
                {
                    float ft = Time.time - flexStart;
                    float osc = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * flexHz * ft + 0.7f * Mathf.Sin(2f * Mathf.PI * 0.55f * ft));
                    c = Mathf.Max(c, Mathf.Pow(osc, 0.8f) * flexCurl * Mathf.Clamp01((2.5f - ft) / 0.6f));
                }
                float push = punching ? thr : -0.5f * pullBack * curl;

                foreach (Finger f in h.fingers)
                    for (int i = 0; i < f.bones.Length; i++)
                        f.bones[i].localRotation = Quaternion.AngleAxis(f.angle[i] * c * (h.holding ? f.holdSign : 1f), f.axisLocal[i]) * f.rest[i];

                float tw = punching ? twist * wristTwist * h.side : 0f;
                h.wrist.localRotation = Quaternion.AngleAxis(tw, h.wristAxisLocal) * h.wristRest;
                h.arm.localPosition = h.armRestLocalPos + h.thrustLocal * push;
                float yaw = punching ? sweep * h.side : 0f;
                h.arm.localRotation = Quaternion.AngleAxis(yaw, h.armAxisLocal) * h.armRestRot;
                if (h.side < 0f && panicT > 0f)
                {
                    float ph = 1f - panicT / PanicDuration;
                    float s2 = Mathf.Sin(Mathf.Clamp01(ph) * Mathf.PI) * (1f - ph * 0.3f);
                    h.arm.localRotation *= Quaternion.Euler(-32f * s2, -16f * s2, 12f * s2);
                    if (h.elbow != null) h.elbow.localRotation *= Quaternion.Euler(0f, 0f, 24f * s2);
                }
                if (h.talkReady)
                {
                    float e = talkAmt * talkAmt * (3f - 2f * talkAmt);
                    h.arm.localPosition += h.talkShift * e;
                    if (h.elbow != null) h.elbow.localRotation = Quaternion.Slerp(h.elbowRest, h.elbowTalk, e);
                    h.arm.localRotation = Quaternion.Slerp(h.arm.localRotation, h.armTalk, e);

                    if (e > 0.001f) TalkGestures(h, e);
                }
            }

            if (done && !idle) { t = -1f; active = null; }
        }
    }
}
