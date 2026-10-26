using System.Collections;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>
    /// Múmia da Fase 2 (labirinto e câmara): modelo Meshy rigado (Resources/MummyMeshy) sobre o comportamento das guardiãs.
    /// As animações vêm de arquivos em Assets/Resources/MummyMeshy, um clipe por arquivo:
    ///   Mummy_Attack.fbx (modelo + ataque duplo; obrigatório), e opcionais Mummy_Idle.fbx, Mummy_Walk.fbx, Mummy_Scream.fbx, Mummy_Death.fbx.
    /// Sem um clipe opcional, o estado usa a pose de guarda do ataque com balanço, inclinação e queda feitos por código.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(MummyGuardian))]
    public class MazeMummy : MonoBehaviour
    {
        const string Folder = "MummyMeshy/";
        const string ModelFile = Folder + "Mummy_Attack";
        const string TextureFile = Folder + "Mummy_Tex";

        [Tooltip("Por quantos segundos o caminho vermelho aparece no mapa depois que esta múmia cai.")]
        public float revealSeconds = 8f;
        [Tooltip("Altura da múmia em metros.")]
        public float height = 4.8f;
        [Tooltip("Vida mínima (as múmias da câmara zeram isso para cair com 3 golpes).")]
        public float minHealth = 220f;
        [Tooltip("Procura o caminho pelos corredores do labirinto (desligue para múmias em salas abertas, que vão em linha reta).")]
        public bool useMazePath = true;
        [Tooltip("Ao cair, mostra a rota até a saída no mapa do labirinto.")]
        public bool revealRoute = true;
        [Tooltip("Em que ponto (0 a 1) do clipe de ataque cai o primeiro golpe.")]
        [Range(0.1f, 0.9f)] public float firstHitAt = 0.35f;
        [Tooltip("Em que ponto (0 a 1) cai o segundo golpe do combo (0 = sem segundo golpe).")]
        [Range(0f, 0.95f)] public float secondHitAt = 0.72f;
        [Tooltip("Gira o modelo em torno do eixo Y se ele estiver virado para o lado errado.")]
        public float modelYaw = 0f;

        MummyGuardian mummy;
        Transform visual, mesh, hips;
        Transform toeL, toeR, footL, footR;
        float soleOffset;
        Transform upLegL, upLegR, legL, legR, spine, spine1, armL, armR, head;
        float walkPhase, growlTimer;
        Vector3 lastPos;
        Material material;
        Vector3 hipsStart, hipsRest;
        bool restSet;
        AnimationClip clipAttack, clipIdle, clipWalk, clipScream, clipDeath;
        float animT, lastHealth, hitFlash;
        bool fallen;

        MummyGuardian.State prev = MummyGuardian.State.Dormant;
        bool attacking, secondHitDone;
        float attackTime, attackSpeed;
        float screamTime;
        AudioSource voice;
        Transform playerT;

        void Awake()
        {
            // aguenta vários golpes (o valor guardado na cena era de 2 socos)
            var g0 = GetComponent<MummyGuardian>();
            if (g0 != null) g0.maxHealth = Mathf.Max(g0.maxHealth, minHealth);
            GameObject model = Resources.Load<GameObject>(ModelFile);
            Texture2D baseMap = Resources.Load<Texture2D>(TextureFile);
            AnimationClip[] all = Resources.LoadAll<AnimationClip>(ModelFile);
            clipAttack = FirstClip(all);
            clipIdle = LoadClip("Mummy_Idle");
            clipWalk = LoadClip("Mummy_Walk");
            clipScream = LoadClip("Mummy_Scream");
            clipDeath = LoadClip("Mummy_Death");
            if (model != null) BuildVisual(model, baseMap);
            else Debug.LogWarning("[MazeMummy] não achei Assets/Resources/" + ModelFile + ".fbx");
        }

        // clipes que vêm de arquivos "sem pele" (só esqueleto + animação): guardam o arquivo de onde saíram
        readonly System.Collections.Generic.Dictionary<AnimationClip, string> clipFile = new System.Collections.Generic.Dictionary<AnimationClip, string>();

        AnimationClip LoadClip(string file)
        {
            AnimationClip c = FirstClip(Resources.LoadAll<AnimationClip>(Folder + file));
            if (c != null) clipFile[c] = file;
            return c;
        }

        static AnimationClip FirstClip(AnimationClip[] clips)
        {
            foreach (AnimationClip c in clips)
                if (c != null && !c.name.StartsWith("__preview__")) return c;
            return null;
        }

        void Start()
        {
            mummy = GetComponent<MummyGuardian>();
            lastHealth = mummy.Health;
            if (useMazePath) mummy.Pathfinder = Pathfind;
            mummy.Died += OnDied;

            if (clipAttack != null)
            {
                // o clipe inteiro cobre: preparação até o 1º golpe (windup) + golpe + recuperação
                attackSpeed = clipAttack.length * firstHitAt / Mathf.Max(0.1f, mummy.windupTime);
                float total = clipAttack.length / attackSpeed;
                mummy.recoverTime = Mathf.Max(mummy.recoverTime, total - mummy.windupTime - 0.35f + 0.2f);
            }

            lastPos = transform.position;
            var ccm = GetComponent<CharacterController>();
            if (ccm != null) ccm.radius = 0.85f;
            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 1f; voice.minDistance = 3f; voice.maxDistance = 30f; voice.playOnAwake = false;
        }

        void OnDestroy()
        {
            if (mummy != null) mummy.Died -= OnDied;
        }

        void BuildVisual(GameObject model, Texture2D baseMap)
        {
            visual = new GameObject("Model").transform;
            visual.SetParent(transform, false);

            GameObject g = Instantiate(model, visual);
            g.name = "Mesh";
            mesh = g.transform;
            foreach (Collider c in g.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (Animator a in g.GetComponentsInChildren<Animator>()) Destroy(a);
            foreach (Animation a in g.GetComponentsInChildren<Animation>()) Destroy(a);
            g.transform.localRotation = Quaternion.Euler(0f, modelYaw, 0f);

            foreach (Transform t in g.GetComponentsInChildren<Transform>())
                if (t.name.EndsWith("Hips")) { hips = t; break; }
            foreach (Transform t in g.GetComponentsInChildren<Transform>())
            {
                string n = t.name.Substring(t.name.IndexOf(':') + 1);
                switch (n)
                {
                    case "LeftUpLeg": upLegL = t; break;
                    case "RightUpLeg": upLegR = t; break;
                    case "LeftLeg": legL = t; break;
                    case "RightLeg": legR = t; break;
                    case "Spine": spine = t; break;
                    case "Spine1": spine1 = t; break;
                    case "LeftArm": armL = t; break;
                    case "RightArm": armR = t; break;
                    case "Head": head = t; break;
                    case "LeftToeBase": toeL = t; break;
                    case "RightToeBase": toeR = t; break;
                    case "LeftFoot": footL = t; break;
                    case "RightFoot": footR = t; break;
                }
            }

            // pose de repouso (bind) do modelo, antes de qualquer clipe: referência para trazer animações de outros arquivos
            foreach (Transform t in g.GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("mixamorig:")) myBones[t.name] = t;
            if (hips != null)
            {
                hipsBindPosMesh = mesh.InverseTransformPoint(hips.position);
                hipsBindRotMesh = Quaternion.Inverse(mesh.rotation) * hips.rotation;
            }
            myFrame = BindFrame(myBones, mesh);

            material = RotMaterials.Lit(Color.white);
            if (baseMap != null) { material.SetTexture("_BaseMap", baseMap); material.mainTexture = baseMap; }
            Renderer[] rs = g.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in rs)
            {
                var ms = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < ms.Length; i++) ms[i] = material;
                r.sharedMaterials = ms;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var sk = r as SkinnedMeshRenderer;
                if (sk != null) sk.updateWhenOffscreen = true;     // os ossos saem do volume original durante o ataque
            }

            SamplePose(clipIdle != null ? clipIdle : clipAttack, 0f);
            if (hips != null) hipsStart = hips.localPosition;

            allRenderers = rs;
            // escala pela altura da malha (bounds, como antes) e apoia a base no chão
            Bounds b = BoundsOf(rs);
            if (b.size.y > 0.0001f)
            {
                float scale = height / b.size.y;
                if (scale > 0.0001f && scale < 1000f) g.transform.localScale = Vector3.one * scale;
            }
            b = BoundsOf(rs);
            Debug.Log("[Múmia] altura do modelo " + b.size.y.ToString("F2") + " m (alvo " + height.ToString("F2") + ")");
            // centro do corpo = quadril (e não o centro da malha, que se desloca com os braços esticados): assim o corpo fica dentro da cápsula
            Vector3 center = hips != null ? hips.position : b.center;
            g.transform.position += new Vector3(transform.position.x - center.x, transform.position.y - b.min.y, transform.position.z - center.z);
            built = true;
            CaptureRest();
            soleOffset = Mathf.Max(0f, LowestFoot() - transform.position.y);     // altura do osso do pé acima da sola, na pose de guarda
        }

        float lastYaw, turnLean;
        bool built;
        Quaternion myFrame = Quaternion.identity;

        // orientação do esqueleto na pose de repouso: "para cima" = quadril → cabeça, "direita" = perna esquerda → direita
        static Quaternion BindFrame(System.Collections.Generic.Dictionary<string, Transform> bones, Transform root)
        {
            Transform h, hd, l, r;
            if (!bones.TryGetValue("mixamorig:Hips", out h) || !bones.TryGetValue("mixamorig:Head", out hd) ||
                !bones.TryGetValue("mixamorig:LeftUpLeg", out l) || !bones.TryGetValue("mixamorig:RightUpLeg", out r))
                return Quaternion.identity;
            Vector3 up = root.InverseTransformPoint(hd.position) - root.InverseTransformPoint(h.position);
            Vector3 right = root.InverseTransformPoint(r.position) - root.InverseTransformPoint(l.position);
            if (up.sqrMagnitude < 1e-8f || right.sqrMagnitude < 1e-8f) return Quaternion.identity;
            up.Normalize();
            right = Vector3.ProjectOnPlane(right, up);
            if (right.sqrMagnitude < 1e-8f) return Quaternion.identity;
            Vector3 fwd = Vector3.Cross(right.normalized, up);
            return Quaternion.LookRotation(fwd, up);
        }
        Vector3 hipsBindPosMesh, hipsBindVis;
        Quaternion hipsBindRotMesh = Quaternion.identity;
        readonly System.Collections.Generic.Dictionary<string, Transform> myBones = new System.Collections.Generic.Dictionary<string, Transform>();

        // posição do quadril (no espaço do corpo) na pose de guarda: o quadril fica presa a ela
        void CaptureRest()
        {
            if (clipAttack == null || hips == null) return;
            clipAttack.SampleAnimation(mesh.gameObject, 0f);
            hipsRest = visual.InverseTransformPoint(hips.position);
            hipsBindVis = visual.InverseTransformPoint(mesh.TransformPoint(hipsBindPosMesh));
            restSet = true;
        }

        // ---- animações de arquivos "sem pele": o esqueleto do arquivo é amostrado e as rotações dos ossos copiadas para o corpo
        class Donor
        {
            public GameObject go;
            public System.Collections.Generic.Dictionary<string, Transform> bones = new System.Collections.Generic.Dictionary<string, Transform>();
            public Transform hips;
            public Vector3 bindPos;
            public Quaternion bindRot;
            public Quaternion align = Quaternion.identity;      // gira o espaço do arquivo para o espaço do modelo
        }
        readonly System.Collections.Generic.Dictionary<AnimationClip, Donor> donors = new System.Collections.Generic.Dictionary<AnimationClip, Donor>();

        Donor GetDonor(AnimationClip clip)
        {
            Donor d;
            if (donors.TryGetValue(clip, out d)) return d;
            string file;
            if (clipFile.TryGetValue(clip, out file))
            {
                GameObject prefab = Resources.Load<GameObject>(Folder + file);
                if (prefab != null)
                {
                    d = new Donor();
                    d.go = Instantiate(prefab, visual);
                    d.go.name = "Doador_" + file;
                    foreach (Renderer r in d.go.GetComponentsInChildren<Renderer>()) r.enabled = false;
                    foreach (Animator a in d.go.GetComponentsInChildren<Animator>()) Destroy(a);
                    foreach (Transform t in d.go.GetComponentsInChildren<Transform>())
                        if (t.name.StartsWith("mixamorig:")) d.bones[t.name] = t;
                    d.bones.TryGetValue("mixamorig:Hips", out d.hips);
                    if (d.hips != null)
                    {
                        d.bindPos = d.go.transform.InverseTransformPoint(d.hips.position);
                        d.bindRot = Quaternion.Inverse(d.go.transform.rotation) * d.hips.rotation;
                        d.align = myFrame * Quaternion.Inverse(BindFrame(d.bones, d.go.transform));
                    }
                }
            }
            donors[clip] = d;
            return d;
        }

        // devolve false se não deu para usar o arquivo (aí cai no comportamento sem clipe)
        bool ApplyDonor(AnimationClip clip, float t, bool free)
        {
            Donor d = GetDonor(clip);
            if (d == null || d.hips == null || hips == null) return false;
            clip.SampleAnimation(d.go, Mathf.Clamp(t, 0f, clip.length));
            foreach (var kv in d.bones)
            {
                Transform mine;
                if (kv.Key == "mixamorig:Hips" || !myBones.TryGetValue(kv.Key, out mine)) continue;
                mine.localRotation = kv.Value.localRotation;
            }
            Quaternion cur = Quaternion.Inverse(d.go.transform.rotation) * d.hips.rotation;
            Quaternion dDonor = cur * Quaternion.Inverse(d.bindRot);
            hips.rotation = mesh.rotation * (d.align * dDonor * Quaternion.Inverse(d.align)) * hipsBindRotMesh;
            Vector3 dp = d.align * (d.go.transform.InverseTransformPoint(d.hips.position) - d.bindPos);
            float s = Mathf.Abs(d.bindPos.y) > 0.0001f ? Mathf.Abs(hipsBindPosMesh.y / d.bindPos.y) : 1f;
            Vector3 local = visual.InverseTransformPoint(mesh.TransformPoint(hipsBindPosMesh + dp * s));
            if (free) { local.x += hipsRest.x - hipsBindVis.x; local.z += hipsRest.z - hipsBindVis.z; }
            else { local.x = hipsRest.x; local.z = hipsRest.z; }
            hips.position = visual.TransformPoint(local);
            return true;
        }

        Renderer[] allRenderers;

        float LowestFoot()
        {
            float low = float.PositiveInfinity;
            foreach (Transform t in new[] { toeL, toeR, footL, footR })
                if (t != null) low = Mathf.Min(low, t.position.y);
            return low;
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

        // aplica o clipe no instante t, sem deixar o quadril andar para os lados (a múmia anda pelo CharacterController)
        void SamplePose(AnimationClip clip, float t)
        {
            if (clip == null || mesh == null) return;
            if (!built || hips == null || visual == null) { clip.SampleAnimation(mesh.gameObject, Mathf.Clamp(t, 0f, clip.length)); return; }
            if (!restSet) CaptureRest();

            // clipes de outros arquivos (andar, morrer): a morte pode deslocar o corpo, os demais ficam no lugar
            if (clip != clipAttack && ApplyDonor(clip, t, clip == clipDeath)) return;
            if (clip != clipAttack && clipAttack != null) clip = clipAttack;

            clip.SampleAnimation(mesh.gameObject, Mathf.Clamp(t, 0f, clip.length));
            // o clipe de ataque avança o corpo e, no fim, volta de uma vez: parecia um teleporte.
            // Trava o quadril, no espaço do corpo da múmia, no ponto da pose de guarda
            Vector3 cur = visual.InverseTransformPoint(hips.position);
            Vector3 delta = cur - hipsRest;
            delta.y = 0f;
            hips.position -= visual.TransformVector(delta);
        }

        AudioSource groan;
        AudioClip groanClip;
        float nextGroan;

        /// <summary>Gemido gutural, baixo, enquanto o jogador está por perto.</summary>
        void UpdateGroan()
        {
            if (mummy == null || mummy.IsDead || Time.time < nextGroan) return;
            if (playerT == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerT = p.transform;
            }
            if (playerT == null || (playerT.position - transform.position).sqrMagnitude > 12f * 12f) return;
            if (groanClip == null) groanClip = Resources.Load<AudioClip>("Sfx/zombie_groan");
            if (groanClip == null) { nextGroan = Time.time + 30f; return; }
            if (groan == null)
            {
                groan = gameObject.AddComponent<AudioSource>();
                groan.playOnAwake = false;
                groan.spatialBlend = 1f;
                groan.minDistance = 3f;
                groan.maxDistance = 16f;
                groan.volume = 0.3f;
            }
            groan.clip = groanClip;
            groan.Play();
            nextGroan = Time.time + groanClip.length + Random.Range(3f, 7f);
        }

        void Update()
        {
            UpdateGroan();
            if (visual == null || mummy == null || fallen) return;
            animT += Time.deltaTime;
            MummyGuardian.State st = mummy.Current;

            if (mummy.Health < lastHealth - 0.01f) hitFlash = 0.12f;
            lastHealth = mummy.Health;
            if (material != null)
            {
                hitFlash = Mathf.Max(0f, hitFlash - Time.deltaTime);
                RotMaterials.SetColor(material, Color.white);      // não fica vermelha ao apanhar
            }

            // transições
            if (st != prev)
            {
                if (st == MummyGuardian.State.Awakening) { screamTime = 0f; StartCoroutine(ScreamAfter(0.35f)); }
                if (st == MummyGuardian.State.Chase && prev == MummyGuardian.State.Dormant) StartCoroutine(ScreamAfter(0.1f));    // acordou direto para a perseguição
                if (st == MummyGuardian.State.Windup) { attacking = true; attackTime = 0f; secondHitDone = false; }
                if (st == MummyGuardian.State.Chase) attacking = false;
                prev = st;
            }

            float pitch = 0f, bob = 0f, sway = 0f, yaw = 0f;
            if (attacking && clipAttack != null)
            {
                attackTime += Time.deltaTime * attackSpeed;
                SamplePose(clipAttack, attackTime);
                if (attackTime >= clipAttack.length) attacking = false;
                else SecondHit();
            }
            else
            {
                switch (st)
                {
                    case MummyGuardian.State.Dormant:
                        if (clipIdle != null) SamplePose(clipIdle, Mathf.Repeat(animT, clipIdle.length));
                        else { SamplePose(clipAttack, 0f); pitch = 14f; }
                        break;
                    case MummyGuardian.State.Awakening:
                        screamTime += Time.deltaTime;
                        if (clipScream != null) SamplePose(clipScream, screamTime * clipScream.length / 1.2f);
                        else
                        {
                            // grito sem clipe: o corpo se ergue e a cabeça treme (usa a pose de braços levantados do ataque)
                            float k = Mathf.Clamp01(screamTime / 1.2f);
                            SamplePose(clipAttack, clipAttack != null ? clipAttack.length * 0.14f * Mathf.Sin(k * Mathf.PI) : 0f);
                            pitch = Mathf.Lerp(8f, 0f, Mathf.Sin(k * Mathf.PI));
                        }
                        break;
                    default:   // Chase, Recover (fora do ataque)
                        if (clipWalk != null && UseWalkFile)
                        {
                            float wt = Mathf.Repeat(animT * mummy.moveSpeed / 2.2f, clipWalk.length);
                            SamplePose(clipWalk, wt);
                            // anda balançando: o corpo gira e inclina de um lado para o outro a cada passada
                            float ph = wt / clipWalk.length * Mathf.PI * 2f;
                            yaw = Mathf.Sin(ph) * 9f;
                            sway = Mathf.Sin(ph + 1.2f) * 4f;
                            bob = Mathf.Abs(Mathf.Sin(ph)) * 0.03f;
                            pitch = 4f;
                        }
                        else
                        {
                            SamplePose(clipIdle != null ? clipIdle : clipAttack, clipIdle != null ? Mathf.Repeat(animT, clipIdle.length) : 0f);
                            WalkCycle(st == MummyGuardian.State.Chase);
                            bob = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.05f;
                            sway = Mathf.Sin(walkPhase) * 3f;
                            yaw = Mathf.Sin(walkPhase) * 8f;
                            pitch = 6f;
                        }
                        break;
                }
            }

            // nas curvas o corpo "acompanha com atraso": o tronco fica para trás e se inclina para dentro da curva
            float bodyYaw = transform.eulerAngles.y;
            float turnRate = Mathf.DeltaAngle(lastYaw, bodyYaw) / Mathf.Max(Time.deltaTime, 0.0001f);
            lastYaw = bodyYaw;
            turnLean = Mathf.Lerp(turnLean, Mathf.Clamp(turnRate * 0.12f, -22f, 22f), Time.deltaTime * 6f);
            if (!attacking && (st == MummyGuardian.State.Chase || st == MummyGuardian.State.Recover)) { yaw -= turnLean; sway -= turnLean * 0.25f; }
            Quaternion target = Quaternion.Euler(pitch, yaw, sway);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, target, Time.deltaTime * 12f);
            visual.localPosition = new Vector3(0f, bob, 0f);
            // mantém o pé mais baixo exatamente no chão em qualquer pose (agachada, golpe, passo)
            float low = LowestFoot();
            if (!float.IsPositiveInfinity(low))
                visual.position += Vector3.up * (transform.position.y + soleOffset - low);
        }

        // andar feito por código sobre a pose de guarda: pernas e braços balançam no ritmo da velocidade real,
        // o tronco se inclina e cada pisada faz um baque. Os eixos são os do mundo (a múmia inteira), não os dos ossos.
        // o arquivo de andar saía de lado no corpo; o andar agora é o feito por código (WalkCycle)
        const bool UseWalkFile = false;

        void WalkCycle(bool chasing)
        {
            float dt = Time.deltaTime;
            Vector3 d = transform.position - lastPos; d.y = 0f;
            lastPos = transform.position;
            float speed = dt > 0f ? Mathf.Min(d.magnitude / dt, 6f) : 0f;
            float amt = Mathf.Clamp01(speed / Mathf.Max(0.5f, mummy.moveSpeed));
            float prevPhase = walkPhase;
            walkPhase += speed * 2.3f * dt;      // ~1 passo a cada 1,4 m

            Vector3 right = transform.right;
            float swing = Mathf.Sin(walkPhase) * 34f * amt;
            Swing(upLegL, right, -swing);
            Swing(upLegR, right, swing);
            // joelho dobra quando a perna passa para a frente
            Swing(legL, right, Mathf.Max(0f, Mathf.Cos(walkPhase)) * 38f * amt);
            Swing(legR, right, Mathf.Max(0f, -Mathf.Cos(walkPhase)) * 38f * amt);
            // braços balançam pouco (pose de zumbi) e o tronco se arrasta para a frente
            Swing(armL, right, Mathf.Sin(walkPhase) * 9f * amt);
            Swing(armR, right, -Mathf.Sin(walkPhase) * 9f * amt);
            Swing(spine, right, 8f * amt);
            Swing(spine1, right, 6f * amt);
            Swing(head, right, -5f * amt + Mathf.Sin(walkPhase * 2f) * 3f * amt);
            if (spine1 != null) spine1.rotation = Quaternion.AngleAxis(Mathf.Sin(walkPhase) * 7f * amt, transform.up) * spine1.rotation;

            // pisada: a cada vez que o seno de duas vezes a fase cruza o zero
        }

        static void Swing(Transform bone, Vector3 axis, float degrees)
        {
            if (bone != null) bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }

        // o 2º golpe do combo: se o jogador ainda está ao alcance, leva dano de novo
        void SecondHit()
        {
            if (secondHitDone || secondHitAt <= 0f || attackTime < clipAttack.length * secondHitAt) return;
            secondHitDone = true;
            if (playerT == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerT = p.transform;
            }
            if (playerT == null) return;
            Vector3 to = playerT.position - transform.position; to.y = 0f;
            if (to.magnitude <= mummy.attackRange + 0.45f && Vector3.Dot(transform.forward, to.normalized) > 0.25f && LineClear())
            {
                PlayerHealth ph = playerT.GetComponent<PlayerHealth>();
                if (ph != null && !ph.IsDead)
                {
                    ph.TakeDamage(mummy.attackDamage * 0.8f);
                }
            }
        }

        IEnumerator ScreamAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (mummy != null && !mummy.IsDead) Scream();
        }

        bool LineClear()
        {
            RaycastHit h;
            if (!Physics.Linecast(transform.position + Vector3.up, playerT.position + Vector3.up, out h, ~0, QueryTriggerInteraction.Ignore)) return true;
            Transform t = h.collider.transform;
            return t.IsChildOf(playerT) || t.IsChildOf(transform) || playerT.IsChildOf(t);
        }

        // grito rouco ao ver o jogador, por cima do gemido das guardiãs
        void Scream()
        {
            if (voice == null) return;
            voice.pitch = Random.Range(0.95f, 1.05f);
            AudioClip shriek = Resources.Load<AudioClip>("Sfx/grito_mumia");
            voice.PlayOneShot(shriek != null ? shriek : MakeScream(), 0.45f);
        }

        static AudioClip screamClip;
        static AudioClip MakeScream()
        {
            if (screamClip != null) return screamClip;
            const int rate = 22050;
            int n = (int)(1.3f * rate);
            var d = new float[n];
            var rng = new System.Random(21);
            float lp = 0f, ph1 = 0f, ph2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Clamp01(t / 0.08f) * Mathf.Exp(-Mathf.Max(0f, t - 0.5f) * 2.6f);
                float f0 = Mathf.Lerp(260f, 150f, t / 1.3f) * (1f + 0.04f * Mathf.Sin(t * 38f));
                ph1 += 2f * Mathf.PI * f0 / rate;
                ph2 += 2f * Mathf.PI * (f0 * 2.01f) / rate;
                float saw = (ph1 / (2f * Mathf.PI) % 1f) * 2f - 1f;
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.35f * (w - lp);
                float v = saw * 0.55f + Mathf.Sin(ph2) * 0.2f + lp * 0.55f;
                d[i] = Mathf.Clamp(v * env * 0.9f, -1f, 1f);
            }
            screamClip = AudioClip.Create("GritoMumia", n, 1, rate, false);
            screamClip.SetData(d, 0);
            return screamClip;
        }

        IEnumerator Fall()
        {
            fallen = true;
            StartCoroutine(PinCorpse());
            if (visual == null) yield break;
            // cai de costas (é assim que a animação cai): vira o corpo para que as costas fiquem do lado com mais espaço livre
            // prefere morrer de frente para o jogador: cai de costas, para longe dele
            if (playerT == null)
            {
                GameObject pl = GameObject.FindGameObjectWithTag("Player");
                if (pl != null) playerT = pl.transform;
            }
            Vector3 backDir = -transform.forward;
            if (playerT != null)
            {
                Vector3 toP = playerT.position - transform.position; toP.y = 0f;
                if (toP.sqrMagnitude > 0.01f) backDir = -toP.normalized;
            }
            Vector3 bestDir = backDir; float bestDist = -1f;
            for (int i = 0; i < 16; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                RaycastHit hit;
                float d = Physics.Raycast(transform.position + Vector3.up * 1.2f, dir, out hit, 9f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 9f;
                if (Vector3.Dot(dir, backDir) > 0.9f) d += 4f;     // só desvia do jogador se não houver espaço atrás
                if (d > bestDist) { bestDist = d; bestDir = dir; }
            }
            Quaternion faceAway = Quaternion.LookRotation(-bestDir, Vector3.up);
            for (float tt = 0f; tt < 0.3f; tt += Time.deltaTime)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, faceAway, 0.25f);
                yield return null;
            }
            transform.rotation = faceAway;
            StartCoroutine(Thud(clipDeath != null ? clipDeath.length * 0.8f : 0.6f));
            if (clipDeath != null)
            {
                for (float t = 0f; t < clipDeath.length; t += Time.deltaTime)
                {
                    SamplePose(clipDeath, t);
                    SnapToGround();
                    yield return null;
                }
                SamplePose(clipDeath, clipDeath.length);
                SnapToGround();
                yield break;
            }
            // sem clipe de morte: desaba num movimento só (joelhos cedem, tronco tomba de costas, braços soltos) e fica deitada
            Quaternion from = visual.localRotation, to = Quaternion.Euler(-90f, 0f, 0f);
            Vector3 right = transform.right;
            const float Dur = 1.5f;
            for (float t = 0f; t < Dur; t += Time.deltaTime)
            {
                float u = t / Dur;
                float buckle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.55f));       // 1ª metade: as pernas cedem
                float fall = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.3f) / 0.7f)); // depois tomba de costas
                SamplePose(clipAttack, 0f);
                Swing(upLegL, right, -70f * buckle * (1f - fall * 0.8f)); Swing(upLegR, right, -55f * buckle * (1f - fall * 0.8f));
                Swing(legL, right, 100f * buckle * (1f - fall * 0.8f)); Swing(legR, right, 85f * buckle * (1f - fall * 0.8f));
                Swing(spine, right, 18f * buckle * (1f - fall)); Swing(head, right, 25f * buckle);
                Swing(armL, right, -35f * fall); Swing(armR, right, -50f * fall);
                visual.localRotation = Quaternion.Slerp(from, to, fall * fall);
                visual.localPosition = Vector3.zero;
                SnapToGround();
                yield return null;
            }
            visual.localRotation = to;
            visual.localPosition = Vector3.zero;
            SnapToGround();
        }

        // o cadáver fica onde caiu (nada o empurra nem o faz seguir o jogador)
        IEnumerator PinCorpse()
        {
            yield return null;
            Vector3 pos = transform.position;
            while (this != null)
            {
                transform.position = pos;
                yield return null;
            }
        }

        // encosta o ponto mais baixo da malha no chão (usado ao cair: deitada, o corpo tem espessura)
        void SnapToGround()
        {
            if (myBones.Count == 0) return;
            LevelShoulders();
            // osso mais baixo menos a espessura de cada parte (a cabeça é maior que o resto)
            float low = float.PositiveInfinity;
            foreach (var kv in myBones)
            {
                // os pés ficam de fora: deitada de costas é o tronco, a cabeça e os braços que apoiam no chão
                if (kv.Key.EndsWith("Foot") || kv.Key.EndsWith("ToeBase") || kv.Key.EndsWith("Toe_End")) continue;
                // o osso do joelho/coxa fica no meio da perna: precisa da espessura da carne, senão o joelho entra no chão
                bool leg = kv.Key.EndsWith("Leg") || kv.Key.EndsWith("UpLeg");
                low = Mathf.Min(low, kv.Value.position.y - height * (leg ? 0.085f : 0f));
            }
            visual.position += Vector3.up * (transform.position.y + 0.005f - low);
            LiftFeet();
        }

        // tronco e cabeça já estão no chão: se os pés afundaram, gira o corpo em torno da cabeça até eles saírem do chão
        void LiftFeet()
        {
            Transform l, r, hd, fl, fr;
            if (!myBones.TryGetValue("mixamorig:LeftShoulder", out l) || !myBones.TryGetValue("mixamorig:RightShoulder", out r) ||
                !myBones.TryGetValue("mixamorig:Head", out hd) || !myBones.TryGetValue("mixamorig:LeftFoot", out fl) ||
                !myBones.TryGetValue("mixamorig:RightFoot", out fr)) return;
            Transform foot = fl.position.y < fr.position.y ? fl : fr;
            float floor = transform.position.y;
            float sunk = floor + 0.005f - (foot.position.y - height * 0.055f);
            if (sunk <= 0.01f) return;
            Vector3 axis = r.position - l.position; axis.y = 0f;
            if (axis.sqrMagnitude < 0.01f) return;
            axis.Normalize();
            Vector3 pivot = hd.position;
            Vector3 v = foot.position - pivot;
            float dh = Mathf.Max(new Vector2(v.x, v.z).magnitude, 0.5f);
            float a = Mathf.Clamp(Mathf.Asin(Mathf.Clamp01(sunk / dh)) * Mathf.Rad2Deg, 0f, 60f);
            Quaternion keep = visual.rotation; Vector3 keepPos = visual.position;
            float before = foot.position.y;
            visual.RotateAround(pivot, axis, a);
            if (foot.position.y < before)
            {
                visual.rotation = keep; visual.position = keepPos;
                visual.RotateAround(pivot, axis, -a);
            }
        }

        // deitada, os dois ombros ficam na mesma altura (gira o corpo em torno do eixo quadril → cabeça)
        void LevelShoulders()
        {
            Transform l, r, h, hd;
            if (!myBones.TryGetValue("mixamorig:LeftShoulder", out l) || !myBones.TryGetValue("mixamorig:RightShoulder", out r) ||
                !myBones.TryGetValue("mixamorig:Hips", out h) || !myBones.TryGetValue("mixamorig:Head", out hd)) return;
            Vector3 axis = hd.position - h.position;
            if (axis.sqrMagnitude < 0.01f) return;
            axis.Normalize();
            Vector3 side = r.position - l.position;
            float dist = side.magnitude;
            if (dist < 0.01f) return;
            float a = Mathf.Asin(Mathf.Clamp(side.y / dist, -1f, 1f)) * Mathf.Rad2Deg;
            // só nivela quando o corpo está deitado (eixo do corpo mais horizontal que vertical)
            if (Mathf.Abs(axis.y) > 0.5f) return;
            Quaternion q = Quaternion.AngleAxis(a, axis);
            Vector3 pivot = h.position;
            Quaternion keep = visual.rotation; Vector3 keepPos = visual.position;
            visual.RotateAround(pivot, axis, a);
            if (Mathf.Abs((r.position - l.position).y) > Mathf.Abs(side.y))
            {
                visual.rotation = keep; visual.position = keepPos;
                visual.RotateAround(pivot, axis, -a);
            }
        }

        // o pé já está no chão: inclina o corpo em torno dele até a cabeça também descer ao chão

        // quanto a carne se estende abaixo do osso, por parte do corpo (margem MAIOR = corpo mais ALTO: o chão é o osso menos a margem)
        float Margin(string bone)
        {
            if (bone.EndsWith("Head") || bone.EndsWith("HeadTop_End") || bone.EndsWith("Neck")) return height * 0.0f;
            if (bone.EndsWith("Foot") || bone.EndsWith("ToeBase") || bone.EndsWith("Toe_End")) return height * 0.055f;
            return height * 0.03f;
        }

        Mesh bakeMesh;

        // o vértice mais baixo da malha já deformada pelo esqueleto (cabeça, mãos, tudo)

        // o corpo bate no chão
        IEnumerator Thud(float delay)
        {
            yield return new WaitForSeconds(delay);
            AudioDirector.Sfx("fall", 0.9f);
        }

        static Vector3 Pathfind(Vector3 from, Vector3 to)
        {
            LabyrinthMap map = LabyrinthMap.Instance;
            return map != null ? map.Waypoint(from, to) : to;
        }

        void OnDied(MummyGuardian m)
        {
            attacking = false;
            StartCoroutine(Fall());
            if (!revealRoute) return;

            LabyrinthMap map = LabyrinthMap.Instance;
            if (map == null) return;
            if (!map.Unlocked)
            {
                if (HUD.Instance != null) HUD.Instance.ShowSubtitle("A múmia caiu. Procure o papiro com o mapa do labirinto!", 4f);
                return;
            }
            map.RevealRoute(revealSeconds);
            if (HUD.Instance != null) HUD.Instance.ShowSubtitle("Múmia derrotada! O caminho apareceu no mapa.", 4f);
        }
    }
}
