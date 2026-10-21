using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Menú "Echoes > Kata S10: Habitación emocional": crea la escena de la kata, con un cubo en una
/// habitación y tres perfiles de iluminación (Terror, Calma, Acción) que cambian la emoción sin
/// tocar la geometría. También guarda una captura de cada perfil en Docs/Capturas.
/// </summary>
public static class EmotionalRoomBuilder
{
    const string ScenePath = "Assets/Echoes/Scenes/Kata_HabitacionEmocional.unity";
    const string ProfilesFolder = "Assets/Echoes/Lighting/Kata";

    [MenuItem("Echoes/Kata S10: Habitación emocional")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    public static void Build()
    {
        VolumeProfiles.EnsureFolder("Assets/Echoes/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;

        // Geometría fija: suelo, tres paredes y el cubo. Nada de esto cambia entre perfiles.
        var room = new GameObject("Habitación").transform;
        Box(room, "Suelo", new Vector3(0f, -0.05f, 0f), new Vector3(12f, 0.1f, 12f));
        Box(room, "Pared fondo", new Vector3(0f, 3f, 6f), new Vector3(12f, 6f, 0.2f));
        Box(room, "Pared izquierda", new Vector3(-6f, 3f, 0f), new Vector3(0.2f, 6f, 12f));
        Box(room, "Pared derecha", new Vector3(6f, 3f, 0f), new Vector3(0.2f, 6f, 12f));
        var cube = Box(room, "Cubo", new Vector3(0f, 0.75f, 0f), Vector3.one * 1.5f);
        cube.transform.rotation = Quaternion.Euler(0f, 35f, 0f);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        camGo.transform.position = new Vector3(0f, 2.2f, -7f);
        camGo.transform.LookAt(new Vector3(0f, 1f, 0f));
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

        var lighting = new GameObject("-- LIGHTING --").transform;
        var controller = lighting.gameObject.AddComponent<EmotionalRoom>();
        controller.profiles = new[] { Terror(lighting), Calm(lighting), Action(lighting) };
        controller.current = 1;

        // Una captura por perfil, mismo encuadre.
        Directory.CreateDirectory("Docs/Capturas");
        for (int i = 0; i < controller.profiles.Length; i++)
        {
            controller.Apply(i);
            Capture(cam, $"Docs/Capturas/Kata_{i + 1}_{controller.profiles[i].name}.png");
        }
        controller.Apply(controller.current);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Debug.Log($"[Echoes] Kata creada en {ScenePath}. En Play, 1/2/3 cambian entre Terror, Calma y Acción.");
    }

    // Terror: luz fría desde abajo (deforma la cara del objeto), contra roja, casi sin ambiente.
    static EmotionalRoom.Profile Terror(Transform parent)
    {
        var root = Group(parent, "Perfil Terror");
        Spot(root, "Key desde abajo", new Vector3(0f, 0.15f, -2.4f), new Vector3(0f, 1.2f, 0f), new Color(0.55f, 0.9f, 0.75f), 18f, 55f, LightShadows.Hard);
        Point(root, "Contra roja", new Vector3(0.6f, 1.2f, 2.2f), new Color(0.85f, 0.05f, 0.05f), 4f, 4f);
        AddVolume(root, "Terror", p =>
        {
            VolumeProfiles.Tonemapping(p);
            VolumeProfiles.ColorAdjustments(p, -0.4f, 45f, -60f, new Color(0.82f, 1f, 0.95f));
            VolumeProfiles.Vignette(p, 0.55f, 0.5f, Color.black);
            VolumeProfiles.Bloom(p, 0.3f, 1f, 0.5f, Color.white);
            p.Add<FilmGrain>(true).intensity.Override(0.5f);
        });
        return new EmotionalRoom.Profile { name = "Terror", root = root, ambient = new Color(0.02f, 0.02f, 0.03f) };
    }

    // Calma: luz cálida y amplia desde arriba como una ventana, relleno azul suave, poco contraste.
    static EmotionalRoom.Profile Calm(Transform parent)
    {
        var root = Group(parent, "Perfil Calma");
        Spot(root, "Key ventana", new Vector3(-3f, 5.5f, -2.5f), new Vector3(0f, 0.5f, 0f), new Color(1f, 0.86f, 0.68f), 45f, 100f, LightShadows.Soft);
        Point(root, "Relleno cielo", new Vector3(3.5f, 2.5f, -3f), new Color(0.6f, 0.75f, 1f), 1.5f, 12f);
        AddVolume(root, "Calma", p =>
        {
            VolumeProfiles.Tonemapping(p);
            VolumeProfiles.ColorAdjustments(p, 0.2f, -12f, 5f, new Color(1f, 0.97f, 0.92f));
            VolumeProfiles.Vignette(p, 0.15f, 0.6f, Color.black);
            VolumeProfiles.Bloom(p, 0.6f, 0.9f, 0.8f, new Color(1f, 0.9f, 0.8f));
        });
        return new EmotionalRoom.Profile { name = "Calma", root = root, ambient = new Color(0.3f, 0.29f, 0.27f) };
    }

    // Acción: dos luces duras de colores opuestos (naranja/cian), mucho contraste y saturación.
    static EmotionalRoom.Profile Action(Transform parent)
    {
        var root = Group(parent, "Perfil Acción");
        Spot(root, "Key naranja", new Vector3(4f, 3.5f, -2.5f), new Vector3(0f, 0.8f, 0f), new Color(1f, 0.35f, 0.08f), 80f, 45f, LightShadows.Hard);
        Spot(root, "Contra cian", new Vector3(-3.5f, 3f, 3.5f), new Vector3(0f, 0.8f, 0f), new Color(0.1f, 0.8f, 1f), 70f, 40f, LightShadows.Hard);
        AddVolume(root, "Accion", p =>
        {
            VolumeProfiles.Tonemapping(p);
            VolumeProfiles.ColorAdjustments(p, 0.1f, 40f, 35f, Color.white);
            VolumeProfiles.Vignette(p, 0.3f, 0.4f, new Color(0.1f, 0f, 0f));
            VolumeProfiles.Bloom(p, 1.2f, 0.8f, 0.7f, Color.white);
            p.Add<ChromaticAberration>(true).intensity.Override(0.35f);
        });
        return new EmotionalRoom.Profile { name = "Acción", root = root, ambient = new Color(0.06f, 0.03f, 0.03f) };
    }

    static GameObject Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = position;
        box.transform.localScale = scale;
        return box;
    }

    static void Spot(GameObject parent, string name, Vector3 position, Vector3 target, Color color, float intensity, float angle, LightShadows shadows)
    {
        var light = new GameObject(name).AddComponent<Light>();
        light.transform.SetParent(parent.transform, false);
        light.transform.position = position;
        light.transform.LookAt(target);
        light.type = LightType.Spot;
        light.color = color;
        light.intensity = intensity;
        light.range = 15f;
        light.spotAngle = angle;
        light.innerSpotAngle = angle * 0.5f;
        light.shadows = shadows;
    }

    static void Point(GameObject parent, string name, Vector3 position, Color color, float intensity, float range)
    {
        var light = new GameObject(name).AddComponent<Light>();
        light.transform.SetParent(parent.transform, false);
        light.transform.position = position;
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
    }

    static void AddVolume(GameObject parent, string name, System.Action<VolumeProfile> fill)
    {
        var volume = parent.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = VolumeProfiles.Create($"{ProfilesFolder}/Kata_{name}.asset", fill);
    }

    static void Capture(Camera cam, string file)
    {
        const int w = 1280, h = 720;
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render(); // calentamiento: el primer frame tras crear la escena puede salir sin shaders
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        RenderTexture.active = prev;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
    }

    static void AddToBuildSettings(string path)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
