using UnityEditor;

namespace RiftOfTime
{
    /// <summary>Atalho de teste: no Editor, a Fase 2 já começa diante da porta da câmara final.</summary>
    public static class Fase2StartAtDoor
    {
        const string Key = "RoT_Fase2NaPorta";
        const string Menu = "Rift Of Time/Teste: pular direto para a porta da câmara da Fase 2";

        [MenuItem(Menu)]
        static void Toggle() { EditorPrefs.SetBool(Key, !EditorPrefs.GetBool(Key, true)); }

        [MenuItem(Menu, true)]
        static bool Validate() { Menu_Check(); return true; }

        static void Menu_Check() { Menu_SetChecked(EditorPrefs.GetBool(Key, true)); }
        static void Menu_SetChecked(bool on) { UnityEditor.Menu.SetChecked(Menu, on); }
    }
}
