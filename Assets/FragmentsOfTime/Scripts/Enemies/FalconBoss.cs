using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Falcão Guardião (chefe da Fase 3): parado até a luta, depois persegue e ataca em combo; cai quando a pedra o acerta.</summary>
    public class FalconBoss : MonoBehaviour
    {
        public const string DisplayName = "Falcão Guardião";

        public float height = 3.2f;
        public float chaseSpeed = 3.4f;
        public float reach = 3.2f;
        public float hitDamage = 12f;

        public bool Chasing { get; set; }
        public bool IsDead { get; private set; }

        // nos últimos segundos ele enlouquece: corre mais e se joga contra pilares e paredes
        bool frenzy, slamming, chargeHit;
        float nextSlam, staggerUntil;
        Vector3 slamTarget;
        static readonly Vector3[] SlamPoints =
        {
            new Vector3(58f, 0f, 6f), new Vector3(58f, 0f, 24f), new Vector3(68f, 0f, 6f), new Vector3(68f, 0f, 24f),
            new Vector3(79f, 0f, 15f), new Vector3(66f, 0f, 1f), new Vector3(66f, 0f, 29f)
        };

        public void StartFrenzy() { frenzy = true; nextSlam = Time.time + 0.8f; }

        public void Hide()
        {
            if (model != null) foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            if (body != null) body.enabled = false;
        }

        bool Frenzied()
        {
            if (Time.time < staggerUntil) { mode = Mode.Idle; return true; }
            if (slamming)
            {
                Vector3 d = slamTarget - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 400f * Time.deltaTime);
                bool moved = Step(d.normalized, 9.5f * Time.deltaTime);
                Vector3 toP = player.position - transform.position; toP.y = 0f;
                if (!chargeHit && toP.magnitude < reach * 0.8f) { chargeHit = true; TryHit(toP.normalized); }
                if (!moved || d.magnitude < 1.6f)
                {
                    slamming = false;
                    staggerUntil = Time.time + 0.8f;
                    nextSlam = Time.time + Random.Range(2f, 3.2f);
                    CameraShake.Kick(0.14f, 0.5f);
                    AudioDirector.Rec("Sfx/boulder_impact", 1f);
                    AudioDirector.Sfx("rumble", 0.5f);
                }
                return true;
            }
            if (Time.time >= nextSlam)
            {
                slamTarget = SlamPoints[Random.Range(0, SlamPoints.Length)];
                slamming = true;
                chargeHit = false;
                mode = Mode.Walk;
                return true;
            }
            return false;
        }

        AnimationClip walk, attack, die;
        GameObject model;
        Transform player;
        PlayerHealth playerHealth;

        enum Mode { Idle, Walk, Attack, Die }
        Mode mode = Mode.Idle;
        float clipTime;
        float attackTime, attackCooldownUntil;
        bool[] hitDone = new bool[3];
        static readonly float[] HitAt = { 0.28f, 0.5f, 0.78f };

        float groundY;
        CapsuleCollider body;

        // limites da sala do fragmento: ele não sai daqui nem cai do mapa
        const float MinX = 51.5f, MaxX = 78.5f, MinZ = 1.5f, MaxZ = 28.5f;

        /// <summary>Corpo sólido: o jogador não atravessa o chefe.</summary>
        void AddBody()
        {
            body = gameObject.AddComponent<CapsuleCollider>();
            body.direction = 1;
            body.radius = 0.9f;
            body.height = height;
            body.center = new Vector3(0f, height * 0.5f, 0f);
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        void KeepInRoom()
        {
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x, MinX, MaxX);
            p.z = Mathf.Clamp(p.z, MinZ, MaxZ);
            p.y = groundY;
            transform.position = p;
        }

        public static FalconBoss Spawn(Vector3 pos, Vector3 faceTowards, Transform playerT)
        {
            var go = new GameObject("FalconBoss");
            go.transform.position = pos;
            FalconBoss b = go.AddComponent<FalconBoss>();
            b.player = playerT;
            b.playerHealth = playerT != null ? playerT.GetComponent<PlayerHealth>() : null;
            b.groundY = pos.y;
            b.Build();
            b.AddBody();
            b.FaceInstant(faceTowards);
            return b;
        }

        static AnimationClip FirstClip(string path)
        {
            foreach (AnimationClip c in Resources.LoadAll<AnimationClip>(path))
                if (!c.name.StartsWith("__preview__")) return c;
            return null;
        }

        void Build()
        {
            GameObject prefab = Resources.Load<GameObject>("Boss/falcon_walk");
            walk = FirstClip("Boss/falcon_walk");
            attack = FirstClip("Boss/falcon_attack");
            die = FirstClip("Boss/falcon_die");
            if (prefab == null) { Debug.LogWarning("[FalconBoss] modelo Boss/falcon_walk não encontrado"); return; }

            model = Instantiate(prefab, transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            foreach (Animator a in model.GetComponentsInChildren<Animator>(true)) Destroy(a);
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);

            Texture2D tex = Resources.Load<Texture2D>("Boss/falcon_tex");
            Renderer[] rs = model.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in rs)
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = RotMaterials.Lit(Color.white);
                    if (tex != null) { m.mainTexture = tex; if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); }
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }

            Pose(walk, 0f);
            // altura e pés no chão (a escala do FBX varia)
            Bounds b = new Bounds(transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in rs) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            if (any && b.size.y > 0.01f)
            {
                float k = height / b.size.y;
                model.transform.localScale = Vector3.one * k;
                Pose(walk, 0f);
                b = rs[0].bounds;
                foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                model.transform.position += Vector3.up * (transform.position.y - b.min.y);
            }
        }

        void Pose(AnimationClip c, float t)
        {
            if (c == null || model == null) return;
            c.SampleAnimation(model, Mathf.Clamp(t, 0f, c.length));
        }

        public void FaceInstant(Vector3 p)
        {
            Vector3 d = p - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
        }

        void LateUpdate()
        {
            KeepInRoom();
            if (model == null || IsDead && mode == Mode.Die && die == null) return;
            switch (mode)
            {
                case Mode.Idle: Pose(walk, 0f); break;
                case Mode.Walk:
                    if (walk != null) { clipTime = Mathf.Repeat(clipTime + Time.deltaTime, Mathf.Max(0.1f, walk.length)); Pose(walk, clipTime); }
                    break;
                case Mode.Attack: Pose(attack, attackTime); break;
                case Mode.Die:
                    clipTime += Time.deltaTime;
                    Pose(die, clipTime);
                    break;
            }
        }

        AudioSource breath;

        /// <summary>Respiração pesada em loop enquanto ele persegue.</summary>
        void UpdateBreath()
        {
            bool want = Chasing && !IsDead;
            if (want && breath == null)
            {
                AudioClip clip = Resources.Load<AudioClip>("Boss/falcon_breath");
                if (clip == null) return;
                breath = gameObject.AddComponent<AudioSource>();
                breath.clip = clip;
                breath.loop = true;
                breath.spatialBlend = 0.6f;
                breath.minDistance = 6f;
                breath.maxDistance = 50f;
                breath.volume = 0.9f;
            }
            if (breath == null) return;
            if (want && !breath.isPlaying) breath.Play();
            else if (!want && breath.isPlaying) breath.Stop();
        }

        void Update()
        {
            UpdateBreath();
            if (IsDead || player == null || model == null) return;
            if (!Chasing) { mode = Mode.Idle; return; }

            Vector3 to = player.position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            Vector3 dir = dist > 0.01f ? to / dist : transform.forward;

            if (mode == Mode.Attack)
            {
                float prev = attackTime;
                attackTime += Time.deltaTime;
                float len = attack != null ? attack.length : 1.5f;
                if (attackTime < len * 0.6f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 120f * Time.deltaTime);
                for (int i = 0; i < HitAt.Length; i++)
                {
                    if (hitDone[i] || attackTime < HitAt[i] * len) continue;
                    hitDone[i] = true;
                    TryHit(dir);
                }
                if (attackTime >= len)
                {
                    mode = Mode.Walk;
                    clipTime = 0f;
                    attackCooldownUntil = Time.time + 0.7f;
                }
                return;
            }

            mode = Mode.Walk;
            if (frenzy && Frenzied()) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), (frenzy ? 360f : 240f) * Time.deltaTime);
            if (dist > reach * 0.8f) Step(dir, chaseSpeed * (frenzy ? 1.7f : 1f) * Time.deltaTime);
            else if (Time.time >= attackCooldownUntil)
            {
                mode = Mode.Attack;
                attackTime = 0f;
                for (int i = 0; i < hitDone.Length; i++) hitDone[i] = false;
            }
        }

        void TryHit(Vector3 dirToPlayer)
        {
            if (player == null) return;
            Vector3 to = player.position - transform.position; to.y = 0f;
            if (to.magnitude > reach || Vector3.Dot(transform.forward, to.normalized) < 0.2f) return;
            CameraShake.Kick(0.07f, 0.3f);
            AudioDirector.Rec("Sfx/boulder_impact", 0.9f);
            if (playerHealth != null)
            {
                if (playerHealth.Invulnerable) return;
                playerHealth.TakeDamage(hitDamage);
            }
            // o golpe atordoa: visão turva e passos mais lentos por um tempo
            if (HUD.Instance != null) HUD.Instance.Daze(3.5f);
            SimpleFPSController f = player.GetComponent<SimpleFPSController>();
            if (f != null) f.Slow(3.5f, 0.5f);
        }

        bool Step(Vector3 dir, float dist)
        {
            Vector3 origin = transform.position + Vector3.up * 1.2f;
            if (!Blocked(origin, dir, dist)) { transform.position += dir * dist; return true; }
            Vector3 dx = new Vector3(dir.x, 0f, 0f).normalized, dz = new Vector3(0f, 0f, dir.z).normalized;
            if (Mathf.Abs(dir.x) > 0.1f && !Blocked(origin, dx, dist)) { transform.position += dx * dist; return !slamming; }
            if (Mathf.Abs(dir.z) > 0.1f && !Blocked(origin, dz, dist)) { transform.position += dz * dist; return !slamming; }
            return false;
        }

        bool Blocked(Vector3 origin, Vector3 dir, float dist)
        {
            RaycastHit hit;
            if (!Physics.SphereCast(origin, 0.7f, dir, out hit, dist + 0.3f, ~0, QueryTriggerInteraction.Ignore)) return false;
            return !hit.transform.IsChildOf(player) && !hit.transform.IsChildOf(transform);
        }

        AudioSource voice;
        Coroutine growlStop;

        /// <summary>Ruge enquanto fala (seconds &gt; 0 corta o som nesse tempo; 0 toca inteiro).</summary>
        public void Growl(float seconds, float volume = 1f)
        {
            AudioClip clip = Resources.Load<AudioClip>("Boss/falcon_growl");
            if (clip == null) return;
            if (voice == null)
            {
                voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0.5f;
                voice.minDistance = 8f;
                voice.maxDistance = 60f;
            }
            if (growlStop != null) StopCoroutine(growlStop);
            voice.clip = clip;
            voice.volume = volume;
            voice.Play();
            if (seconds > 0f) growlStop = StartCoroutine(StopGrowl(seconds));
        }

        IEnumerator StopGrowl(float s)
        {
            yield return new WaitForSeconds(s);
            if (voice != null) voice.Stop();
        }

        public void Freeze() { Chasing = false; mode = Mode.Idle; }

        public void Die()
        {
            if (IsDead) return;
            IsDead = true;
            Chasing = false;
            mode = Mode.Die;
            clipTime = 0f;
            AudioDirector.Sfx("rumble", 0.8f);
        }
    }
}
