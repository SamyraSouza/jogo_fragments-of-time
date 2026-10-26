using System.Collections.Generic;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Monta o visual da Múmia Guardiã / Faraó com primitivas (não há modelo de múmia no projeto).</summary>
    public static class MummyBuilder
    {
        public class Rig
        {
            public Transform root, torso, head, armL, armR;
            public readonly List<Renderer> renderers = new List<Renderer>();
            public readonly List<Renderer> eyes = new List<Renderer>();
        }

        static readonly Color Wrap = new Color(0.80f, 0.72f, 0.55f);
        static readonly Color WrapDark = new Color(0.50f, 0.42f, 0.30f);
        static readonly Color Gold = new Color(0.92f, 0.74f, 0.22f);
        static readonly Color Blue = new Color(0.10f, 0.20f, 0.60f);
        static readonly Color Linen = new Color(0.92f, 0.9f, 0.82f);

        static Transform Part(Rig rig, Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Color c)
        {
            Transform t = ProcBuild.Primitive(parent, name, type, pos, scale, RotMaterials.Lit(c));
            rig.renderers.Add(t.GetComponent<Renderer>());
            return t;
        }

        public static Rig Build(Transform parent, bool pharaoh)
        {
            var rig = new Rig();
            rig.root = new GameObject("Model").transform;
            rig.root.SetParent(parent, false);
            Transform r = rig.root;

            Part(rig, r, "LegL", PrimitiveType.Cube, new Vector3(-0.15f, 0.45f, 0f), new Vector3(0.22f, 0.9f, 0.24f), Wrap);
            Part(rig, r, "LegR", PrimitiveType.Cube, new Vector3(0.15f, 0.45f, 0f), new Vector3(0.22f, 0.9f, 0.24f), Wrap);

            rig.torso = new GameObject("Torso").transform;
            rig.torso.SetParent(r, false);
            rig.torso.localPosition = new Vector3(0f, 0.9f, 0f);
            Transform tr = rig.torso;
            Part(rig, tr, "Chest", PrimitiveType.Cube, new Vector3(0f, 0.3f, 0f), new Vector3(0.56f, 0.7f, 0.32f), Wrap);
            for (int i = 0; i < 4; i++)
                Part(rig, tr, "Stripe" + i, PrimitiveType.Cube, new Vector3(0f, 0.08f + i * 0.17f, 0f), new Vector3(0.58f, 0.045f, 0.34f), WrapDark);

            rig.head = new GameObject("Head").transform;
            rig.head.SetParent(tr, false);
            rig.head.localPosition = new Vector3(0f, 0.82f, 0f);
            Part(rig, rig.head, "Skull", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.34f, 0.38f, 0.34f), Wrap);
            Part(rig, rig.head, "Band", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(0.35f, 0.05f, 0.35f), WrapDark);
            for (int i = -1; i <= 1; i += 2)
            {
                Transform eye = ProcBuild.Primitive(rig.head, "Eye", PrimitiveType.Sphere,
                    new Vector3(0.08f * i, 0.04f, 0.15f), Vector3.one * 0.06f, RotMaterials.Unlit(new Color(1f, 0.35f, 0.1f)));
                rig.eyes.Add(eye.GetComponent<Renderer>());
            }

            rig.armL = MakeArm(rig, tr, -1, Wrap);
            rig.armR = MakeArm(rig, tr, 1, Wrap);

            if (pharaoh)
            {
                Part(rig, rig.head, "Nemes", PrimitiveType.Cube, new Vector3(0f, 0.17f, -0.02f), new Vector3(0.42f, 0.18f, 0.40f), Blue);
                Part(rig, rig.head, "LappetL", PrimitiveType.Cube, new Vector3(-0.22f, -0.12f, 0f), new Vector3(0.10f, 0.45f, 0.26f), Blue);
                Part(rig, rig.head, "LappetR", PrimitiveType.Cube, new Vector3(0.22f, -0.12f, 0f), new Vector3(0.10f, 0.45f, 0.26f), Blue);
                Part(rig, rig.head, "Crown", PrimitiveType.Cube, new Vector3(0f, 0.30f, 0.12f), new Vector3(0.08f, 0.14f, 0.06f), Gold);
                Part(rig, tr, "Collar", PrimitiveType.Cube, new Vector3(0f, 0.58f, 0.02f), new Vector3(0.72f, 0.10f, 0.38f), Gold);
                Part(rig, r, "Belt", PrimitiveType.Cube, new Vector3(0f, 0.92f, 0f), new Vector3(0.58f, 0.08f, 0.34f), Gold);
                Part(rig, r, "Kilt", PrimitiveType.Cube, new Vector3(0f, 0.72f, 0f), new Vector3(0.60f, 0.34f, 0.34f), Linen);
            }

            return rig;
        }

        static Transform MakeArm(Rig rig, Transform torso, int side, Color c)
        {
            var pivot = new GameObject(side < 0 ? "ArmL" : "ArmR").transform;
            pivot.SetParent(torso, false);
            pivot.localPosition = new Vector3(0.37f * side, 0.55f, 0f);
            Part(rig, pivot, "Arm", PrimitiveType.Cube, new Vector3(0f, -0.3f, 0f), new Vector3(0.16f, 0.62f, 0.16f), c);
            Part(rig, pivot, "ArmWrap", PrimitiveType.Cube, new Vector3(0f, -0.38f, 0f), new Vector3(0.18f, 0.06f, 0.18f), WrapDark);
            return pivot;
        }
    }
}
