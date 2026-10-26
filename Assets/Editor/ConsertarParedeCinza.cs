using UnityEditor;
using UnityEngine;

public static class ConsertarParedeCinza
{
    const string NomeDoMaterialCinza = "Wall_interior";

    const string CaminhoDoMaterialNovo = "Assets/Ancient Modular Tombs/Materials/Parede_Pedra.mat";

    [MenuItem("Ferramentas/Consertar Parede Cinza")]
    public static void Consertar()
    {
        Material novo = AssetDatabase.LoadAssetAtPath<Material>(CaminhoDoMaterialNovo);
        if (novo == null)
        {
            EditorUtility.DisplayDialog("Ops",
                "Não achei o material Parede_Pedra em:\n" + CaminhoDoMaterialNovo, "OK");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Ancient Modular Tombs" });
        int consertados = 0;

        foreach (string guid in guids)
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(caminho) as ModelImporter;
            if (importer == null) continue;

            bool usaMaterialCinza = false;
            foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(caminho))
            {
                if (obj is Material m && m.name == NomeDoMaterialCinza)
                {
                    usaMaterialCinza = true;
                    break;
                }
            }
            if (!usaMaterialCinza) continue;

            importer.AddRemap(
                new AssetImporter.SourceAssetIdentifier(typeof(Material), NomeDoMaterialCinza), novo);
            importer.SaveAndReimport();
            consertados++;
        }

        EditorUtility.DisplayDialog("Pronto!",
            consertados + " modelo(s) agora usam o Parede_Pedra.", "OK");
    }
}
