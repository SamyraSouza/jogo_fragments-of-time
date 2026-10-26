using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RiftOfTime.EditorTools
{
    /// <summary>Tira as portas automáticas da cena e deixa só o vão.</summary>
    public static class RemoveAutoDoors
    {
        const string ArchPrefab = "Assets/Ancient Modular Tombs/Prefabs/Door_arch.prefab";

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var arch = AssetDatabase.LoadAssetAtPath<GameObject>(ArchPrefab);
            int large = 0, gates = 0;

            foreach (var door in Object.FindObjectsByType<Large_door_demo>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                GameObject go = door.gameObject;
                Transform leaf = go.transform.Find("Stone_door");
                Undo.DestroyObjectImmediate(door);
                var audio = go.GetComponent<AudioSource>();
                if (audio != null) Undo.DestroyObjectImmediate(audio);
                var anim = go.GetComponent<Animator>();
                if (anim != null) Undo.DestroyObjectImmediate(anim);
                foreach (var box in go.GetComponents<BoxCollider>().Where(b => b.isTrigger).ToArray()) Undo.DestroyObjectImmediate(box);
                if (leaf != null) Undo.DestroyObjectImmediate(leaf.gameObject);
                EditorSceneManager.MarkSceneDirty(go.scene);
                large++;
            }

            foreach (var trap in Object.FindObjectsByType<Stone_gate_trap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                GameObject old = trap.gameObject;
                if (arch != null)
                {
                    var fresh = (GameObject)PrefabUtility.InstantiatePrefab(arch, old.scene);
                    Undo.RegisterCreatedObjectUndo(fresh, "Trocar portão por arco");
                    fresh.transform.SetParent(old.transform.parent, false);
                    fresh.transform.localPosition = old.transform.localPosition;
                    fresh.transform.localRotation = old.transform.localRotation;
                    fresh.transform.localScale = old.transform.localScale;
                    fresh.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
                    fresh.name = "Door_arch";
                }
                EditorSceneManager.MarkSceneDirty(old.scene);
                Undo.DestroyObjectImmediate(old);
                gates++;
            }

            string msg = "Porta grande: " + large + " tratada(s). Portões em arco: " + gates + " trocado(s) por arco simples.";
            Debug.Log("[Portas] " + msg);
            EditorUtility.DisplayDialog("Portas", msg + (large + gates > 0 ? "\nSalve a cena (Ctrl+S)." : "\nNão achei nenhuma nesta cena."), "OK");
        }
    }
}
