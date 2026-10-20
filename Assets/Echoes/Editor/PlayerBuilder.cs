using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menú "Echoes > Crear Player en la escena": construye el jugador en primera persona
/// (solo cámara, sin modelo), lo guarda como prefab y lo coloca sobre el terreno.
/// </summary>
public static class PlayerBuilder
{
    const string Root = "Assets/Echoes";
    const string PrefabsFolder = Root + "/Prefabs";
    const string PrefabPath = PrefabsFolder + "/Player.prefab";

    [MenuItem("Echoes/Crear Player en la escena")]
    public static void CreatePlayerInScene()
    {
        EnsureFolder(Root);
        EnsureFolder(PrefabsFolder);

        GameObject built = BuildPlayer();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(built, PrefabPath);
        Object.DestroyImmediate(built);

        var old = GameObject.Find("Player");
        if (old != null) Undo.DestroyObjectImmediate(old);

        var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(player, "Crear Player");
        player.transform.position = SpawnPosition();

        // Desactiva otras cámaras para que la del jugador sea la única.
        foreach (var cam in Camera.allCameras)
        {
            if (cam.gameObject.scene != player.scene || cam.transform.IsChildOf(player.transform)) continue;
            Undo.RecordObject(cam.gameObject, "Desactivar cámara");
            cam.gameObject.SetActive(false);
            Debug.Log($"[Echoes] Cámara '{cam.name}' desactivada: ahora se usa la cámara del Player.");
        }

        Selection.activeGameObject = player;
        EditorSceneManager.MarkSceneDirty(player.scene);
        Debug.Log($"[Echoes] Player creado en {player.transform.position}. Prefab: {PrefabPath}. Guarda la escena (Ctrl+S) y pulsa Play.");
    }

    static Vector3 SpawnPosition()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("[Echoes] No se encontró un Terrain activo; el Player se coloca en (0, 2, 0).");
            return new Vector3(0f, 2f, 0f);
        }

        // Si la vista de escena está mirando al terreno, aparece ahí; si no, en el centro del terreno.
        Vector3 tPos = terrain.GetPosition();
        Vector3 size = terrain.terrainData.size;
        Vector3 p = tPos + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        var view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            Vector3 pivot = view.pivot;
            if (pivot.x > tPos.x && pivot.x < tPos.x + size.x && pivot.z > tPos.z && pivot.z < tPos.z + size.z)
                p = pivot;
        }
        p.y = terrain.SampleHeight(p) + tPos.y + 0.1f;
        return p;
    }

    static GameObject BuildPlayer()
    {
        var root = new GameObject("Player") { tag = "Player" };
        root.AddComponent<CharacterController>().slopeLimit = 50f;

        var pivot = new GameObject("CameraPivot").transform;
        pivot.SetParent(root.transform, false);

        var camGo = new GameObject("PlayerCamera") { tag = "MainCamera" };
        camGo.transform.SetParent(pivot, false);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 2000f;
        cam.fieldOfView = 70f;
        camGo.AddComponent<AudioListener>();

        var controller = root.AddComponent<PlayerController>();
        controller.cameraPivot = pivot;
        controller.playerCamera = cam;
        controller.ApplyScale();
        SetupAudio(root);

        return root;
    }

    /// <summary>Añade (o completa) el PlayerAudio con los clips de Assets/Audio.</summary>
    public static bool SetupAudio(GameObject player)
    {
        var audio = player.GetComponent<PlayerAudio>();
        bool changed = audio == null;
        if (changed) audio = player.AddComponent<PlayerAudio>();

        changed |= AssignClip(ref audio.footstepsClip, "Pasos.mp3");
        changed |= AssignClip(ref audio.ambientClip, "music ambiental.mp3");
        changed |= AssignClip(ref audio.musicClip, "musica-background.mp3");
        changed |= AssignClip(ref audio.roarClip, "gruñidodinosaurio.mp3");
        return changed;
    }

    static bool AssignClip(ref AudioClip field, string file)
    {
        if (field != null) return false;
        field = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + file);
        if (field == null) Debug.LogWarning($"[Echoes] No se encontró Assets/Audio/{file}");
        return field != null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
    }
}
