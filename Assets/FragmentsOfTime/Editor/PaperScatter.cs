using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RiftOfTime.EditorTools
{
    /// <summary>Espalha folhas de papel (Assets/Movels/papers/source/Papers/Papers.fbx) pelo chão de uma sala.</summary>
    public class PaperScatter : EditorWindow
    {
        const string Model = "Assets/Movels/papers/source/Papers/Papers.fbx";
        const string TexDir = "Assets/Movels/papers/textures/";
        const string MatDir = "Assets/Movels/papers/";
        const string RootName = "PapeisEspalhados";

        int sheets = 90, piles = 14, seed = 7;
        float spread = 0.45f, sheetSize = 0.30f, margin = 0.35f;
        bool onlyFloor = true;

        static void Open() { GetWindow<PaperScatter>("Espalhar papéis"); }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Selecione o Office (ou o chão) na Hierarquia. Os papéis caem no chão dentro da área dele, sem entrar nos móveis.", MessageType.Info);
            sheets = EditorGUILayout.IntSlider("Folhas", sheets, 5, 400);
            piles = EditorGUILayout.IntSlider("Montinhos", piles, 1, 60);
            spread = EditorGUILayout.Slider("Espalhamento do monte (m)", spread, 0.1f, 1.5f);
            sheetSize = EditorGUILayout.Slider("Tamanho da folha (m)", sheetSize, 0.1f, 0.6f);
            margin = EditorGUILayout.Slider("Distância da parede (m)", margin, 0f, 1.5f);
            seed = EditorGUILayout.IntField("Semente (muda o sorteio)", seed);
            if (GUILayout.Button("Espalhar papéis")) Scatter();
            if (GUILayout.Button("Remover papéis")) Remove();
        }

        static GameObject FindArea()
        {
            if (Selection.activeGameObject != null) return Selection.activeGameObject;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (t.name.ToLower().Contains("office")) return t.gameObject;
            return null;
        }

        static void Remove()
        {
            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);
        }

        void Scatter()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if (model == null) { EditorUtility.DisplayDialog("Papéis", "Não achei " + Model, "OK"); return; }
            var area = FindArea();
            if (area == null) { EditorUtility.DisplayDialog("Papéis", "Selecione o Office na Hierarquia e tente de novo.", "OK"); return; }

            var oldRoot = GameObject.Find(RootName);
            var rends = area.GetComponentsInChildren<Renderer>();
            if (oldRoot != null) rends = rends.Where(r => !r.transform.IsChildOf(oldRoot.transform)).ToArray();
            if (rends.Length == 0) { EditorUtility.DisplayDialog("Papéis", "O objeto selecionado não tem malhas para medir a área.", "OK"); return; }
            Bounds area3 = rends[0].bounds;
            foreach (var r in rends) area3.Encapsulate(r.bounds);

            var mats = EnsureMaterials(model);
            Physics.SyncTransforms();
            if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot);

            var rng = new System.Random(seed);
            float Rand() => (float)rng.NextDouble();
            float Range(float a, float b) => a + (b - a) * Rand();

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Espalhar papéis");

            var centers = new List<Vector3>();
            for (int tries = 0; tries < piles * 40 && centers.Count < piles; tries++)
            {
                Vector3 p = new Vector3(Range(area3.min.x + margin, area3.max.x - margin), 0f, Range(area3.min.z + margin, area3.max.z - margin));
                if (TryFloor(p.x, p.z, area3, out Vector3 floor)) centers.Add(floor);
            }
            if (centers.Count == 0)
            {
                EditorUtility.DisplayDialog("Papéis", "Não achei chão livre. O Office precisa ter colliders (Generate Colliders no importador) e a área precisa estar na cena.", "OK");
                Undo.DestroyObjectImmediate(root);
                return;
            }

            var placed = new List<Vector3>();
            int count = 0;
            for (int i = 0; i < sheets * 6 && count < sheets; i++)
            {
                Vector3 c = centers[rng.Next(centers.Count)];
                float ang = Range(0f, Mathf.PI * 2f);
                float rad = Mathf.Abs(Range(-1f, 1f) * Range(0f, 1f)) * spread * 2f;
                float x = c.x + Mathf.Cos(ang) * rad, z = c.z + Mathf.Sin(ang) * rad;
                if (!TryFloor(x, z, area3, out Vector3 floor)) continue;
                if (Mathf.Abs(floor.y - c.y) > 0.15f) continue;

                int under = placed.Count(q => (new Vector2(q.x - x, q.z - z)).sqrMagnitude < (sheetSize * 0.45f) * (sheetSize * 0.45f));
                var sheet = MakeSheet(model, mats, rng.Next(3));
                if (sheet == null) break;
                sheet.transform.SetParent(root.transform, false);
                sheet.transform.position = new Vector3(x, floor.y + 0.004f + 0.0035f * under + Range(0f, 0.001f), z);
                sheet.transform.rotation = Quaternion.Euler(Range(-2.5f, 2.5f), Range(0f, 360f), Range(-2.5f, 2.5f));
                float s = sheetSize / 0.30f * Range(0.85f, 1.15f);
                sheet.transform.localScale = new Vector3(s * Range(0.92f, 1.08f), s, s * Range(0.92f, 1.08f));
                placed.Add(sheet.transform.position);
                count++;
            }
            Selection.activeGameObject = root;
            Debug.Log("[Papéis] " + count + " folhas em " + centers.Count + " montinhos");
        }

        bool TryFloor(float x, float z, Bounds area, out Vector3 floor)
        {
            floor = default;
            var hits = Physics.RaycastAll(new Vector3(x, area.max.y + 1f, z), Vector3.down, area.size.y + 3f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.normal.y > 0.9f).OrderBy(h => h.point.y).ToArray();
            if (hits.Length == 0) return false;
            floor = hits[0].point;
            float half = sheetSize * 0.5f;
            var boxCenter = floor + Vector3.up * 0.06f;
            return !Physics.CheckBox(boxCenter, new Vector3(half, 0.04f, half), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        }

        static GameObject MakeSheet(GameObject model, Material[] mats, int pick)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            var mrs = inst.GetComponentsInChildren<MeshRenderer>(true);
            if (mrs.Length == 0) { Object.DestroyImmediate(inst); return null; }
            var mr = mrs[pick % mrs.Length];
            var mf = mr.GetComponent<MeshFilter>();

            var go = new GameObject("Folha");
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mats.Length > 0 ? mats[pick % mats.Length] : mr.sharedMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Bounds mb = mf.sharedMesh.bounds;
            int thin = mb.size.x <= mb.size.y && mb.size.x <= mb.size.z ? 0 : (mb.size.y <= mb.size.z ? 1 : 2);
            Vector3 n = thin == 0 ? Vector3.right : (thin == 1 ? Vector3.up : Vector3.forward);
            Quaternion toFlat = Quaternion.FromToRotation(mr.transform.TransformDirection(n), Vector3.up);
            go.transform.rotation = toFlat * mr.transform.rotation;
            go.transform.localScale = Vector3.one;

            Bounds wb = r.bounds;
            float longest = Mathf.Max(wb.size.x, wb.size.z);
            if (longest > 1e-5f) go.transform.localScale = Vector3.one * (0.30f / longest);
            Vector3 centerWorld = r.bounds.center;
            var holder = new GameObject("Folha");
            holder.transform.position = centerWorld;
            go.transform.SetParent(holder.transform, true);
            Object.DestroyImmediate(inst);
            return holder;
        }

        static Material[] EnsureMaterials(GameObject model)
        {
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            var list = new List<Material>();
            for (int i = 1; i <= 3; i++)
            {
                string path = MatDir + "Papel_" + i + "_URP.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null && urp != null)
                {
                    m = new Material(urp) { name = "Papel_" + i + "_URP" };
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + i + ".png");
                    if (tex != null) m.SetTexture("_BaseMap", tex);
                    m.SetColor("_BaseColor", Color.white);
                    m.SetFloat("_Cull", 0f);
                    m.SetFloat("_Smoothness", 0.1f);
                    m.SetFloat("_Metallic", 0f);
                    AssetDatabase.CreateAsset(m, path);
                }
                if (m != null) list.Add(m);
            }
            AssetDatabase.SaveAssets();
            return list.ToArray();
        }
    }
}
