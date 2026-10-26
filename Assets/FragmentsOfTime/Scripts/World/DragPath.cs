using System.Collections.Generic;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Acha o caminho andável (corredores, portas, escadas) entre o jogador e a máquina, medindo o mapa com física.</summary>
    public class DragPath
    {
        public const float Cell = 1f;
        const float MaxStep = 0.7f;
        const float CapsuleRadius = 0.3f;

        class Node { public Vector3 pos; public long parent; public bool hasParent; }

        readonly Dictionary<long, Node> nodes = new Dictionary<long, Node>();
        readonly Queue<long> queue = new Queue<long>();
        readonly Transform ignoreA, ignoreB;
        readonly Vector3 start;
        readonly RaycastHit[] rayBuf = new RaycastHit[12];
        readonly Collider[] colBuf = new Collider[24];
        int expanded;

        public bool Done { get; private set; }
        public bool Found { get; private set; }
        public List<Vector3> Path { get; private set; }
        public int Expanded { get { return expanded; } }

        public DragPath(Vector3 startPos, Vector3 goal, Transform ignorePlayer, Transform ignoreMachine)
        {
            start = startPos;
            ignoreA = ignorePlayer;
            ignoreB = ignoreMachine;

            for (float r = 4f; r <= 9f; r += 1.5f)
                for (int a = 0; a < 360; a += 15)
                {
                    float rad = a * Mathf.Deg2Rad;
                    Vector3 p = new Vector3(goal.x + Mathf.Cos(rad) * r, goal.y + 1.6f, goal.z + Mathf.Sin(rad) * r);
                    if (GroundAt(p, 3.2f, out Vector3 ground) && Clear(ground)) Seed(ground);
                }
            if (nodes.Count == 0) Done = true;
        }

        long Key(Vector3 p)
        {
            int ix = Mathf.RoundToInt(p.x / Cell), iz = Mathf.RoundToInt(p.z / Cell), iy = Mathf.RoundToInt(p.y / 1.2f);
            return ((long)(ix + 100000) << 40) | ((long)(iz + 100000) << 20) | (long)(iy + 1000);
        }

        void Seed(Vector3 ground)
        {
            long k = Key(ground);
            if (nodes.ContainsKey(k)) return;
            nodes[k] = new Node { pos = ground };
            queue.Enqueue(k);
        }

        bool GroundAt(Vector3 from, float depth, out Vector3 ground)
        {
            ground = default;
            int n = Physics.RaycastNonAlloc(from, Vector3.down, rayBuf, depth, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = rayBuf[i];
                if (h.normal.y < 0.55f) continue;
                if (ignoreA != null && h.transform.IsChildOf(ignoreA)) continue;
                if (h.point.y > best) { best = h.point.y; ground = h.point; }
            }
            return !float.IsNegativeInfinity(best);
        }

        bool Clear(Vector3 ground)
        {
            int n = Physics.OverlapCapsuleNonAlloc(ground + Vector3.up * 0.7f, ground + Vector3.up * 1.6f, CapsuleRadius, colBuf, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Transform t = colBuf[i].transform;
                if (ignoreA != null && t.IsChildOf(ignoreA)) continue;
                if (ignoreB != null && t.IsChildOf(ignoreB)) continue;
                return false;
            }
            return true;
        }

        static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] Dz = { 0, 0, 1, -1, 1, -1, 1, -1 };

        /// <summary>Processa até 'budget' células.</summary>
        public void Step(int budget)
        {
            if (Done) return;
            while (budget-- > 0)
            {
                if (queue.Count == 0 || expanded > 160000) { Done = true; return; }
                long k = queue.Dequeue();
                Node cur = nodes[k];
                expanded++;

                for (int d = 0; d < 8; d++)
                {
                    Vector3 np = new Vector3(cur.pos.x + Dx[d] * Cell, cur.pos.y, cur.pos.z + Dz[d] * Cell);
                    if (!GroundAt(np + Vector3.up * 1.2f, 2.6f, out Vector3 g)) continue;
                    if (Mathf.Abs(g.y - cur.pos.y) > MaxStep) continue;
                    long nk = Key(g);
                    if (nodes.ContainsKey(nk)) continue;
                    if (!Clear(g)) continue;
                    if (d >= 4)
                    {
                        Vector3 a = new Vector3(cur.pos.x + Dx[d] * Cell, cur.pos.y, cur.pos.z);
                        Vector3 b = new Vector3(cur.pos.x, cur.pos.y, cur.pos.z + Dz[d] * Cell);
                        if (!GroundAt(a + Vector3.up * 1.2f, 2.6f, out Vector3 ga) || !Clear(ga)) continue;
                        if (!GroundAt(b + Vector3.up * 1.2f, 2.6f, out Vector3 gb) || !Clear(gb)) continue;
                    }
                    nodes[nk] = new Node { pos = g, parent = k, hasParent = true };
                    queue.Enqueue(nk);

                    Vector3 df = g - start; df.y = 0f;
                    if (df.magnitude < 1.5f && Mathf.Abs(g.y - start.y) < 1.2f) { Finish(nk); return; }
                }
            }
        }

        void Finish(long startKey)
        {
            Path = new List<Vector3>();
            long k = startKey;
            int guard = 0;
            while (guard++ < 100000)
            {
                Node n = nodes[k];
                Path.Add(n.pos);
                if (!n.hasParent) break;
                k = n.parent;
            }
            Found = Path.Count > 1;
            Done = true;
        }
    }
}
