using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Al recompilar, elimina el cuerpo y los brazos ("Body", "ViewModel") que dejó la primera
/// versión del Player, tanto en el prefab como en las escenas abiertas, y aplica la escala.
/// </summary>
[InitializeOnLoad]
static class PlayerCleanup
{
    static readonly string[] OldParts = { "Body", "ViewModel" };
    const string PrefabPath = "Assets/Echoes/Prefabs/Player.prefab";

    static PlayerCleanup() => EditorApplication.delayCall += Run;

    [MenuItem("Echoes/Actualizar Player (sonido, quitar brazos)")]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            Clean(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        foreach (var pc in Resources.FindObjectsOfTypeAll<PlayerController>())
        {
            if (EditorUtility.IsPersistent(pc) || !pc.gameObject.scene.IsValid()) continue;
            if (Clean(pc.gameObject)) EditorSceneManager.MarkSceneDirty(pc.gameObject.scene);
        }
    }

    static bool Clean(GameObject player)
    {
        bool changed = false;
        foreach (var t in player.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || System.Array.IndexOf(OldParts, t.name) < 0) continue;
            if (PrefabUtility.IsPartOfPrefabInstance(t) && !PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)
                && PrefabUtility.GetCorrespondingObjectFromSource(t) != null)
            {
                // Hijo heredado del prefab: se elimina en el prefab y desaparece aquí también.
                continue;
            }
            Object.DestroyImmediate(t.gameObject);
            changed = true;
        }

        // Quita componentes "Missing Script" del animador que ya no existe.
        changed |= GameObjectUtility.RemoveMonoBehavioursWithMissingScript(player) > 0;

        if (changed) Debug.Log($"[Echoes] Cuerpo y brazos eliminados de '{player.name}'.");

        // Sonido: en instancias del prefab llega desde el prefab, no hace falta añadirlo.
        if (!PrefabUtility.IsPartOfPrefabInstance(player) && PlayerBuilder.SetupAudio(player))
        {
            Debug.Log($"[Echoes] Sonido configurado en '{player.name}'.");
            changed = true;
        }

        var pc = player.GetComponent<PlayerController>();
        if (pc != null) pc.ApplyScale();
        return changed;
    }
}
