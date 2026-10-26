using System.Collections;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Efeitos visuais feitos por código (sem assets): marcas brilhantes no chão, poeira, estilhaços, clarão e onda de impacto.</summary>
    public static class Fx
    {
        static Texture2D soft, ring, disc;

        /// <summary>Bolinha com bordas macias (poeira, sombras, brilhos).</summary>
        public static Texture2D Soft()
        {
            if (soft != null) return soft;
            soft = Generate(64, d => { float a = Mathf.Clamp01(1f - d); return a * a * (3f - 2f * a); });
            return soft;
        }

        /// <summary>Anel fino e brilhante, com um halo macio por dentro.</summary>
        public static Texture2D Ring()
        {
            if (ring != null) return ring;
            ring = Generate(256, d =>
            {
                float core = Mathf.Clamp01(1f - Mathf.Abs(d - 0.94f) / 0.045f);
                float glow = Mathf.Clamp01(1f - Mathf.Abs(d - 0.94f) / 0.2f) * 0.35f;
                float inner = Mathf.Clamp01((d - 0.35f) / 0.6f) * (d < 0.94f ? 0.22f : 0f);
                return Mathf.Clamp01(core + glow + inner);
            });
            return ring;
        }

        /// <summary>Disco cheio com a borda macia.</summary>
        public static Texture2D Disc()
        {
            if (disc != null) return disc;
            disc = Generate(128, d => Mathf.Clamp01((1f - d) / 0.12f));
            return disc;
        }

        static Texture2D Generate(int size, System.Func<float, float> alphaByRadius)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, d > 1f ? 0f : alphaByRadius(d)));
                }
            t.Apply();
            return t;
        }

        public static Material SpriteMaterial(Texture2D tex, Color color)
        {
            var m = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex, color = color };
            return m;
        }

        /// <summary>Quadrado deitado no chão com a textura dada (anel, disco, sombra...).</summary>
        public static Transform FloorMark(Vector3 ground, float diameter, Texture2D tex, Color color, float lift = 0.03f)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "MarcaNoChao";
            Collider c = q.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
            q.transform.position = ground + Vector3.up * lift;
            q.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            q.transform.localScale = new Vector3(diameter, diameter, 1f);
            Renderer r = q.GetComponent<Renderer>();
            r.sharedMaterial = SpriteMaterial(tex, color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return q.transform;
        }

        public static void SetAlpha(Transform mark, float alpha)
        {
            if (mark == null) return;
            Renderer r = mark.GetComponent<Renderer>();
            if (r == null) return;
            Color c = r.sharedMaterial.color;
            c.a = alpha;
            r.sharedMaterial.color = c;
        }

        public static ParticleSystem NewSystem(string name, Vector3 position, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = SpriteMaterial(Soft(), Color.white);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static Gradient FadeOut(Color c, float peakAlpha)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.12f), new GradientAlphaKey(peakAlpha * 0.6f, 0.55f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        /// <summary>Poeira caindo do teto (aviso de que uma pedra vem aí).</summary>
        public static ParticleSystem CeilingDust(Vector3 ground, float radius, float height, Transform parent)
        {
            ParticleSystem ps = NewSystem("PoeiraDoTeto", ground + Vector3.up * height, parent);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            main.gravityModifier = 0.8f;
            main.maxParticles = 120;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.75f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var em = ps.emission;
            em.rateOverTime = 45f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeOut(new Color(0.78f, 0.68f, 0.52f), 0.8f);
            ps.Play();
            return ps;
        }

        /// <summary>Rastro de poeira atrás da pedra enquanto ela cai.</summary>
        public static ParticleSystem FallTrail(Transform follow)
        {
            ParticleSystem ps = NewSystem("RastroDaPedra", follow.position, follow);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.maxParticles = 200;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;
            var em = ps.emission;
            em.rateOverTime = 70f;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.5f)));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeOut(new Color(0.7f, 0.62f, 0.5f), 0.55f);
            ps.Play();
            return ps;
        }

        /// <summary>Nuvem de poeira que se abre em anel no chão, mais fagulhas laranja do impacto.</summary>
        public static void ImpactDust(Vector3 ground, float radius)
        {
            ParticleSystem ps = NewSystem("PoeiraDoImpacto", ground + Vector3.up * 0.1f);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 1.2f, radius * 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.03f;
            main.maxParticles = 160;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.25f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.y = new ParticleSystem.MinMaxCurve(0.2f, 1.1f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.dampen = 0.12f;
            drag.limit = 0.1f;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 70, 90) });
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(1f, 1.6f)));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeOut(new Color(0.72f, 0.62f, 0.48f), 0.6f);
            ps.Play();
            Object.Destroy(ps.gameObject, 5f);

            ParticleSystem sp = NewSystem("FagulhasDoImpacto", ground + Vector3.up * 0.15f);
            var smain = sp.main;
            smain.loop = false;
            smain.playOnAwake = false;
            smain.duration = 0.2f;
            smain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            smain.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.5f);
            smain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            smain.gravityModifier = 1.6f;
            smain.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.35f, 1f), new Color(1f, 0.4f, 0.15f, 1f));
            var sshape = sp.shape;
            sshape.shapeType = ParticleSystemShapeType.Hemisphere;
            sshape.radius = 0.3f;
            var sem = sp.emission;
            sem.rateOverTime = 0f;
            sem.SetBursts(new[] { new ParticleSystem.Burst(0f, 22, 30) });
            var scol = sp.colorOverLifetime;
            scol.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            scol.color = g;
            sp.Play();
            Object.Destroy(sp.gameObject, 3f);
        }

        /// <summary>Pedaços de pedra que saltam do ponto de impacto, quicam no chão e somem.</summary>
        public static void Debris(MonoBehaviour runner, Vector3 ground, int count, Material rockLook)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = "Estilhaco";
                Collider col = c.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
                float s = Random.Range(0.08f, 0.26f);
                c.transform.localScale = new Vector3(s, s * Random.Range(0.6f, 1f), s * Random.Range(0.7f, 1.2f));
                c.transform.position = ground + Vector3.up * 0.3f;
                c.transform.rotation = Random.rotation;
                Renderer r = c.GetComponent<Renderer>();
                r.sharedMaterial = rockLook != null ? rockLook : RotMaterials.Lit(new Color(0.5f, 0.43f, 0.33f));
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Vector2 dir = Random.insideUnitCircle.normalized;
                Vector3 vel = new Vector3(dir.x, 0f, dir.y) * Random.Range(1.8f, 5.2f) + Vector3.up * Random.Range(2.5f, 6.5f);
                runner.StartCoroutine(DebrisRoutine(c.transform, vel, ground.y));
            }
        }

        static IEnumerator DebrisRoutine(Transform t, Vector3 vel, float floorY)
        {
            Vector3 spin = Random.onUnitSphere * Random.Range(180f, 540f);
            float life = 0f, rest = 0f;
            while (t != null && life < 2.6f)
            {
                float dt = Time.deltaTime;
                life += dt;
                vel += Vector3.down * 16f * dt;
                t.position += vel * dt;
                t.Rotate(spin * dt, Space.World);
                if (t.position.y < floorY + 0.06f)
                {
                    Vector3 p = t.position;
                    p.y = floorY + 0.06f;
                    t.position = p;
                    vel.y = Mathf.Abs(vel.y) * 0.35f;
                    vel.x *= 0.6f; vel.z *= 0.6f;
                    spin *= 0.5f;
                    if (vel.y < 0.4f) rest += dt;
                }
                if (rest > 0.8f) break;
                yield return null;
            }
            if (t != null) Object.Destroy(t.gameObject);
        }

        /// <summary>Clarão alaranjado curto (luz de ponto que apaga sozinha).</summary>
        public static void Flash(MonoBehaviour runner, Vector3 pos, Color color, float intensity, float range, float seconds)
        {
            var go = new GameObject("ClaraoDoImpacto");
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            runner.StartCoroutine(FlashRoutine(l, intensity, seconds));
        }

        static IEnumerator FlashRoutine(Light l, float intensity, float seconds)
        {
            float t = 0f;
            while (t < seconds && l != null)
            {
                t += Time.deltaTime;
                l.intensity = intensity * Mathf.Pow(1f - t / seconds, 2f);
                yield return null;
            }
            if (l != null) Object.Destroy(l.gameObject);
        }

        /// <summary>Onda de choque: um anel que se abre no chão e some.</summary>
        public static IEnumerator Shockwave(Vector3 ground, float fromDiameter, float toDiameter, Color color, float seconds)
        {
            Transform m = FloorMark(ground, fromDiameter, Ring(), color, 0.05f);
            float t = 0f;
            while (t < seconds && m != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / seconds);
                float e = 1f - (1f - k) * (1f - k);
                float d = Mathf.Lerp(fromDiameter, toDiameter, e);
                m.localScale = new Vector3(d, d, 1f);
                SetAlpha(m, color.a * (1f - k));
                yield return null;
            }
            if (m != null) Object.Destroy(m.gameObject);
        }
    }
}
