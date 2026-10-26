using System.Collections.Generic;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Helpers para montar objetos 3D simples com primitivas (múmias, máquina, fragmentos...).</summary>
    public static class ProcBuild
    {
        public static Transform Primitive(Transform parent, string name, PrimitiveType type, Vector3 localPos,
            Vector3 localScale, Material mat, bool keepCollider = false)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            if (!keepCollider)
            {
                Collider c = g.GetComponent<Collider>();
                if (c != null) Object.Destroy(c);
            }
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = localScale;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            return g.transform;
        }

        public static Transform Cube(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            return Primitive(parent, name, PrimitiveType.Cube, pos, scale, mat);
        }

        /// <summary>Anel de cubos no plano XY local (gira em torno de Z).</summary>
        public static Transform Ring(Transform parent, string name, float radius, int count, Vector3 segScale, Material mat, List<Renderer> collect = null)
        {
            var ring = new GameObject(name).transform;
            ring.SetParent(parent, false);
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                Transform seg = Primitive(ring, "seg" + i, PrimitiveType.Cube,
                    new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f), segScale, mat);
                seg.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                if (collect != null) collect.Add(seg.GetComponent<Renderer>());
            }
            return ring;
        }

        /// <summary>Círculo tracejado deitado no chão (marca onde as pedras vão cair).</summary>
        public static Transform DashedCircle(Transform parent, float radius, int segments, Material mat)
        {
            var root = new GameObject("DashedCircle").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float arc = radius * Mathf.PI * 2f / segments * 0.55f;
                Transform seg = Primitive(root, "dash" + i, PrimitiveType.Cube,
                    new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius),
                    new Vector3(0.12f, 0.02f, arc), mat);
                seg.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            }
            return root;
        }

        /// <summary>Malha de diamante (bipirâmide) usada nos fragmentos.</summary>
        public static Mesh Diamond(float halfHeight = 0.5f, float radius = 0.3f)
        {
            Vector3 top = new Vector3(0f, halfHeight, 0f);
            Vector3 bot = new Vector3(0f, -halfHeight, 0f);
            Vector3[] eq =
            {
                new Vector3(radius, 0f, 0f), new Vector3(0f, 0f, radius),
                new Vector3(-radius, 0f, 0f), new Vector3(0f, 0f, -radius)
            };
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = eq[i];
                Vector3 b = eq[(i + 1) % 4];
                int s = verts.Count;
                verts.Add(top); verts.Add(b); verts.Add(a);
                tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
                s = verts.Count;
                verts.Add(bot); verts.Add(a); verts.Add(b);
                tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
            }
            var mesh = new Mesh { name = "RoT_Diamond" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static GameObject DiamondObject(Transform parent, string name, Color color, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = Diamond();
            go.AddComponent<MeshRenderer>().sharedMaterial = RotMaterials.Unlit(color);
            return go;
        }
    }
}
