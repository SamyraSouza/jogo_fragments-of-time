using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RiftOfTime.EditorTools
{
    /// <summary>Instala os braços em primeira pessoa colocados em Assets/Characters/Arms como filho "Arms" da câmera do Player.prefab.</summary>
    public static class ArmsInstaller
    {
        const string Dir = "Assets/Characters/Arms";
        const string PlayerPrefab = "Assets/Ancient Modular Tombs/Prefabs/Player.prefab";
        const string ControllerPath = Dir + "/ArmsController.controller";
        const string FallbackClipPath = Dir + "/Arms_Attack_Fallback.anim";
        const string MaterialsDir = Dir + "/Materials";
        const float BrightnessBoost = 2.5f;
        const float AttackLength = 0.45f;

        public static void Install()
        {
            if (FindModel() == null)
            {
                EditorUtility.DisplayDialog("Rift Of Time", "Coloque o modelo (.fbx ou .glb) em " + Dir + " e tente de novo.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            bool ok = Attach(root);
            if (ok) PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            PrefabUtility.UnloadPrefabContents(root);

            string original = EditorSceneManager.GetActiveScene().path;
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                var scene = EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(guid), OpenSceneMode.Single);
                bool dirty = false;
                foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var stray = cam.transform.Find("Arms");
                    if (stray != null && PrefabUtility.GetOutermostPrefabInstanceRoot(stray.gameObject) == stray.gameObject)
                    {
                        Object.DestroyImmediate(stray.gameObject);
                        dirty = true;
                    }
                }
                if (dirty) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            }
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);

            EditorUtility.DisplayDialog("Rift Of Time",
                ok ? "Braços colocados no Player.prefab (vale para todas as cenas)." : "Falha ao colocar os braços (Player.prefab sem câmera?).", "OK");
        }

        static string FindModel()
        {
            if (!Directory.Exists(Dir)) return null;
            string[] exts = { ".fbx", ".glb", ".gltf", ".obj" };
            return Directory.GetFiles(Dir, "*", SearchOption.AllDirectories)
                .Select(f => f.Replace('\\', '/'))
                .FirstOrDefault(p => exts.Contains(Path.GetExtension(p).ToLower()));
        }

        static Transform NewHolder(Transform parent, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var t = new GameObject("Arms").transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = rot;
            t.localScale = scale;
            return t;
        }

        /// <summary>Coloca (ou recoloca) o filho "Arms" na câmera do player.</summary>
        static bool Attach(GameObject player)
        {
            string modelPath = FindModel();
            var cam = player != null ? player.GetComponentInChildren<Camera>(true) : null;
            if (modelPath == null || cam == null) return false;

            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer != null && importer.animationType == ModelImporterAnimationType.None)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.SaveAndReimport();
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (prefab == null) return false;

            var holder = cam.transform.Find("Arms");
            if (holder != null && holder.Find("Model") == null)
            {
                Vector3 p0 = holder.localPosition, s0 = holder.localScale;
                Quaternion r0 = holder.localRotation;
                Object.DestroyImmediate(holder.gameObject);
                holder = NewHolder(cam.transform, p0, r0, s0);
            }
            else if (holder == null)
            {
                holder = NewHolder(cam.transform, new Vector3(0.1f, -0.25f, 0.5f), Quaternion.identity, Vector3.one);
            }
            if (holder.Find("ArmsLight") == null)
            {
                var lg = new GameObject("ArmsLight");
                lg.transform.SetParent(holder, false);
                lg.transform.localPosition = new Vector3(0f, 0.3f, -0.4f);
                var light = lg.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.95f, 0.88f);
                light.intensity = 3f;
                light.range = 3f;
                light.shadows = LightShadows.None;
            }
            var oldModel = holder.Find("Model");
            if (oldModel != null) Object.DestroyImmediate(oldModel.gameObject);

            var arms = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
            arms.name = "Model";
            arms.transform.localPosition = Vector3.zero;
            arms.transform.localRotation = Quaternion.identity;
            arms.transform.localScale = Vector3.one;

            foreach (var r in arms.GetComponentsInChildren<Renderer>(true))
            {
                string n = (r.name + " " + string.Join(" ", r.sharedMaterials.Select(m => m != null ? m.name : "null"))).ToLower();
                bool keep = n.Contains("arm") || n.Contains("glove") || n.Contains("sleeve") || n.Contains("hand");
                Debug.Log("[Arms] renderer '" + r.name + "' tamanho " + r.bounds.size.ToString("F2") + (keep ? "" : "  -> DESLIGADO"));
                if (!keep) r.enabled = false;
            }

            foreach (var smr in arms.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
            foreach (var c in arms.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            ConvertMaterials(arms);

            var anim = arms.GetComponent<Animator>();
            if (anim == null) anim = arms.AddComponent<Animator>();
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.runtimeAnimatorController = BuildController(modelPath, Vector3.zero, Vector3.zero);

            cam.nearClipPlane = Mathf.Min(cam.nearClipPlane, 0.01f);
            return true;
        }

        static void ConvertMaterials(GameObject go)
        {
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp == null) return;
            if (!AssetDatabase.IsValidFolder(MaterialsDir)) AssetDatabase.CreateFolder(Dir, "Materials");

            var texPaths = AssetDatabase.FindAssets("t:Texture2D", new[] { Dir }).Select(AssetDatabase.GUIDToAssetPath).ToArray();

            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string src = mats[i] != null ? mats[i].name : "";
                    string key = (src + " " + r.name).ToLower().Contains("glove") ? "glove" : "arm";
                    string safe = key + "_" + string.Concat(src.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
                    string path = MaterialsDir + "/" + safe + "_URP.mat";
                    AssetDatabase.DeleteAsset(path);

                    var made = new Material(urp) { name = safe + "_URP" };
                    var color = FindTexture(texPaths, key, "color");
                    if (color != null)
                    {
                        made.SetTexture("_BaseMap", color);
                        made.SetColor("_BaseColor", new Color(BrightnessBoost, BrightnessBoost, BrightnessBoost));
                        made.EnableKeyword("_EMISSION");
                        made.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                        made.SetTexture("_EmissionMap", color);
                        made.SetColor("_EmissionColor", new Color(1.1f, 1.1f, 1.1f));
                    }
                    var normal = FindTexture(texPaths, key, "normal");
                    if (normal != null)
                    {
                        string np = AssetDatabase.GetAssetPath(normal);
                        var ti = AssetImporter.GetAtPath(np) as TextureImporter;
                        if (ti != null && ti.textureType != TextureImporterType.NormalMap)
                        {
                            ti.textureType = TextureImporterType.NormalMap;
                            ti.SaveAndReimport();
                        }
                        made.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(np));
                        made.EnableKeyword("_NORMALMAP");
                    }
                    made.SetFloat("_Smoothness", 0.1f);
                    AssetDatabase.CreateAsset(made, path);
                    mats[i] = made;
                }
                r.sharedMaterials = mats;
                r.receiveShadows = false;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        static Texture2D FindTexture(string[] paths, string key, string kind)
        {
            string t = paths.FirstOrDefault(p =>
            {
                string n = Path.GetFileNameWithoutExtension(p).ToLower();
                return n.Contains(key) && n.Contains(kind);
            });
            return t != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(t) : null;
        }

        static AnimatorController BuildController(string modelPath, Vector3 restPos, Vector3 restEuler)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = ctrl.layers[0].stateMachine;

            var idle = sm.AddState("Idle");
            idle.motion = MakeIdleClip(restPos, restEuler);
            sm.defaultState = idle;

            var attack = sm.AddState("Attack");
            attack.motion = PickAttackClip(modelPath) ?? MakeFallbackClip(restPos, restEuler);
            var back = attack.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 1f;
            back.duration = 0.05f;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        static AnimationClip PickAttackClip(string modelPath)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToArray();
            if (clips.Length == 0) return null;
            string[] keys = { "attack", "slash", "stab", "swing", "hit", "melee", "knife", "fire", "shoot", "punch" };
            return clips.FirstOrDefault(c => keys.Any(k => c.name.ToLower().Contains(k))) ?? clips[0];
        }

        static AnimationClip MakeIdleClip(Vector3 p, Vector3 e)
        {
            const float L = 3.2f;
            var clip = new AnimationClip { name = "Arms_Idle", frameRate = 60f };
            void Wave(string prop, float baseV, float amp, float phase)
            {
                var keys = new Keyframe[9];
                for (int i = 0; i < keys.Length; i++)
                {
                    float t = i / (float)(keys.Length - 1);
                    keys[i] = new Keyframe(t * L, baseV + Mathf.Sin((t + phase) * Mathf.PI * 2f) * amp);
                }
                var c = new AnimationCurve(keys);
                for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
                clip.SetCurve("", typeof(Transform), prop, c);
            }
            Wave("localPosition.x", p.x, 0.006f, 0.25f);
            Wave("localPosition.y", p.y, 0.010f, 0f);
            Wave("localPosition.z", p.z, 0.006f, 0.15f);
            Wave("localEulerAnglesRaw.x", e.x, 1.2f, 0.1f);
            Wave("localEulerAnglesRaw.y", e.y, 1.0f, 0.35f);
            Wave("localEulerAnglesRaw.z", e.z, 0.8f, 0.2f);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            const string path = Dir + "/Arms_Idle.anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        static AnimationClip MakeFallbackClip(Vector3 p, Vector3 e)
        {
            var clip = new AnimationClip { name = "Arms_Attack_Fallback", frameRate = 60f };
            void Curve(string prop, float v0, float v1, float v2, float v3)
            {
                clip.SetCurve("", typeof(Transform), prop, new AnimationCurve(
                    new Keyframe(0f, v0), new Keyframe(0.12f, v1), new Keyframe(0.25f, v2), new Keyframe(AttackLength, v3)));
            }
            Curve("localPosition.x", p.x, p.x + 0.10f, p.x - 0.08f, p.x);
            Curve("localPosition.y", p.y, p.y + 0.12f, p.y + 0.02f, p.y);
            Curve("localPosition.z", p.z, p.z - 0.08f, p.z + 0.22f, p.z);
            Curve("localEulerAnglesRaw.x", e.x, e.x - 25f, e.x + 12f, e.x);
            Curve("localEulerAnglesRaw.y", e.y, e.y + 20f, e.y - 30f, e.y);
            Curve("localEulerAnglesRaw.z", e.z, e.z - 10f, e.z + 15f, e.z);
            AssetDatabase.DeleteAsset(FallbackClipPath);
            AssetDatabase.CreateAsset(clip, FallbackClipPath);
            return clip;
        }
    }
}
