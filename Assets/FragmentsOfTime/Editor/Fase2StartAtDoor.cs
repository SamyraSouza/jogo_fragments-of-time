using UnityEditor;

namespace RiftOfTime
{
    /// <summary>Atalho de teste (desligado por padrão): no Editor, a Fase 1 é pulada e a Fase 2 já começa diante da porta da câmara final.</summary>
    public static class Fase2StartAtDoor
    {
        const string Key = PhaseController.TestKeyFase2AtDoor;
        const string Menu = "Rift Of Time/Teste: pular direto para a porta da câmara da Fase 2";

        [MenuItem(Menu)]
        static void Toggle() { EditorPrefs.SetBool(Key, !EditorPrefs.GetBool(Key, false)); }

        [MenuItem(Menu, true)]
        static bool Validate() { Menu_Check(); return true; }

        static void Menu_Check() { Menu_SetChecked(EditorPrefs.GetBool(Key, false)); }
        static void Menu_SetChecked(bool on) { UnityEditor.Menu.SetChecked(Menu, on); }
    }
}
