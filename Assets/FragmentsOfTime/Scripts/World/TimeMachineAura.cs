using System.Collections.Generic;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>A máquina do tempo "acorda" conforme o jogador chega perto (0 = longe, 1 = encostando).</summary>
    public class TimeMachineAura : MonoBehaviour
    {
        [Header("Alcance")]
        public bool autoScale = true;
        public float farDistance = 87f;
        public float nearDistance = 4f;
        public float curve = 1.6f;

        [Header("Tremor dos objetos")]
        public float shakeRadius = 170f;
        public int maxShakeObjects = 300;
        public float shakePosition = 0.14f;
        public float shakeRotation = 9f;
        public float shakeFrequency = 22f;
        public float shakeNearRadius = 22f;

        [Header("Câmera")]
        public float cameraShake = 0.07f;
        public bool driveCamera = true;
        public float shakeStartDistance = 135f;
        [Range(0f, 1f)] public float shakeAtFar = 0.06f;

        [Header("Feixes de luz")]
        public int beamCount = 18;
        public float beamMaxLength = 26f;
        public Color beamColor = new Color(0.45f, 0.92f, 1f, 1f);

        [Header("Holofotes")]
        public int spotCount = 3;
        public float spotIntensity = 24f;
        public float spotRange = 34f;
        public float spotAngle = 16f;

        Vector3 origin;
        Transform player;
        float triggerDistance;
        float k;
        float kv;

        class Shaker { public Transform t; public Vector3 pos; public Quaternion rot; public Vector3 center; public float weight, phase; public bool moved; }
        readonly List<Shaker> shakers = new List<Shaker>();
        class FlickerLight { public Light l; public float baseIntensity, phase; }
        readonly List<FlickerLight> lamps = new List<FlickerLight>();

        class Beam { public LineRenderer lr; public float yaw, pitch, speed, phase, length, width, wobble; }
        readonly List<Beam> beams = new List<Beam>();
        readonly List<Light> spots = new List<Light>();
        readonly List<float> spotPhase = new List<float>();
        Light pointGlow;
        SpriteRenderer glowCore, glowHalo;
        ParticleSystem sparks, dust;
        float nextSfx;
        AudioSource hum;
        Material spriteMat;
        Texture2D beamTex, dotTex;
        Transform fx;
        float baseYaw;

        /// <summary>Proximidade da máquina 0..1 (0 = longe, 1 = encostando).</summary>
        public float Proximity { get { return k; } }

        public void Begin(Vector3 portalPoint, Transform playerTransform, float trigger)
        {
            origin = portalPoint;
            player = playerTransform;
            triggerDistance = trigger;
            if (autoScale && player != null)
            {
                Vector3 d0 = origin - player.position; d0.y = 0f;
                float D0 = d0.magnitude;
                shakeStartDistance = D0 * 1.1f;
                farDistance = D0 * 0.45f;
                shakeRadius = Mathf.Max(shakeRadius, D0 * 1.3f);
                Debug.Log("[Aura] jogador começa a " + D0.ToString("F1") + " m da máquina: tremor desde " + shakeStartDistance.ToString("F0") + " m, chiado e tremor maior a " + farDistance.ToString("F0") + " m");
            }
            Build();
            Debug.Log("[Aura] iniciada. portal em " + origin.ToString("F1") + ", " + shakers.Count + " objetos tremem, " + lamps.Count + " luzes piscam, " + beams.Count + " feixes");
        }

        void Build()
        {
            fx = new GameObject("MachineAuraFX").transform;
            fx.position = origin;

            spriteMat = new Material(Shader.Find("Sprites/Default")) { name = "AuraSprite" };
            beamTex = MakeBeamTexture();
            dotTex = MakeDotTexture();

            Vector3 toPlayer = player != null ? player.position - origin : Vector3.back;
            toPlayer.y = 0f;
            baseYaw = toPlayer.sqrMagnitude > 1e-4f ? Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg : 180f;

            BuildHum();
            BuildBeams();
            BuildLights();
            BuildGlow();
            BuildParticles();
            CollectShakers();
            CollectLamps();
        }

        void BuildHum()
        {
            const int rate = 22050, secs = 4;
            var data = new float[rate * secs];
            float y = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                float lfo = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
                float v = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.55f + Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.22f;
                y += 0.05f * ((Random.value * 2f - 1f) - y);
                data[i] = (v * lfo + y * 0.5f) * 0.6f;
            }
            var clip = AudioClip.Create("AuraHum", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            var go = new GameObject("AuraHum");
            go.transform.SetParent(fx, false);
            hum = go.AddComponent<AudioSource>();
            hum.clip = clip; hum.loop = true; hum.volume = 0f;
            hum.spatialBlend = 1f; hum.minDistance = 8f; hum.maxDistance = 140f;
            hum.rolloffMode = AudioRolloffMode.Linear;
            hum.Play();
        }

        void BuildBeams()
        {
            var rng = new System.Random(11);
            float R() { return (float)rng.NextDouble(); }
            for (int i = 0; i < beamCount; i++)
            {
                var go = new GameObject("Feixe" + i);
                go.transform.SetParent(fx, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.alignment = LineAlignment.View;
                lr.textureMode = LineTextureMode.Stretch;
                lr.numCapVertices = 0;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                var mat = new Material(spriteMat);
                mat.mainTexture = beamTex;
                lr.sharedMaterial = mat;
                lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.2f, 0.55f), new Keyframe(1f, 1f));
                var b = new Beam
                {
                    lr = lr,
                    yaw = i < beamCount - 4 ? (R() - 0.5f) * 150f : R() * 360f - 180f,
                    pitch = -6f + R() * 34f,
                    speed = 0.25f + R() * 0.7f,
                    phase = R() * 100f,
                    length = 0.55f + R() * 0.45f,
                    width = 0.5f + R() * 1.1f,
                    wobble = 6f + R() * 14f,
                };
                beams.Add(b);
            }
        }

        void BuildLights()
        {
            var pg = new GameObject("AuraPointLight");
            pg.transform.SetParent(fx, false);
            pointGlow = pg.AddComponent<Light>();
            pointGlow.type = LightType.Point;
            pointGlow.color = new Color(0.4f, 0.9f, 1f);
            pointGlow.range = 20f;
            pointGlow.intensity = 0f;
            pointGlow.shadows = LightShadows.None;

            Color[] cols = { new Color(0.55f, 0.95f, 1f), new Color(0.75f, 0.6f, 1f), new Color(0.9f, 1f, 1f), new Color(0.4f, 0.8f, 1f) };
            for (int i = 0; i < spotCount; i++)
            {
                var g = new GameObject("AuraSpot" + i);
                g.transform.SetParent(fx, false);
                var l = g.AddComponent<Light>();
                l.type = LightType.Spot;
                l.color = cols[i % cols.Length];
                l.range = spotRange;
                l.spotAngle = spotAngle + i * 3f;
                l.innerSpotAngle = spotAngle * 0.4f;
                l.intensity = 0f;
                l.shadows = LightShadows.None;
                spots.Add(l);
                spotPhase.Add(i * 2.1f + 0.7f);
            }
        }

        void BuildGlow()
        {
            glowHalo = MakeGlow("GlowHalo", 0);
            glowCore = MakeGlow("GlowCore", 1);
        }

        SpriteRenderer MakeGlow(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(fx, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(dotTex, new Rect(0, 0, dotTex.width, dotTex.height), new Vector2(0.5f, 0.5f), dotTex.width / 2f);
            sr.sharedMaterial = spriteMat;
            sr.sortingOrder = order;
            sr.color = new Color(0.5f, 0.95f, 1f, 0f);
            return sr;
        }

        void BuildParticles()
        {
            sparks = NewParticles("AuraSparks", origin);
            var m = sparks.main;
            m.loop = true; m.playOnAwake = true;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.1f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.11f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.5f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 1f));
            m.gravityModifier = -0.02f;
            m.maxParticles = 600;
            var sh = sparks.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 2.2f;
            var em = sparks.emission; em.rateOverTime = 0f;
            var noise = sparks.noise; noise.enabled = true; noise.strength = 0.9f; noise.frequency = 0.6f; noise.scrollSpeed = 0.5f;
            var col = sparks.colorOverLifetime; col.enabled = true; col.color = FadeGradient();
            var sz = sparks.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.2f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f)));

            dust = NewParticles("AuraDust", origin + Vector3.up * 4.2f);
            var dm = dust.main;
            dm.loop = true; dm.playOnAwake = true;
            dm.simulationSpace = ParticleSystemSimulationSpace.World;
            dm.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            dm.startSpeed = 0f;
            dm.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.045f);
            dm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.68f, 0.52f, 0.55f), new Color(0.6f, 0.55f, 0.45f, 0.3f));
            dm.gravityModifier = 0.025f;
            dm.maxParticles = 900;
            var dsh = dust.shape;
            dsh.shapeType = ParticleSystemShapeType.Box; dsh.scale = new Vector3(18f, 0.2f, 18f);
            var dem = dust.emission; dem.rateOverTime = 0f;
            var dn = dust.noise; dn.enabled = true; dn.strength = 0.25f; dn.frequency = 0.4f;
            var dcol = dust.colorOverLifetime; dcol.enabled = true; dcol.color = FadeGradient();
        }

        ParticleSystem NewParticles(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(fx, true);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            var mat = new Material(spriteMat) { mainTexture = dotTex };
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        static Gradient FadeGradient()
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        void CollectShakers()
        {
            string[] skip = { "floor", "ceiling", "wall", "corridor", "pyramid", "sky", "terrain", "stairs", "aura", "papel", "folha", "barata" };
            var seen = new HashSet<Transform>();
            var candidates = new List<(Transform t, float d)>();
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || r.GetComponentInParent<Camera>(true) != null) continue;
                if (player != null && r.transform.IsChildOf(player)) continue;
                if (r.transform.IsChildOf(fx)) continue;
                if (r.GetComponentInParent<TimeMachine>() != null) continue;
                if (r.GetComponentInParent<Rigidbody>() != null) continue;
                float d = Vector3.Distance(r.bounds.center, origin);
                if (d > shakeRadius) continue;
                string n = r.name.ToLower();
                bool skipIt = false;
                foreach (string s in skip) if (n.Contains(s)) { skipIt = true; break; }
                if (skipIt || r.bounds.size.magnitude > 4.5f) continue;
                Transform root = ShakeRoot(r.transform);
                if (seen.Add(root)) candidates.Add((root, d));
            }
            candidates.Sort((a, b) => a.d.CompareTo(b.d));
            for (int i = 0; i < candidates.Count && shakers.Count < maxShakeObjects; i++)
            {
                var c = candidates[i];
                shakers.Add(new Shaker
                {
                    t = c.t, pos = c.t.localPosition, rot = c.t.localRotation, center = c.t.position,
                    weight = Mathf.Clamp(1.15f - c.d / shakeRadius, 0.25f, 1f),
                    phase = Random.value * 100f,
                });
            }
        }

        static Transform ShakeRoot(Transform t)
        {
            Transform cur = t;
            while (cur.parent != null)
            {
                Transform p = cur.parent;
                var rs = p.GetComponentsInChildren<Renderer>();
                if (rs.Length > 25) break;
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                if (b.size.magnitude > 4.5f) break;
                cur = p;
            }
            return cur;
        }

        void CollectLamps()
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (!l.enabled || l.transform.IsChildOf(fx)) continue;
                if (l.type == LightType.Directional) continue;
                if (player != null && l.transform.IsChildOf(player)) continue;
                string n = l.name.ToLower();
                if (n.Contains("flashlight") || n.Contains("armslight") || n.Contains("portalglow")) continue;
                if (l.GetComponentInParent<Camera>(true) != null) continue;
                if (Vector3.Distance(l.transform.position, origin) > shakeRadius) continue;
                lamps.Add(new FlickerLight { l = l, baseIntensity = l.intensity, phase = Random.value * 100f });
            }
        }

        void Update()
        {
            if (player == null) return;
            Vector3 d = origin - player.position; d.y = 0f;
            float t = Mathf.InverseLerp(farDistance, nearDistance, d.magnitude);
            k = Mathf.Pow(Mathf.Clamp01(t), curve);
            float dist = d.magnitude;
            kv = dist > farDistance
                ? shakeAtFar * Mathf.Clamp01(Mathf.InverseLerp(shakeStartDistance, farDistance, dist))
                : Mathf.Lerp(shakeAtFar, 1f, k);
            CameraSway.Stress = kv;
            if (driveCamera && d.magnitude > triggerDistance + 0.2f) CameraShake.Hold(cameraShake * kv);

            UpdateBeams();
            UpdateLights();
            UpdateGlow();
            UpdateParticles();
            UpdateSound();
        }

        void LateUpdate()
        {
            UpdateShake();
            UpdateLamps();
        }

        void UpdateBeams()
        {
            float time = Time.time;
            foreach (var b in beams)
            {
                float on = Mathf.Clamp01((k + 0.35f * kv - 0.06f - 0.45f * ((b.phase * 0.137f) % 1f)) / 0.2f);
                float flick = 0.65f + 0.35f * Mathf.PerlinNoise(time * 6f + b.phase, b.phase);
                float a = on * flick;
                if (a < 0.01f) { b.lr.enabled = false; continue; }
                b.lr.enabled = true;

                float yaw = baseYaw + b.yaw + Mathf.Sin(time * b.speed + b.phase) * b.wobble;
                float pitch = b.pitch + Mathf.Sin(time * b.speed * 0.7f + b.phase * 2f) * 5f;
                Vector3 dir = Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
                float len = Mathf.Lerp(2.5f, beamMaxLength, k) * b.length * (0.85f + 0.15f * Mathf.Sin(time * 1.3f + b.phase));
                len = ClipLength(dir, len);
                b.lr.SetPosition(0, origin);
                b.lr.SetPosition(1, origin + dir * len);
                b.lr.widthMultiplier = Mathf.Lerp(0.3f, 2.4f, k) * b.width * (0.8f + 0.4f * Mathf.PerlinNoise(time * 3f, b.phase));
                var c = beamColor; c.a = a * Mathf.Lerp(0.4f, 1f, k);
                b.lr.startColor = c;
                var e = c; e.a = 0f;
                b.lr.endColor = e;
            }
        }

        float ClipLength(Vector3 dir, float len)
        {
            var hits = Physics.RaycastAll(origin, dir, len, ~0, QueryTriggerInteraction.Ignore);
            float best = len;
            foreach (var h in hits)
            {
                if (h.distance < 1.5f) continue;
                if (h.collider.GetComponentInParent<TimeMachine>() != null) continue;
                if (player != null && h.collider.transform.IsChildOf(player)) continue;
                if (h.distance < best) best = h.distance;
            }
            return best;
        }

        void UpdateLights()
        {
            float time = Time.time;
            float jitter = 0.75f + 0.25f * Mathf.PerlinNoise(time * 11f, 3.1f);
            pointGlow.transform.position = origin;
            pointGlow.intensity = (0.8f + k * 9f) * kv * jitter;
            pointGlow.range = Mathf.Lerp(8f, 22f, k);

            for (int i = 0; i < spots.Count; i++)
            {
                var l = spots[i];
                float ph = spotPhase[i];
                float yaw = baseYaw + Mathf.Sin(time * (0.45f + 0.12f * i) + ph) * 80f + (i - 1) * 40f;
                float pitch = 4f + Mathf.Sin(time * 0.8f + ph) * 7f;
                l.transform.position = origin;
                l.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
                float on = Mathf.Clamp01((k + 0.25f * kv - 0.1f * (i + 1)) / 0.25f);
                l.intensity = on * spotIntensity * (0.7f + 0.3f * Mathf.PerlinNoise(time * 9f + ph, ph));
            }
        }

        void UpdateGlow()
        {
            if (glowCore == null) return;
            Camera cam = Camera.main;
            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * (3f + 6f * k));
            foreach (var sr in new[] { glowHalo, glowCore })
            {
                if (cam != null) sr.transform.rotation = cam.transform.rotation;
                sr.transform.position = origin;
            }
            glowHalo.transform.localScale = Vector3.one * Mathf.Lerp(4f, 20f, k) * pulse;
            glowCore.transform.localScale = Vector3.one * Mathf.Lerp(1.2f, 5f, k) * pulse;
            glowHalo.color = new Color(0.4f, 0.9f, 1f, Mathf.Clamp01(k + 0.15f * kv) * 0.6f);
            glowCore.color = new Color(0.9f, 1f, 1f, Mathf.Clamp01(k * 1.3f + 0.1f * kv) * 0.95f);
        }

        void UpdateParticles()
        {
            var se = sparks.emission; se.rateOverTime = kv * 160f;
            var sm = sparks.main; sm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f + k * 1.8f);
            float dustK = Mathf.Clamp01(kv * 1.1f);
            var de = dust.emission; de.rateOverTime = dustK * 240f;
        }

        void UpdateSound()
        {
            if (hum != null)
            {
                hum.volume = Mathf.Clamp01(kv * 1.15f);
                hum.pitch = Mathf.Lerp(0.75f, 1.9f, k);
            }
            if (AudioDirector.Instance != null && kv > 0.01f)
            {
                AudioDirector.Instance.Loop("aura_pulse", "heartbeat", Mathf.Lerp(0.15f, 0.9f, k));
                AudioDirector.Instance.Loop("aura_tense", "tense", Mathf.Lerp(0f, 0.55f, k));
            }
            if (kv < 0.05f || Time.time < nextSfx) return;
            nextSfx = Time.time + Mathf.Lerp(3.5f, 0.45f, k) * Random.Range(0.6f, 1.3f);
            if (AudioDirector.Instance == null) return;
            float vol = Mathf.Clamp01(0.35f + 0.65f * kv);
            if (Random.value < 0.5f) AudioDirector.Sfx("crackle", vol);
            else AudioDirector.Sfx("rumble", vol);
            if (k > 0.6f && Random.value < 0.35f) AudioDirector.Sfx("portal", 0.4f * k);
        }

        void UpdateShake()
        {
            float time = Time.time * shakeFrequency;
            Vector3 pp = player != null ? player.position : origin;
            foreach (var s in shakers)
            {
                if (s.t == null) continue;
                float near = Mathf.Clamp01(1f - Vector3.Distance(s.center, pp) / shakeNearRadius);
                near = near * near * (3f - 2f * near);
                float a = kv * near;
                if (a < 0.002f)
                {
                    if (s.moved) { s.t.localPosition = s.pos; s.t.localRotation = s.rot; s.moved = false; }
                    continue;
                }
                s.moved = true;
                Vector3 off = new Vector3(Noise(time, s.phase), Noise(time, s.phase + 17f), Noise(time, s.phase + 41f)) * (shakePosition * a);
                s.t.localPosition = s.pos + off;
                s.t.localRotation = Quaternion.Euler(new Vector3(Noise(time, s.phase + 5f), Noise(time, s.phase + 23f), Noise(time, s.phase + 59f)) * (shakeRotation * a)) * s.rot;
            }
        }

        static float Noise(float t, float seed) { return (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f; }

        void UpdateLamps()
        {
            float time = Time.time;
            foreach (var f in lamps)
            {
                if (f.l == null) continue;
                float n = Mathf.PerlinNoise(time * 9f + f.phase, f.phase);
                float dip = n < 0.3f + 0.3f * kv ? 1f - Mathf.Clamp01(kv * 1.1f) * 0.85f : 1f;
                f.l.intensity = f.baseIntensity * (kv > 0.01f ? dip : 1f);
            }
        }

        void OnDestroy()
        {
            foreach (var s in shakers) if (s.t != null) { s.t.localPosition = s.pos; s.t.localRotation = s.rot; }
            foreach (var f in lamps) if (f.l != null) f.l.intensity = f.baseIntensity;
            if (AudioDirector.Instance != null) { AudioDirector.Instance.StopLoop("aura_pulse"); AudioDirector.Instance.StopLoop("aura_tense"); }
            if (fx != null) Destroy(fx.gameObject);
        }

        static Texture2D MakeBeamTexture()
        {
            const int w = 8, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
            {
                float v = y / (h - 1f);
                float edge = Mathf.Sin(v * Mathf.PI);
                float a = Mathf.Pow(edge, 1.6f);
                for (int x = 0; x < w; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return tex;
        }

        static Texture2D MakeDotTexture()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return tex;
        }
    }
}
