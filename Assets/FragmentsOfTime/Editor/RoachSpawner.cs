using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RiftOfTime.EditorTools
{
    /// <summary>Espalha baratas (Assets/Movels/baratas/source/Roach/roach.fbx) pelo chão da cena.</summary>
    public class RoachSpawner : EditorWindow
    {
        const string Model = "Assets/Movels/baratas/source/Roach/roach.fbx";
        const string TexDir = "Assets/Movels/baratas/source/Roach/";
        const string MatPath = "Assets/Movels/baratas/Roach_URP.mat";
        const string CtrlPath = "Assets/Movels/baratas/RoachController.controller";
        const string RootName = "Baratas";

        bool onlySelected;
        int count = 45, seed = 3, clusters = 10, perCluster = 6;
        float bodyLength = 0.07f, margin = 0.4f, separation = 3f;

        static void Open() { GetWindow<RoachSpawner>("Espalhar baratas"); }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("As baratas vão para TODOS os chãos da cena (corredores, salas, escadas, Office), mesmo que haja algo selecionado. Só limita a um objeto se você ligar a opção abaixo. As baratas fogem quando a lanterna (F) as ilumina.", MessageType.Info);
            onlySelected = EditorGUILayout.ToggleLeft("Limitar ao objeto selecionado na Hierarquia (desligado = TODAS as áreas)", onlySelected);
            count = EditorGUILayout.IntSlider("Baratas espalhadas", count, 0, 80);
            separation = EditorGUILayout.Slider("Distância mínima entre elas (m)", separation, 0f, 12f);
            clusters = EditorGUILayout.IntSlider("Grupos em cantos", clusters, 0, 15);
            perCluster = EditorGUILayout.IntSlider("Baratas por grupo", perCluster, 2, 12);
            bodyLength = EditorGUILayout.Slider("Comprimento do corpo (m)", bodyLength, 0.03f, 0.25f);
            margin = EditorGUILayout.Slider("Distância da parede (m)", margin, 0f, 1.5f);
            seed = EditorGUILayout.IntField("Semente", seed);
            if (GUILayout.Button("Espalhar baratas")) Spawn();
            if (GUILayout.Button("Remover baratas")) { var old = GameObject.Find(RootName); if (old != null) Undo.DestroyObjectImmediate(old); }
        }

        void Spawn()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if (model == null) { EditorUtility.DisplayDialog("Baratas", "Não achei " + Model, "OK"); return; }

            var oldRoot = GameObject.Find(RootName);
            GameObject limit = onlySelected ? Selection.activeGameObject : null;
            Renderer[] rends = limit != null
                ? limit.GetComponentsInChildren<Renderer>()
                : Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (oldRoot != null) rends = rends.Where(r => !r.transform.IsChildOf(oldRoot.transform)).ToArray();
            rends = rends.Where(r => !(r is ParticleSystemRenderer) && !(r is SkinnedMeshRenderer)).ToArray();
            if (rends.Length == 0) { EditorUtility.DisplayDialog("Baratas", "Não achei malhas para medir a área.", "OK"); return; }
            Bounds area = rends[0].bounds;
            foreach (var r in rends) area.Encapsulate(r.bounds);

            BuildPatches(limit);
            if (patches.Count == 0) { EditorUtility.DisplayDialog("Baratas", "Nenhum chão com collider na cena. Rode antes o item 8 do menu (colisão nos objetos sem).", "OK"); return; }
            var mat = EnsureMaterial();
            var ctrl = EnsureController();
            Physics.SyncTransforms();
            if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot);

            var rng = new System.Random(seed);
            float Range(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Espalhar baratas");

            int placed = 0;

            void Place(Vector3 floor)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                go.name = "Barata";
                go.transform.SetParent(root.transform, false);
                go.transform.position = floor;
                go.transform.rotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                var smr = go.GetComponentsInChildren<SkinnedMeshRenderer>();
                var mr = go.GetComponentsInChildren<MeshRenderer>();
                Bounds b = smr.Length > 0 ? smr[0].bounds : mr.Length > 0 ? mr[0].bounds : new Bounds(go.transform.position, Vector3.one);
                foreach (var sm0 in smr) b.Encapsulate(sm0.bounds);
                foreach (var m0 in mr) b.Encapsulate(m0.bounds);
                float longest = Mathf.Max(b.size.x, b.size.z);
                if (longest > 1e-5f) go.transform.localScale = Vector3.one * (bodyLength * Range(0.85f, 1.2f) / longest);
                go.transform.position = floor;

                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (r is SkinnedMeshRenderer sm) sm.updateWhenOffscreen = true;
                }
                var anim = go.GetComponentInChildren<Animator>();
                if (anim == null) anim = go.AddComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                go.AddComponent<RoachBehavior>();
                placed++;
            }

            var spots = new List<Vector3>();
            for (int tries = 0; tries < Mathf.Max(1, count) * 200 && spots.Count < count; tries++)
            {
                if (!SampleFloor(rng, out Vector3 floor)) continue;
                bool far = true;
                foreach (var q in spots) if ((q - floor).sqrMagnitude < separation * separation) { far = false; break; }
                if (!far) continue;
                spots.Add(floor);
                Place(floor);
            }

            var corners = new List<Vector3>();
            var dirs = new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            for (int tries = 0; tries < 20000 && corners.Count < clusters; tries++)
            {
                if (!SampleFloor(rng, out Vector3 floor)) continue;
                float[] d = new float[4];
                for (int i = 0; i < 4; i++)
                    d[i] = Physics.Raycast(floor + Vector3.up * 0.1f, dirs[i], out RaycastHit h, 1.5f, ~0, QueryTriggerInteraction.Ignore) && h.normal.y < 0.5f ? h.distance : 99f;
                int bx = d[0] < d[1] ? 0 : 1, bz = d[2] < d[3] ? 2 : 3;
                if (d[bx] > 1.2f || d[bz] > 1.2f) continue;
                Vector3 corner = floor + dirs[bx] * (d[bx] - 0.2f) + dirs[bz] * (d[bz] - 0.2f);
                if (!FloorNear(corner, out Vector3 cf)) continue;
                bool apart = true;
                foreach (var c in corners) if ((c - cf).sqrMagnitude < 8f * 8f) { apart = false; break; }
                if (!apart) continue;
                corners.Add(cf);
            }
            foreach (var cf in corners)
            {
                Vector3 inward = Vector3.zero;
                for (int i = 0; i < perCluster; i++)
                {
                    Vector3 around = cf + new Vector3(Range(-0.3f, 0.3f), 0f, Range(-0.3f, 0.3f));
                    if (FloorNear(around, out Vector3 f)) Place(f);
                    else Place(cf);
                }
            }
            Debug.Log("[Baratas] " + spots.Count + " espalhadas e " + corners.Count + " grupos em cantos");

            Selection.activeGameObject = root;
            Debug.Log("[Baratas] " + placed + " baratas colocadas");
            if (placed == 0)
                EditorUtility.DisplayDialog("Baratas", "Não achei chão livre. O chão precisa ter colliders.", "OK");
        }

        static readonly List<Bounds> patches = new List<Bounds>();
        static readonly List<float> cumArea = new List<float>();

        static void BuildPatches(GameObject limit)
        {
            patches.Clear(); cumArea.Clear();
            float acc = 0f;
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (c.isTrigger || !c.enabled) continue;
                if (c.GetComponentInParent<Camera>(true) != null || c.GetComponentInParent<RoachBehavior>() != null) continue;
                if (limit != null && !c.transform.IsChildOf(limit.transform)) continue;
                Bounds b = c.bounds;
                if (b.size.x < 1.5f || b.size.z < 1.5f) continue;
                string n = (c.name + " " + (c.transform.parent != null ? c.transform.parent.name : "")).ToLower();
                bool flat = b.size.y <= 1.2f;
                bool floorish = n.Contains("floor") || n.Contains("corridor") || n.Contains("stairs") || n.Contains("ground") || n.Contains("office") || n.Contains("room") || n.Contains("pit");
                if (!flat && !floorish) continue;
                if (n.Contains("ceiling") || n.Contains("wall")) continue;
                patches.Add(b);
                acc += b.size.x * b.size.z;
                cumArea.Add(acc);
            }
        }

        static bool SampleFloor(System.Random rng, out Vector3 floor)
        {
            floor = default;
            if (patches.Count == 0) return false;
            float pick = (float)rng.NextDouble() * cumArea[cumArea.Count - 1];
            int lo = 0, hi = cumArea.Count - 1;
            while (lo < hi) { int mid = (lo + hi) / 2; if (cumArea[mid] < pick) lo = mid + 1; else hi = mid; }
            Bounds b = patches[lo];
            float x = b.min.x + (float)rng.NextDouble() * b.size.x, z = b.min.z + (float)rng.NextDouble() * b.size.z;
            return FloorAt(new Vector3(x, b.min.y + 1.6f, z), b.min.y - 0.4f, out floor);
        }

        static bool FloorNear(Vector3 p, out Vector3 floor) { return FloorAt(p + Vector3.up * 0.8f, p.y - 1.2f, out floor); }

        static bool FloorAt(Vector3 from, float minY, out Vector3 floor)
        {
            floor = default;
            float best = float.NegativeInfinity;
            foreach (var h in Physics.RaycastAll(from, Vector3.down, from.y - minY + 0.3f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.normal.y < 0.9f || h.transform.GetComponentInParent<RoachBehavior>() != null) continue;
                if (h.point.y > best) { best = h.point.y; floor = h.point; }
            }
            if (float.IsNegativeInfinity(best)) return false;
            return !Physics.CheckBox(floor + Vector3.up * 0.06f, new Vector3(0.1f, 0.04f, 0.1f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        }

        static bool TryFloor(float x, float z, Bounds area, out Vector3 floor)
        {
            floor = default;
            var hits = Physics.RaycastAll(new Vector3(x, area.max.y + 1f, z), Vector3.down, area.size.y + 3f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.normal.y > 0.9f).OrderBy(h => h.point.y).ToArray();
            if (hits.Length == 0) return false;
            floor = hits[0].point;
            return !Physics.CheckBox(floor + Vector3.up * 0.06f, new Vector3(0.1f, 0.04f, 0.1f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        }

        static Material EnsureMaterial()
        {
            var importer = AssetImporter.GetAtPath(Model) as ModelImporter;
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null && urp != null)
            {
                mat = new Material(urp) { name = "Roach_URP" };
                Texture2D Tex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + n + ".png");
                var bc = Tex("BaseColor");
                if (bc != null) { mat.SetTexture("_BaseMap", bc); mat.SetColor("_BaseColor", Color.white); }
                var nm = Tex("Normal");
                if (nm != null)
                {
                    var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(nm)) as TextureImporter;
                    if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
                    mat.SetTexture("_BumpMap", nm);
                    mat.EnableKeyword("_NORMALMAP");
                }
                mat.SetFloat("_Smoothness", 0.55f);
                mat.SetFloat("_Metallic", 0.1f);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                mat.SetTexture("_EmissionMap", bc);
                mat.SetColor("_EmissionColor", new Color(0.25f, 0.25f, 0.25f));
                AssetDatabase.CreateAsset(mat, MatPath);
            }
            if (importer != null && mat != null)
            {
                bool changed = false;
                foreach (var src in AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Material>())
                {
                    var key = new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name);
                    if (!importer.GetExternalObjectMap().ContainsKey(key)) { importer.AddRemap(key, mat); changed = true; }
                }
                if (changed) importer.SaveAndReimport();
            }
            return mat;
        }

        static AnimatorController EnsureController()
        {
            var importer = AssetImporter.GetAtPath(Model) as ModelImporter;
            if (importer != null && importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.SaveAndReimport();
            }
            if (importer != null)
            {
                var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
                if (clips.Length > 0 && !clips[0].loopTime)
                {
                    clips[0].loopTime = true;
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();
                }
            }
            var clip = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
            if (ctrl == null)
            {
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
                var sm = ctrl.layers[0].stateMachine;
                var st = sm.AddState("Walk");
                st.motion = clip;
                sm.defaultState = st;
                EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
            }
            return ctrl;
        }
    }
}
