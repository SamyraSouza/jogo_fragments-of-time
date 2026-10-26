using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RiftOfTime.EditorTools
{
    /// <summary>Menu "Rift Of Time": monta as cenas do jogo (uma por tela do storyboard) a partir do pacote</summary>
    public static class RiftOfTimeBuilder
    {
        const string Pack = "Assets/Ancient Modular Tombs/";
        const string PackPrefabs = Pack + "Prefabs/";
        const string ScenesDir = "Assets/Scenes/FragmentsOfTime";
        const string ResDir = "Assets/FragmentsOfTime/Resources/RoT";
        const string GateModel = Pack + "Meshes/portal_gate_sci-fi.glb";
        const string KnifeModel = Pack + "knife.glb";
        const float GateScale = 0.5f;
        const float T = 5f;

        enum Anchor { Bottom, Top, Center }

        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (File.Exists(ScenesDir + "/00_Menu.unity") &&
                !EditorUtility.DisplayDialog("Rift Of Time",
                    "As cenas já existem em " + ScenesDir + ".\nConstruir de novo vai SOBRESCREVÊ-LAS e apagar as suas edições " +
                    "(escala do templo, braços, faca...).\n\nContinuar?", "Sobrescrever", "Cancelar"))
                return;

            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes/FragmentsOfTime"));
            AssetDatabase.Refresh();
            EnsureMaterials();

            var paths = new List<string>();
            try
            {
                ShowProgress("Tela 00 – Menu", 0.05f); paths.Add(BuildMenuScene());
                ShowProgress("Tela 01 – Prólogo", 0.2f); paths.Add(BuildPrologue());
                ShowProgress("Tela 02 – Fase 1", 0.4f); paths.Add(BuildFase1());
                ShowProgress("Tela 03 – Fase 2", 0.6f); paths.Add(BuildFase2());
                ShowProgress("Tela 04 – Fase 3", 0.8f); paths.Add(BuildFase3());
                ShowProgress("Tela 05 – Final", 0.95f); paths.Add(BuildFinal());
            }
            finally { EditorUtility.ClearProgressBar(); }

            UpdateBuildSettings(paths);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(paths[0]);

            EditorUtility.DisplayDialog("Rift Of Time",
                "Cenas criadas em " + ScenesDir + " e adicionadas ao Build Settings.\n\n" +
                "Abra a cena 00_Menu e aperte Play.", "OK");
        }

        public static void OpenMenu()
        {
            string p = ScenesDir + "/00_Menu.unity";
            if (!File.Exists(p)) { Debug.LogWarning("[RiftOfTime] Rode primeiro: Rift Of Time > 1. Construir todas as cenas."); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(p);
        }

        public static void AttachKnifeToExistingScenes()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(KnifeModel) == null)
            {
                EditorUtility.DisplayDialog("Rift Of Time", "Não achei " + KnifeModel + ".", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string original = EditorSceneManager.GetActiveScene().path;
            int added = 0, already = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                var scene = EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(guid), OpenSceneMode.Single);
                var player = GameObject.FindGameObjectWithTag("Player");
                var cam = player != null ? player.GetComponentInChildren<Camera>() : null;
                if (cam == null) continue;
                if (cam.transform.Find("Dagger") != null) { already++; continue; }

                AttachKnife(player);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                added++;
            }
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);

            EditorUtility.DisplayDialog("Rift Of Time",
                "Faca colocada em " + added + " cena(s). Já tinham a faca: " + already + ".", "OK");
        }

        public static void FixBuildSettings()
        {
            string[] names = { "00_Menu", "01_Prologo", "02_Fase1_TemploPerdido", "03_Fase2_RuinasDoDeserto", "04_Fase3_CamaraDoTempo", "05_Final" };
            var paths = new List<string>();
            foreach (string n in names)
            {
                string p = ScenesDir + "/" + n + ".unity";
                if (!File.Exists(p)) { EditorUtility.DisplayDialog("Rift Of Time", "Não achei " + p, "OK"); return; }
                paths.Add(p);
            }
            UpdateBuildSettings(paths);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Rift Of Time", "Build Settings agora aponta para as cenas de " + ScenesDir + ".", "OK");
        }

        static void ShowProgress(string label, float v) { EditorUtility.DisplayProgressBar("Rift Of Time", label, v); }

        static void EnsureMaterials()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "FragmentsOfTime/Resources/RoT"));
            AssetDatabase.Refresh();
            MakeMat("RoT_Lit", "Universal Render Pipeline/Lit");
            MakeMat("RoT_Unlit", "Universal Render Pipeline/Unlit");

            string sandPath = ResDir + "/RoT_Sand.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(sandPath) == null)
            {
                Material sand = MakeMat("RoT_Sand", "Universal Render Pipeline/Lit");
                if (sand != null)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Pack + "Terrain textures/Desert Sand_albedo.tif");
                    if (tex != null)
                    {
                        sand.SetTexture("_BaseMap", tex);
                        sand.SetTextureScale("_BaseMap", new Vector2(120f, 120f));
                    }
                    sand.SetColor("_BaseColor", new Color(0.95f, 0.85f, 0.7f));
                    sand.SetFloat("_Smoothness", 0.05f);
                    EditorUtility.SetDirty(sand);
                }
            }
            AssetDatabase.SaveAssets();
        }

        static Material MakeMat(string name, string shader)
        {
            string path = ResDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            Shader sh = Shader.Find(shader);
            if (sh == null) { Debug.LogError("[RiftOfTime] Shader não encontrado: " + shader); return null; }
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material Sand() { return AssetDatabase.LoadAssetAtPath<Material>(ResDir + "/RoT_Sand.mat"); }

        static Material Unlit(Color c)
        {
            var m = new Material(AssetDatabase.LoadAssetAtPath<Material>(ResDir + "/RoT_Unlit.mat"));
            m.SetColor("_BaseColor", c);
            return m;
        }

        static Material PackMat(string file)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(Pack + "Interior modular meshes/Materials/" + file);
            if (m == null) m = AssetDatabase.LoadAssetAtPath<Material>(Pack + "Materials/" + file);
            return m;
        }

        static UnityEngine.SceneManagement.Scene NewScene()
        {
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        static string Save(UnityEngine.SceneManagement.Scene s, string name)
        {
            string path = ScenesDir + "/" + name + ".unity";
            EditorSceneManager.SaveScene(s, path);
            return path;
        }

        static GameObject Go(string name, Transform parent = null)
        {
            var g = new GameObject(name);
            if (parent != null) g.transform.SetParent(parent, false);
            return g;
        }

        static void SetAmbient(Color c)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = c;
        }

        static void SetFog(bool on, Color c, float density)
        {
            RenderSettings.fog = on;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = c;
            RenderSettings.fogDensity = density;
        }

        static Camera MakeCamera(string name, Vector3 pos, Quaternion rot, Color bg, Transform parent = null)
        {
            var g = Go(name, parent);
            g.tag = "MainCamera";
            g.transform.position = pos;
            g.transform.rotation = rot;
            var cam = g.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 5000f;
            g.AddComponent<AudioListener>();
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;
            return cam;
        }

        static void MakeVolume()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Pack + "Profiles/PostFX Profile.asset");
            if (profile == null) return;
            var g = Go("Global Volume");
            var v = g.AddComponent<Volume>();
            v.isGlobal = true;
            v.sharedProfile = profile;
        }

        static Light Directional(string name, Color c, float intensity, Vector3 euler)
        {
            var g = Go(name);
            g.transform.rotation = Quaternion.Euler(euler);
            var l = g.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = c;
            l.intensity = intensity;
            l.shadows = LightShadows.Soft;
            return l;
        }

        static Light PointLight(Transform parent, Vector3 pos, Color c, float intensity, float range)
        {
            var g = Go("PointLight", parent);
            g.transform.position = pos;
            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            return l;
        }

        static readonly Color Warm = new Color(1f, 0.68f, 0.35f);

        static GameObject PlayerAt(Vector3 pos, float yaw, bool withKnife = true)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackPrefabs + "Player.prefab");
            if (prefab == null) { Debug.LogError("[RiftOfTime] Player.prefab não encontrado."); return null; }
            var p = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            p.transform.position = pos;
            p.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (withKnife) AttachKnife(p);
            return p;
        }

        static void AttachKnife(GameObject player)
        {
            var cam = player.GetComponentInChildren<Camera>();
            var knife = AssetDatabase.LoadAssetAtPath<GameObject>(KnifeModel);
            if (cam == null || knife == null) { Debug.LogWarning("[RiftOfTime] Faca não colocada: câmera ou knife.glb ausente."); return; }
            var holder = Go("Dagger", cam.transform);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(knife, holder.transform);
            model.transform.localRotation = Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, 90f, 0f);
            model.transform.localScale = Vector3.one * 0.1f;
            model.transform.localPosition = new Vector3(0.07f, 0f, 0.09f);
        }

        static GameObject LoadPrefab(string name)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(PackPrefabs + name + ".prefab");
            if (p == null) Debug.LogWarning("[RiftOfTime] Prefab não encontrado: " + name);
            return p;
        }

        static GameObject Instance(string name, Transform parent)
        {
            var prefab = LoadPrefab(name);
            if (prefab == null) return null;
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        }

        static Bounds MeshBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<MeshRenderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        static void Fit(GameObject go, Vector3 pos, Anchor anchor)
        {
            Bounds b = MeshBounds(go);
            if (b.size == Vector3.zero) { go.transform.position = pos; return; }
            Vector3 off = pos - b.center;
            if (anchor == Anchor.Bottom) off.y = pos.y - b.min.y;
            else if (anchor == Anchor.Top) off.y = pos.y - b.max.y;
            go.transform.position += off;
        }

        static void EnsureColliders(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>() != null) return;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                    mf.gameObject.AddComponent<MeshCollider>();
        }

        static GameObject Place(string name, Vector3 pos, Anchor anchor, float rotY, Transform parent, float scale = 1f, bool colliders = true)
        {
            GameObject go = Instance(name, parent);
            if (go == null) return null;
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            go.transform.localScale = Vector3.one * scale;
            Fit(go, pos, anchor);
            if (colliders) EnsureColliders(go);
            return go;
        }

        static GameObject PlaceRaw(string name, Vector3 pos, float rotY, Transform parent, float scale = 1f)
        {
            GameObject go = Instance(name, parent);
            if (go == null) return null;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        static GameObject PlaceWall(string name, Vector3 pos, bool alongX, bool flip, Transform parent, bool colliders = true)
        {
            GameObject go = Instance(name, parent);
            if (go == null) return null;
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            Bounds b = MeshBounds(go);
            bool longIsX = b.size.x >= b.size.z;
            float rot = (longIsX == alongX) ? 0f : 90f;
            if (flip) rot += 180f;
            go.transform.rotation = Quaternion.Euler(0f, rot, 0f);
            Fit(go, pos, Anchor.Bottom);
            if (colliders) EnsureColliders(go);
            return go;
        }

        /// <summary>Sala feita de peças de 5x5 m.</summary>
        static void BuildRoom(Transform parent, float ox, float oz, int w, int d, float y0,
            string[] doors, string[] skip, bool ceiling, Func<int, int, bool> skipFloor)
        {
            var doorSet = new HashSet<string>(doors ?? new string[0]);
            var skipSet = new HashSet<string>(skip ?? new string[0]);

            for (int i = 0; i < w; i++)
                for (int j = 0; j < d; j++)
                {
                    float cx = ox + (i + 0.5f) * T, cz = oz + (j + 0.5f) * T;
                    if (ceiling) Place("Ceiling_5x5m", new Vector3(cx, y0 + 5f, cz), Anchor.Bottom, 0f, parent);
                    if (skipFloor != null && skipFloor(i, j)) continue;
                    Place("Floor_5x5m", new Vector3(cx, y0, cz), Anchor.Top, 0f, parent);
                }

            for (int i = 0; i < w; i++)
            {
                float cx = ox + (i + 0.5f) * T;
                WallPiece(parent, "S" + i, new Vector3(cx, y0, oz), true, false, doorSet, skipSet);
                WallPiece(parent, "N" + i, new Vector3(cx, y0, oz + d * T), true, true, doorSet, skipSet);
            }
            for (int j = 0; j < d; j++)
            {
                float cz = oz + (j + 0.5f) * T;
                WallPiece(parent, "W" + j, new Vector3(ox, y0, cz), false, true, doorSet, skipSet);
                WallPiece(parent, "E" + j, new Vector3(ox + w * T, y0, cz), false, false, doorSet, skipSet);
            }
        }

        static void WallPiece(Transform parent, string key, Vector3 pos, bool alongX, bool flip, HashSet<string> doors, HashSet<string> skip)
        {
            if (skip.Contains(key)) return;
            bool door = doors.Contains(key);
            PlaceWall(door ? "Wall_5x5m_door" : "Wall_5x5m", pos, alongX, flip, parent);
            if (door) PlaceWall("Wall_5x5m", pos + Vector3.up * 3.5f, alongX, flip, parent);
        }

        static void Column(Transform parent, float x, float z, float y0)
        {
            Place("Column_5m_Height", new Vector3(x, y0, z), Anchor.Bottom, 0f, parent);
        }

        static void Torch(Transform parent, float x, float y, float z, float rotY)
        {
            Place("Torchfire", new Vector3(x, y, z), Anchor.Center, rotY, parent, 1f, false);
        }

        static Vector3 Altar(Transform parent, float x, float y0, float z)
        {
            Material mat = PackMat("Floor.mat");
            Transform a = Go("Altar", parent).transform;
            Cube(a, new Vector3(x, y0 + 0.15f, z), new Vector3(3.4f, 0.3f, 3.4f), mat);
            Cube(a, new Vector3(x, y0 + 0.45f, z), new Vector3(2.5f, 0.3f, 2.5f), mat);
            Cube(a, new Vector3(x, y0 + 0.9f, z), new Vector3(1f, 0.6f, 1f), mat);
            return new Vector3(x, y0 + 1.75f, z);
        }

        static GameObject Cube(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c.transform.SetParent(parent, false);
            c.transform.position = pos;
            c.transform.localScale = scale;
            if (mat != null) c.GetComponent<Renderer>().sharedMaterial = mat;
            return c;
        }

        static MummyGuardian MakeGuardian(Transform parent, Vector3 pos, float yaw, bool pharaoh)
        {
            var g = Go(pharaoh ? "Faraó (Guardião II)" : "Guardiã I", parent);
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            g.AddComponent<CharacterController>();
            var m = g.AddComponent<MummyGuardian>();
            m.isPharaoh = pharaoh;
            m.displayName = pharaoh ? "Faraó" : "Guardiã I";
            m.maxHealth = pharaoh ? 170f : 100f;
            m.moveSpeed = pharaoh ? 2.0f : 2.5f;
            m.attackDamage = pharaoh ? 18f : 14f;
            return m;
        }

        static FragmentPickup MakeFragment(Transform parent, Vector3 pos, int index)
        {
            var g = Go("Fragmento " + index, parent);
            g.transform.position = pos;
            var f = g.AddComponent<FragmentPickup>();
            f.fragmentIndex = index;
            return f;
        }

        static TimeMachine MakeMachine(Transform parent, Vector3 pos, float yaw)
        {
            var g = Go("Máquina do Tempo", parent);
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var gate = AssetDatabase.LoadAssetAtPath<GameObject>(GateModel);
            if (gate == null) Debug.LogWarning("[RiftOfTime] Modelo da máquina não encontrado; instale o glTFast: " + GateModel);
            else
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(gate, g.transform);
                model.name = "Modelo";
                model.transform.localScale = Vector3.one * GateScale;
                EnsureColliders(model);
            }
            return g.AddComponent<TimeMachine>();
        }

        static AudioClip Clip(string file) { return AssetDatabase.LoadAssetAtPath<AudioClip>(Pack + "Sounds/" + file); }

        static string BuildMenuScene()
        {
            var sc = NewScene();
            Color sky = new Color(0.04f, 0.07f, 0.18f);
            SetAmbient(new Color(0.20f, 0.22f, 0.36f));
            SetFog(false, sky, 0f);
            var root = Go("Cenario").transform;

            var pyr = Place("Pyramid", Vector3.zero, Anchor.Bottom, 0f, root, 1f, false);
            Bounds b = pyr != null ? MeshBounds(pyr) : new Bounds(new Vector3(0f, 40f, 80f), new Vector3(140f, 80f, 140f));
            float s = Mathf.Max(b.size.x, 40f);

            Cube(root, new Vector3(b.center.x, -0.5f, b.center.z), new Vector3(s * 40f, 1f, s * 40f), Sand());

            for (int i = 0; i < 4; i++)
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                d.name = "Duna" + i;
                d.transform.SetParent(root, false);
                d.transform.position = new Vector3(b.center.x + s * (0.6f + i * 0.55f), 0f, b.center.z - s * 0.15f + i * s * 0.3f);
                d.transform.localScale = new Vector3(s * 1.1f, s * 0.22f, s * 0.7f);
                d.GetComponent<Renderer>().sharedMaterial = Sand();
                UnityEngine.Object.DestroyImmediate(d.GetComponent<Collider>());
            }

            Vector3 camPos = new Vector3(b.center.x + s * 0.45f, s * 0.28f, b.min.z - s * 1.15f);
            Vector3 target = new Vector3(b.center.x, s * 0.32f, b.center.z);
            Camera cam = MakeCamera("Main Camera", camPos, Quaternion.LookRotation(target - camPos), sky);
            cam.fieldOfView = 42f;

            Vector3 fwd = cam.transform.forward, right = cam.transform.right, up = cam.transform.up;
            float dist = s * 8f;
            Vector3 moonPos = camPos + fwd * dist + right * (dist * 0.36f) + up * (dist * 0.30f);
            float mr = dist * 0.085f;
            var moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            moon.name = "Lua";
            moon.transform.SetParent(root, false);
            moon.transform.position = moonPos;
            moon.transform.localScale = Vector3.one * mr * 2f;
            moon.GetComponent<Renderer>().sharedMaterial = Unlit(new Color(1f, 0.93f, 0.75f));
            UnityEngine.Object.DestroyImmediate(moon.GetComponent<Collider>());
            var cut = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cut.name = "LuaSombra";
            cut.transform.SetParent(root, false);
            cut.transform.position = moonPos + right * (mr * 0.55f) + up * (mr * 0.15f) - fwd * (mr * 0.3f);
            cut.transform.localScale = Vector3.one * mr * 1.85f;
            cut.GetComponent<Renderer>().sharedMaterial = Unlit(sky);
            UnityEngine.Object.DestroyImmediate(cut.GetComponent<Collider>());

            var stars = Go("Estrelas", root);
            stars.AddComponent<StarTwinkle>();
            var rnd = new System.Random(7);
            Material starMat = Unlit(new Color(1f, 0.97f, 0.85f));
            for (int i = 0; i < 260; i++)
            {
                float az = (float)(rnd.NextDouble() * 90.0 - 45.0);
                float el = (float)(rnd.NextDouble() * 38.0 + 4.0);
                Vector3 dir = Quaternion.AngleAxis(az, Vector3.up) * Quaternion.AngleAxis(-el, Vector3.right) * Vector3.forward;
                dir = Quaternion.LookRotation(fwd, Vector3.up) * dir;
                var st = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                st.name = "Estrela";
                st.transform.SetParent(stars.transform, false);
                st.transform.position = camPos + dir * dist * 1.2f;
                st.transform.localScale = Vector3.one * dist * (0.004f + (float)rnd.NextDouble() * 0.008f);
                st.GetComponent<Renderer>().sharedMaterial = starMat;
                UnityEngine.Object.DestroyImmediate(st.GetComponent<Collider>());
            }

            Directional("Luz da Lua", new Color(0.55f, 0.65f, 1f), 0.9f, new Vector3(35f, 200f, 0f));
            MakeVolume();

            var ctl = Go("MenuController");
            var menu = ctl.AddComponent<MainMenu>();
            menu.windClip = Clip("Wind.wav");

            return Save(sc, "00_Menu");
        }

        static readonly Vector3 ProloguePlayerPos = new Vector3(0f, 0.15f, 2.5f);
        static readonly Vector3 PrologueMachinePos = new Vector3(0f, 0f, 17f);
        static readonly Vector3 PrologueTargetPos = new Vector3(0f, 1.6f, 16.4f);

        public static void PlaceMachineAtSceneView()
        {
            SceneView sv = SceneView.lastActiveSceneView;
            if (sv == null)
            {
                EditorUtility.DisplayDialog("Rift Of Time",
                    "Abra a janela Scene e deixe o centro dela olhando para o chão onde a máquina deve ficar.", "OK");
                return;
            }
            var gate = AssetDatabase.LoadAssetAtPath<GameObject>(GateModel);
            if (gate == null)
            {
                EditorUtility.DisplayDialog("Rift Of Time", "Não achei o modelo da máquina em " + GateModel + ".", "OK");
                return;
            }

            Camera cam = sv.camera;
            Physics.SyncTransforms();
            Vector3 point = sv.pivot;
            RaycastHit hit;
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, 300f))
                point = hit.point + hit.normal * 1.5f;
            if (Physics.Raycast(point + Vector3.up * 2f, Vector3.down, out hit, 20f))
                point = hit.point;

            TimeMachine old = UnityEngine.Object.FindAnyObjectByType<TimeMachine>();
            GameObject machine = old != null ? old.gameObject : GameObject.Find("MaquinaTempo");
            if (machine == null) machine = GameObject.Find("Máquina do Tempo");
            if (machine != null)
            {
                int choice = EditorUtility.DisplayDialogComplex("Rift Of Time",
                    "Já existe uma máquina na cena ('" + machine.name + "').\n\n" +
                    "• Recriar limpa: apaga essa e coloca uma nova (resolve peças fora do lugar).\n" +
                    "• Só mover: mantém a atual e leva para o ponto novo.",
                    "Recriar limpa", "Cancelar", "Só mover");
                if (choice == 1) return;
                if (choice == 0) { Undo.DestroyObjectImmediate(machine); machine = null; }
            }

            if (machine == null)
            {
                machine = (GameObject)PrefabUtility.InstantiatePrefab(gate);
                machine.name = "Máquina do Tempo";
                machine.transform.localScale = Vector3.one * GateScale;
                Undo.RegisterCreatedObjectUndo(machine, "Criar a máquina do tempo");
            }
            else
            {
                Undo.RecordObject(machine.transform, "Mover a máquina do tempo");
                if (Mathf.Abs(machine.transform.lossyScale.x - 1f) < 0.01f) machine.transform.localScale = Vector3.one * GateScale;
            }

            Vector3 toCam = cam.transform.position - point;
            toCam.y = 0f;
            machine.transform.rotation = toCam.sqrMagnitude > 0.01f ? Quaternion.LookRotation(-toCam.normalized) : Quaternion.identity;

            machine.transform.position = point;
            Bounds b = RendererBounds(machine);
            if (b.size != Vector3.zero)
                machine.transform.position += new Vector3(point.x - b.center.x, point.y - b.min.y, point.z - b.center.z);

            TimeMachine tm = machine.GetComponent<TimeMachine>();
            if (tm == null) tm = Undo.AddComponent<TimeMachine>(machine);

            PrologueDirector[] dirs = UnityEngine.Object.FindObjectsByType<PrologueDirector>(FindObjectsSortMode.None);
            PrologueDirector dir = null;
            foreach (PrologueDirector d in dirs) if (d.machine != null || d.lookTarget != null) { dir = d; break; }
            if (dir == null && dirs.Length > 0) dir = dirs[0];
            int removed = 0;
            foreach (PrologueDirector d in dirs)
            {
                if (d == dir) continue;
                GameObject dgo = d.gameObject;
                if (dgo.GetComponents<Component>().Length <= 2) Undo.DestroyObjectImmediate(dgo);
                else Undo.DestroyObjectImmediate(d);
                removed++;
            }
            if (dir == null)
            {
                var g = new GameObject("PrologoDirector");
                Undo.RegisterCreatedObjectUndo(g, "Criar o PrologoDirector");
                dir = Undo.AddComponent<PrologueDirector>(g);
            }

            Transform target = dir.lookTarget;
            if (target == null)
            {
                GameObject t = GameObject.Find("AlvoDoPortal");
                if (t == null) t = GameObject.Find("AlvoPortal");
                if (t != null) target = t.transform;
            }
            if (target == null)
            {
                var tg = new GameObject("AlvoDoPortal");
                Undo.RegisterCreatedObjectUndo(tg, "Criar o alvo do portal");
                target = tg.transform;
            }
            Bounds nb = RendererBounds(machine);
            Undo.SetTransformParent(target, machine.transform, "Alvo dentro da máquina");
            Undo.RecordObject(target, "Posicionar o alvo do portal");
            target.position = (nb.size != Vector3.zero ? nb.center : machine.transform.position) - machine.transform.forward * 0.8f;

            Undo.RecordObject(dir, "Ligar o prólogo");
            dir.machine = tm;
            dir.lookTarget = target;
            EditorUtility.SetDirty(dir);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = machine;
            sv.FrameSelected();

            bool hasAnim = machine.GetComponentInChildren<Animation>(true) != null || machine.GetComponentInChildren<Animator>(true) != null;
            bool hasPlayer = GameObject.FindGameObjectWithTag("Player") != null;
            EditorUtility.DisplayDialog("Rift Of Time",
                "Máquina colocada em " + machine.transform.position.ToString("0.0") + " (escala " + machine.transform.lossyScale.x.ToString("0.##") + ").\n\n" +
                "• TimeMachine, AlvoDoPortal e PrologoDirector ligados\n" +
                (removed > 0 ? "• " + removed + " PrologoDirector duplicado(s) apagado(s)\n" : "") +
                "• Animação do portal: " + (hasAnim ? "encontrada" : "NÃO encontrada (o modelo veio sem animação)") + "\n" +
                (hasPlayer ? "" : "• ATENÇÃO: não há Player na cena\n") +
                "\nSe o tamanho não ficou bom, mude a Scale do objeto 'Máquina do Tempo' e salve (Ctrl+S).", "OK");
        }

        static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        public static void PreparePlayablePrologue()
        {
            var dir = UnityEngine.Object.FindAnyObjectByType<PrologueDirector>();
            var machine = UnityEngine.Object.FindAnyObjectByType<TimeMachine>();
            if (dir == null || machine == null)
            {
                EditorUtility.DisplayDialog("Rift Of Time",
                    "Abra primeiro a cena 01_Prologo (é preciso o PrologoDirector e a Máquina do Tempo).", "OK");
                return;
            }

            GameObject rig = GameObject.Find("CameraRig");
            if (rig != null) Undo.DestroyObjectImmediate(rig);

            Transform room = machine.transform.parent;
            float s = room != null ? room.lossyScale.x : 1f;
            Func<Vector3, Vector3> toWorld = local => room != null ? room.TransformPoint(local) : local;
            Quaternion roomRot = room != null ? room.rotation : Quaternion.identity;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = PlayerAt(toWorld(ProloguePlayerPos), 0f, false);
                if (player == null) return;
                Undo.RegisterCreatedObjectUndo(player, "Player do prólogo");
            }
            Undo.RecordObject(player.transform, "Posicionar o Player");
            player.transform.SetPositionAndRotation(toWorld(ProloguePlayerPos), roomRot);

            SetRoomPosition(machine.transform, room, PrologueMachinePos);

            Undo.RecordObject(dir, "Configurar prólogo");
            if (dir.lookTarget == null)
            {
                GameObject t = GameObject.Find("AlvoDoPortal");
                if (t != null) dir.lookTarget = t.transform;
            }
            if (dir.lookTarget != null) SetRoomPosition(dir.lookTarget, room, PrologueTargetPos);
            dir.machine = machine;

            int lights = 0;
            if (room != null)
                foreach (Light l in room.GetComponentsInChildren<Light>(true))
                {
                    if (l.type != LightType.Point) continue;
                    Undo.RecordObject(l, "Ajustar luz");
                    l.range = 14f * s;
                    l.intensity = 4f * s * s;
                    lights++;
                }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = dir.gameObject;
            EditorUtility.DisplayDialog("Rift Of Time",
                "Prólogo preparado (escala da sala: " + s.ToString("0.##") + "):\n" +
                "• Player na entrada, dentro da sala\n" +
                "• Máquina e alvo do portal no fundo da sala\n" +
                "• " + lights + " luz(es) ajustada(s) à escala\n\n" +
                "Salve a cena (Ctrl+S) e aperte Play. Os tempos ficam no objeto PrologoDirector.", "OK");
        }

        static void SetRoomPosition(Transform t, Transform room, Vector3 roomLocal)
        {
            Undo.RecordObject(t, "Mover " + t.name);
            if (room != null && t.parent == room) t.localPosition = roomLocal;
            else t.position = room != null ? room.TransformPoint(roomLocal) : roomLocal;
        }

        static string BuildPrologue()
        {
            var sc = NewScene();
            SetAmbient(new Color(0.16f, 0.14f, 0.13f));
            SetFog(false, Color.black, 0f);
            var root = Go("Camara Oculta").transform;
            BuildRoom(root, -10f, 0f, 4, 4, 0f, null, null, true, null);

            Column(root, -8f, 3f, 0f); Column(root, 8f, 3f, 0f);
            Column(root, -8f, 17f, 0f); Column(root, 8f, 17f, 0f);
            Torch(root, -9.3f, 2.4f, 10f, 90f); Torch(root, 9.3f, 2.4f, 10f, -90f);

            PointLight(root, new Vector3(-5f, 4f, 5f), Warm, 4f, 14f);
            PointLight(root, new Vector3(5f, 4f, 5f), Warm, 4f, 14f);
            PointLight(root, new Vector3(0f, 4f, 15f), Warm, 4f, 14f);

            TimeMachine machine = MakeMachine(root, PrologueMachinePos, 0f);
            var target = Go("AlvoDoPortal", root);
            target.transform.position = PrologueTargetPos;

            PlayerAt(ProloguePlayerPos, 0f, false);

            MakeVolume();

            var dir = Go("PrologoDirector").AddComponent<PrologueDirector>();
            dir.machine = machine;
            dir.lookTarget = target.transform;
            return Save(sc, "01_Prologo");
        }

        static string BuildFase1()
        {
            var sc = NewScene();
            SetAmbient(new Color(0.30f, 0.24f, 0.18f));
            SetFog(false, Color.black, 0f);
            var root = Go("Templo Perdido").transform;
            BuildRoom(root, -10f, 0f, 4, 6, 0f, null, null, true, null);

            foreach (float z in new[] { 7.5f, 15f, 22.5f })
            {
                Column(root, -6.5f, z, 0f);
                Column(root, 6.5f, z, 0f);
            }
            foreach (float z in new[] { 5f, 15f, 25f })
            {
                Torch(root, -9.3f, 2.4f, z, 90f);
                Torch(root, 9.3f, 2.4f, z, -90f);
            }
            Place("Anubis_statue", new Vector3(-4.5f, 0f, 27.5f), Anchor.Bottom, 180f, root);
            Place("Anubis_statue", new Vector3(4.5f, 0f, 27.5f), Anchor.Bottom, 180f, root);
            Place("Vase_1", new Vector3(-8f, 0f, 1.5f), Anchor.Bottom, 0f, root);
            Place("Vase_1", new Vector3(8f, 0f, 1.5f), Anchor.Bottom, 0f, root);

            foreach (float z in new[] { 6f, 15f, 24f })
            {
                PointLight(root, new Vector3(-6.5f, 3.6f, z), Warm, 5f, 14f);
                PointLight(root, new Vector3(6.5f, 3.6f, z), Warm, 5f, 14f);
            }

            Vector3 altarTop = Altar(root, 0f, 0f, 27f);

            GameObject player = PlayerAt(new Vector3(0f, 0.15f, 3f), 0f);
            var gObj = Go("Papiro");
            gObj.transform.position = new Vector3(-6.5f, 0f, 4f);
            var papyrus = gObj.AddComponent<PapyrusPickup>();

            MummyGuardian guardian = MakeGuardian(root, new Vector3(0f, 0f, 22f), 180f, false);
            FragmentPickup fragment = MakeFragment(root, altarTop, 1);
            fragment.autoCollect = true;

            MakeVolume();
            var pc = Go("PhaseController").AddComponent<PhaseController>();
            pc.phase = PhaseKind.Fase1;
            pc.guardian = guardian;
            pc.fragment = fragment;
            pc.papyrus = papyrus;
            return Save(sc, "02_Fase1_TemploPerdido");
        }

        static string BuildFase2()
        {
            var sc = NewScene();
            Color haze = new Color(0.86f, 0.74f, 0.55f);
            SetAmbient(new Color(0.22f, 0.19f, 0.16f));
            SetFog(false, haze, 0f);
            var skyMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            if (skyMat != null) RenderSettings.skybox = skyMat;

            var root = Go("Ruinas do Deserto").transform;
            const float y0 = 0.2f;

            Material sand = Sand();
            Cube(root, new Vector3(-205f, y0 - 0.5f, 0f), new Vector3(390f, 1f, 800f), sand);
            Cube(root, new Vector3(205f, y0 - 0.5f, 0f), new Vector3(390f, 1f, 800f), sand);
            Cube(root, new Vector3(0f, y0 - 0.5f, -202.5f), new Vector3(20f, 1f, 395f), sand);
            Cube(root, new Vector3(0f, y0 - 0.5f, 207.5f), new Vector3(20f, 1f, 385f), sand);

            var pyramid = PlaceRaw("Pyramid", new Vector3(0f, y0, 83f), 0f, root);
            if (pyramid != null) pyramid.SetActive(false);
            PlaceRaw("Pyramid_horizon", new Vector3(789f, y0, -354f), 0f, root);
            PlaceRaw("Pyramid_horizon", new Vector3(832f, y0, -698f), 0f, root, 0.7177f);

            var chamber = Go("Camara do Farao", root).transform;
            BuildRoom(chamber, -10f, -5f, 4, 4, y0, new[] { "S2" }, null, true, (i, j) => (i == 1 || i == 2) && (j == 1 || j == 2));
            var trap = Place("Floor_10x10m", new Vector3(0f, y0, 5f), Anchor.Top, 0f, chamber);
            TrapdoorFloor trapdoor = null;
            if (trap != null) trapdoor = trap.AddComponent<TrapdoorFloor>();

            Column(chamber, -8f, -2f, y0); Column(chamber, 8f, -2f, y0);
            Column(chamber, -8f, 12f, y0); Column(chamber, 8f, 12f, y0);
            Torch(chamber, -9.3f, y0 + 2.4f, 5f, 90f); Torch(chamber, 9.3f, y0 + 2.4f, 5f, -90f);
            PointLight(chamber, new Vector3(-6f, y0 + 3.6f, 0f), Warm, 5f, 14f);
            PointLight(chamber, new Vector3(6f, y0 + 3.6f, 0f), Warm, 5f, 14f);
            PointLight(chamber, new Vector3(0f, y0 + 3.6f, 10f), Warm, 6f, 14f);

            Vector3 altarTop = Altar(chamber, 0f, y0, 12.5f);
            Directional("Sol", new Color(1f, 0.93f, 0.8f), 0.35f, new Vector3(48f, -35f, 0f));
            BuildLabyrinth(root, y0);

            PlayerAt(MazeStart, 0f);

            MummyGuardian pharaoh = MakeGuardian(chamber, new Vector3(0f, y0, 9.2f), 180f, true);
            FragmentPickup fragment = MakeFragment(chamber, altarTop, 2);

            MakeVolume();
            var pc = Go("PhaseController").AddComponent<PhaseController>();
            pc.phase = PhaseKind.Fase2;
            pc.guardian = pharaoh;
            pc.fragment = fragment;
            pc.trapdoor = trapdoor;
            pc.windClip = Clip("Wind.wav");
            return Save(sc, "03_Fase2_RuinasDoDeserto");
        }

        const int MazeCols = 8, MazeRows = 12;
        const float MazeX0 = -20f, MazeZ0 = -65f;
        const int MazeExitCol = 4;
        static readonly Vector3 MazeStart = new Vector3(MazeX0 + 2.5f, 0.35f, MazeZ0 + 2.5f);

        static void BuildLabyrinth(Transform root, float y0)
        {
            var h = new bool[MazeCols, MazeRows + 1];
            var v = new bool[MazeCols + 1, MazeRows];
            for (int c = 0; c < MazeCols; c++) for (int r = 0; r <= MazeRows; r++) h[c, r] = true;
            for (int c = 0; c <= MazeCols; c++) for (int r = 0; r < MazeRows; r++) v[c, r] = true;

            var rnd = new System.Random(2026);
            var seen = new bool[MazeCols, MazeRows];
            var stack = new Stack<Vector2Int>();
            stack.Push(new Vector2Int(0, 0)); seen[0, 0] = true;
            int[] dx = { 1, -1, 0, 0 }, dz = { 0, 0, 1, -1 };
            while (stack.Count > 0)
            {
                Vector2Int cur = stack.Peek();
                var opts = new List<int>();
                for (int d = 0; d < 4; d++)
                {
                    int nx = cur.x + dx[d], nz = cur.y + dz[d];
                    if (nx >= 0 && nx < MazeCols && nz >= 0 && nz < MazeRows && !seen[nx, nz]) opts.Add(d);
                }
                if (opts.Count == 0) { stack.Pop(); continue; }
                int pick = opts[rnd.Next(opts.Count)];
                if (pick == 0) v[cur.x + 1, cur.y] = false;
                else if (pick == 1) v[cur.x, cur.y] = false;
                else if (pick == 2) h[cur.x, cur.y + 1] = false;
                else h[cur.x, cur.y] = false;
                var next = new Vector2Int(cur.x + dx[pick], cur.y + dz[pick]);
                seen[next.x, next.y] = true;
                stack.Push(next);
            }
            for (int i = 0; i < 8; i++)
            {
                if (rnd.Next(2) == 0) v[rnd.Next(1, MazeCols), rnd.Next(MazeRows)] = false;
                else h[rnd.Next(MazeCols), rnd.Next(1, MazeRows)] = false;
            }

            h[MazeExitCol, MazeRows] = false;

            for (int c = 0; c < MazeCols; c++)
                for (int r = 0; r < MazeRows; r++)
                {
                    float cx = MazeX0 + (c + 0.5f) * T, cz = MazeZ0 + (r + 0.5f) * T;
                    Place("Floor_5x5m", new Vector3(cx, y0 + 0.02f, cz), Anchor.Top, 0f, root, 1f, false);
                    Place("Ceiling_5x5m", new Vector3(cx, y0 + 5f, cz), Anchor.Bottom, 0f, root, 1f, false);
                    if (c % 2 == 0 && r % 3 == 1) PointLight(root, new Vector3(cx, y0 + 3.4f, cz), Warm, 2.6f, 10f);
                }
            PointLight(root, new Vector3(MazeX0 + (MazeExitCol + 0.5f) * T, y0 + 3f, MazeZ0 + (MazeRows - 0.6f) * T),
                new Color(1f, 0.85f, 0.4f), 4.5f, 13f);

            Material stone = PackMat("Parede_Pedra.mat");
            if (stone == null) stone = PackMat("Tile.mat");
            for (int c = 0; c < MazeCols; c++)
                for (int r = 0; r <= MazeRows; r++)
                {
                    float cx = MazeX0 + (c + 0.5f) * T, z = MazeZ0 + r * T;
                    if (r == MazeRows && c == MazeExitCol)
                    {
                        MazeBlock(root, new Vector3(cx - 1.875f, y0 + 2.5f, z), new Vector3(1.25f, 5f, MazeThick), stone);
                        MazeBlock(root, new Vector3(cx + 1.875f, y0 + 2.5f, z), new Vector3(1.25f, 5f, MazeThick), stone);
                        MazeBlock(root, new Vector3(cx, y0 + 3.75f, z), new Vector3(2.5f, 2.5f, MazeThick), stone);
                        continue;
                    }
                    if (h[c, r]) MazeBlock(root, new Vector3(cx, y0 + 2.5f, z), new Vector3(T + MazeThick, 5f, MazeThick), stone);
                }
            for (int c = 0; c <= MazeCols; c++)
                for (int r = 0; r < MazeRows; r++)
                    if (v[c, r])
                        MazeBlock(root, new Vector3(MazeX0 + c * T, y0 + 2.5f, MazeZ0 + (r + 0.5f) * T), new Vector3(MazeThick, 5f, T + MazeThick), stone);

            var map = root.gameObject.GetComponent<LabyrinthMap>();
            if (map == null) map = root.gameObject.AddComponent<LabyrinthMap>();
            map.cols = MazeCols; map.rows = MazeRows; map.x0 = MazeX0; map.z0 = MazeZ0; map.cell = T;
            map.startCell = new Vector2Int(0, 0);
            map.exitCell = new Vector2Int(MazeExitCol, MazeRows - 1);
            map.hWalls = new bool[MazeCols * (MazeRows + 1)];
            map.vWalls = new bool[(MazeCols + 1) * MazeRows];
            for (int c = 0; c < MazeCols; c++) for (int r = 0; r <= MazeRows; r++) map.hWalls[c + r * MazeCols] = h[c, r];
            for (int c = 0; c <= MazeCols; c++) for (int r = 0; r < MazeRows; r++) map.vWalls[c + r * (MazeCols + 1)] = v[c, r];

            var route = map.ComputeRoute();
            if (route.Count > 0)
            {
                Vector2Int pc = route[Mathf.Min(route.Count - 1, 7)];
                var papyrus = Go("Papiro do Labirinto", root);
                papyrus.transform.position = new Vector3(MazeX0 + (pc.x + 0.5f) * T, y0, MazeZ0 + (pc.y + 0.5f) * T);
                var mp = papyrus.AddComponent<MazePapyrus>();
                mp.model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Movels/paper/source/old_papers.fbx");
                mp.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Movels/paper/textures/texture_pbr_20250901.png");

                int n = route.Count;
                var spots = new List<Vector2Int>();
                foreach (float f in new[] { 0.3f, 0.55f, 0.8f })
                {
                    Vector2Int s = route[Mathf.Clamp(Mathf.RoundToInt(n * f), 2, Mathf.Max(2, n - 3))];
                    if (s != pc && !spots.Contains(s)) spots.Add(s);
                }
                var mrnd = new System.Random(77);
                for (int tries = 0; tries < 300; tries++)
                {
                    var c = new Vector2Int(mrnd.Next(MazeCols), mrnd.Next(MazeRows));
                    if (c.y < 3 || route.Contains(c) || spots.Contains(c)) continue;
                    spots.Add(c);
                    break;
                }
                for (int i = 0; i < spots.Count; i++) SpawnMazeMummy(root, spots[i], y0, i + 1);
            }
        }

        static void SpawnMazeMummy(Transform root, Vector2Int cellPos, float y0, int number)
        {
            var g = Go("Múmia do Labirinto " + number, root);
            g.transform.position = new Vector3(MazeX0 + (cellPos.x + 0.5f) * T, y0, MazeZ0 + (cellPos.y + 0.5f) * T);
            g.AddComponent<CharacterController>();
            var m = g.AddComponent<MummyGuardian>();
            m.displayName = "Múmia";
            m.maxHealth = 220f;
            m.moveSpeed = 2.2f;
            m.awakenRange = 8f;
            m.attackDamage = 12f;
            g.AddComponent<MazeMummy>();
        }

        const float MazeThick = 0.4f;

        static void MazeBlock(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject b = Cube(parent, pos, scale, mat);
            b.name = "ParedeLabirinto";
            b.isStatic = true;
        }

        static void MenuBuildLabyrinth()
        {
            GameObject rootGo = GameObject.Find("Ruinas do Deserto");
            if (rootGo == null)
            {
                EditorUtility.DisplayDialog("Labirinto", "Abra a cena 03_Fase2_RuinasDoDeserto (não achei o objeto 'Ruinas do Deserto').", "OK");
                return;
            }
            Transform old = rootGo.transform.Find("Labirinto");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            GameObject maze = Go("Labirinto", rootGo.transform);
            BuildLabyrinth(maze.transform, 0.2f);

            foreach (Transform t in rootGo.transform)
                if (t.name.StartsWith("Column") || t.name.StartsWith("Anubis") || t.name.StartsWith("Rock_")) t.gameObject.SetActive(false);
            GameObject pyr = GameObject.Find("Pyramid");
            if (pyr != null) pyr.SetActive(false);

            SetAmbient(new Color(0.22f, 0.19f, 0.16f));
            SetFog(false, new Color(0.86f, 0.74f, 0.55f), 0f);
            GameObject sol = GameObject.Find("Sol");
            if (sol != null && sol.GetComponent<Light>() != null) sol.GetComponent<Light>().intensity = 0.35f;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) { player.transform.position = MazeStart; player.transform.rotation = Quaternion.identity; }

            EditorSceneManager.MarkSceneDirty(rootGo.scene);
            Debug.Log("[RiftOfTime] Labirinto construído. Salve a cena (Ctrl+S).");
        }

        static void BuildChamberComplex(Transform root, bool full)
        {
            BuildRoom(root, -10f, 0f, 4, 4, 0f, full ? new[] { "E2" } : null, null, true, null);
            Column(root, -8f, 3f, 0f); Column(root, -8f, 17f, 0f);
            Column(root, 8f, 3f, 0f); Column(root, 8f, 17f, 0f);
            Torch(root, -9.3f, 2.4f, 10f, 90f);
            PointLight(root, new Vector3(-5f, 4f, 5f), Warm, 4f, 14f);
            PointLight(root, new Vector3(5f, 4f, 15f), Warm, 4f, 14f);

            if (!full) return;

            BuildRoom(root, 10f, 10f, 8, 1, 0f, null, new[] { "W0", "E0" }, true, null);
            for (int i = 0; i < 4; i++)
                PointLight(root, new Vector3(15f + i * 10f, 4f, 12.5f), Warm, 3.5f, 11f);
            for (int i = 0; i < 4; i++)
                Torch(root, 15f + i * 10f, 2.4f, 10.4f, 0f);

            BuildRoom(root, 50f, 0f, 6, 6, 0f, new[] { "W2" }, null, true, null);
            Column(root, 58f, 6f, 0f); Column(root, 58f, 24f, 0f);
            Column(root, 68f, 6f, 0f); Column(root, 68f, 24f, 0f);
            PointLight(root, new Vector3(60f, 4f, 8f), Warm, 5f, 16f);
            PointLight(root, new Vector3(60f, 4f, 22f), Warm, 5f, 16f);
            PointLight(root, new Vector3(72f, 4f, 15f), Warm, 6f, 16f);
            Place("Horus_statue", new Vector3(77f, 0f, 6f), Anchor.Bottom, -90f, root);
            Place("Horus_statue", new Vector3(77f, 0f, 24f), Anchor.Bottom, -90f, root);
        }

        static string BuildFase3()
        {
            var sc = NewScene();
            SetAmbient(new Color(0.28f, 0.22f, 0.17f));
            SetFog(false, Color.black, 0f);
            var root = Go("Camara do Tempo").transform;
            BuildChamberComplex(root, true);

            TimeMachine machine = MakeMachine(root, new Vector3(-5f, 0f, 10f), -90f);
            Vector3 altarTop = Altar(root, 76f, 0f, 15f);
            FragmentPickup fragment = MakeFragment(root, altarTop, 3);

            PlayerAt(new Vector3(3f, 1.2f, 6f), 90f);

            var cgo = Go("Colapso");
            var collapse = cgo.AddComponent<CollapseController>();
            collapse.duration = 180f;
            collapse.rockPrefab = LoadPrefab("Rock_1");

            MakeVolume();
            var pc = Go("PhaseController").AddComponent<PhaseController>();
            pc.phase = PhaseKind.Fase3;
            pc.fragment = fragment;
            pc.machine = machine;
            pc.collapse = collapse;
            return Save(sc, "04_Fase3_CamaraDoTempo");
        }

        static string BuildFinal()
        {
            var sc = NewScene();
            SetAmbient(new Color(0.28f, 0.22f, 0.17f));
            SetFog(false, Color.black, 0f);
            var root = Go("Camara do Tempo").transform;
            BuildChamberComplex(root, false);

            TimeMachine machine = MakeMachine(root, new Vector3(0f, 0f, 11f), 0f);
            PlayerAt(new Vector3(0f, 0.15f, 5f), 0f);

            var cgo = Go("Colapso");
            var collapse = cgo.AddComponent<CollapseController>();
            collapse.decorOnly = true;
            collapse.rockPrefab = LoadPrefab("Rock_1");

            MakeVolume();
            var pc = Go("PhaseController").AddComponent<PhaseController>();
            pc.phase = PhaseKind.Final;
            pc.machine = machine;
            pc.collapse = collapse;
            return Save(sc, "05_Final");
        }

        static void UpdateBuildSettings(List<string> gameScenes)
        {
            var list = new List<EditorBuildSettingsScene>();
            foreach (string p in gameScenes) list.Add(new EditorBuildSettingsScene(p, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        static void AppendToBuildSettings(string scenePath)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in list) if (s.path == scenePath) return;
            list.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }

    /// <summary>Na primeira abertura do projeto (sem cenas geradas) oferece construir o jogo.</summary>
    [InitializeOnLoad]
    static class RiftOfTimeWelcome
    {
        static RiftOfTimeWelcome()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (SessionState.GetBool("ROT_Welcomed", false)) return;
                SessionState.SetBool("ROT_Welcomed", true);
                if (File.Exists("Assets/Scenes/FragmentsOfTime/00_Menu.unity")) return;
                if (EditorUtility.DisplayDialog("Rift Of Time",
                    "As cenas do jogo ainda não foram criadas.\nQuer construir tudo agora? (leva alguns segundos)\n\n" +
                    "Você também pode fazer isso depois em: Rift Of Time > 1. Construir todas as cenas.",
                    "Construir agora", "Depois"))
                    RiftOfTimeBuilder.BuildAll();
            };
        }
    }
}
