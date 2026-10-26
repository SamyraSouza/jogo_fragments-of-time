using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Walkie-talkie que escapa da mão: vira um objeto com física (cai, quica e rola) e faz barulho de queda</summary>
    public class DroppedWalkie : MonoBehaviour
    {
        static AudioClip clatterClip;
        float lastSound = -1f;
        int sounds;

        public static DroppedWalkie Drop(Transform item, Vector3 velocity, Vector3 spin, Collider[] ignore)
        {
            item.SetParent(null, true);

            Vector3 center = Vector3.zero, size = Vector3.one * 0.05f;
            bool first = true;
            Bounds b = new Bounds();
            foreach (Renderer r in item.GetComponentsInChildren<Renderer>())
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 lp = item.InverseTransformPoint(r.transform.TransformPoint(c));
                    if (first) { b = new Bounds(lp, Vector3.zero); first = false; } else b.Encapsulate(lp);
                }
            }
            if (!first) { center = b.center; size = b.size; }
            var box = item.gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            var mat = new PhysicsMaterial("WalkieDrop") { bounciness = 0.35f, dynamicFriction = 0.5f, staticFriction = 0.5f };
            box.material = mat;

            var rb = item.gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.15f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = velocity;
            rb.angularVelocity = spin;

            if (ignore != null) foreach (Collider c in ignore) if (c != null) Physics.IgnoreCollision(box, c);

            var d = item.gameObject.AddComponent<DroppedWalkie>();
            return d;
        }

        void OnCollisionEnter(Collision c)
        {
            float v = c.relativeVelocity.magnitude;
            if (v < 0.35f || Time.time - lastSound < 0.08f || sounds > 14) return;
            lastSound = Time.time;
            sounds++;
            if (clatterClip == null) clatterClip = MakeClatter();
            var go = new GameObject("WalkieImpact");
            go.transform.position = c.GetContact(0).point;
            var a = go.AddComponent<AudioSource>();
            a.clip = clatterClip;
            a.spatialBlend = 0.7f;
            a.minDistance = 3f; a.maxDistance = 60f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.volume = Mathf.Clamp01(0.25f + v * 0.3f);
            a.pitch = Random.Range(0.88f, 1.12f) * (v > 2.5f ? 0.9f : 1.05f);
            a.Play();
            Destroy(go, clatterClip.length + 0.2f);
        }

        static AudioClip MakeClatter()
        {
            const int rate = 22050;
            int n = (int)(0.8f * rate);
            var d = new float[n];
            var rng = new System.Random(8);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float v = 0f;
                float f = Mathf.Lerp(120f, 55f, Mathf.Clamp01(t / 0.12f));
                v += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 28f) * 0.9f;
                v += ((float)(rng.NextDouble() * 2.0 - 1.0)) * Mathf.Exp(-t * 90f) * 0.8f;
                float[] at = { 0.09f, 0.17f, 0.26f };
                for (int k = 0; k < at.Length; k++)
                {
                    float lt = t - at[k];
                    if (lt >= 0f && lt < 0.07f)
                        v += Mathf.Sin(2f * Mathf.PI * (1800f + 600f * k) * lt) * Mathf.Exp(-lt * 70f) * (0.35f - 0.08f * k)
                           + ((float)(rng.NextDouble() * 2.0 - 1.0)) * Mathf.Exp(-lt * 60f) * 0.25f;
                }
                d[i] = Mathf.Clamp(v, -1f, 1f);
            }
            var clip = AudioClip.Create("WalkieClatter", n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
