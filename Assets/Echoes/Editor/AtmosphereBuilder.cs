using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Menú "Echoes > Iluminación del camino (S10)": monta la narrativa visual del nivel.
///  - Tres capas de luz dentro de "-- LIGHTING --": Key (luna fría), Fill (relleno frío
///    sin sombras + ambiente) y Accent (farolillos a lo largo del camino + luz del objetivo).
///  - El camino es la zona más iluminada: la key está baja y los farolillos lo marcan.
///  - CinemachineCamera siguiendo al Player con Dead Zone y Damping.
///  - Segunda CinemachineCamera que activa un trigger en la zona de tensión.
///  - Global Volume con Tonemapping, Bloom, Vignette y Color Adjustments.
/// Se puede volver a ejecutar: rehace lo que generó sin duplicar.
/// </summary>
public static class AtmosphereBuilder
{
    const string Folder = "Assets/Echoes/Lighting";
    const string AtmosphereProfilePath = Folder + "/Echoes_Atmosphere.asset";
    const string TensionProfilePath = Folder + "/Echoes_Tension.asset";
    const string LanternMaterialPath = Folder + "/Lantern_Glow.mat";
    const string NoisePath = "Packages/com.unity.cinemachine/Presets/Noise/Handheld_normal_strong.asset";

    const string LightingGroup = "-- LIGHTING --";
    const string CamerasGroup = "-- CAMERAS --";

    const float LanternSpacing = 24f;       // metros entre farolillos (el Player mide 4,5 m)
    const float TensionAt = 0.65f;          // posición de la zona de tensión a lo largo del camino (0-1)

    static readonly Color KeyColor = new Color(0.62f, 0.72f, 1f);      // luna
    static readonly Color FillColor = new Color(0.42f, 0.36f, 0.62f);  // rebote violáceo del cielo
    static readonly Color LanternColor = new Color(1f, 0.68f, 0.35f);  // farol
    static readonly Color TensionColor = new Color(1f, 0.25f, 0.15f);  // peligro
    static readonly Color GoalColor = new Color(0.55f, 0.9f, 1f);      // objetivo

    [MenuItem("Echoes/Iluminación del camino (S10)")]
    public static void Build()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("[Echoes] No hay un Terrain activo en la escena.");
            return;
        }

        if (GameObject.Find("Player") == null) PlayerBuilder.CreatePlayerInScene();
        if (GameObject.Find(PathBuilder.PathObjectName) == null) PathBuilder.CreatePath();
        var player = GameObject.Find("Player").GetComponent<PlayerController>();

        var path = PathSamples(terrain);
        if (path.Count < 2) return;
        VolumeProfiles.EnsureFolder(Folder);

        var lighting = FindOrCreateRoot(LightingGroup);
        BuildKeyLight(lighting);
        BuildFillLight(lighting);
        var tensionCenter = PointAlong(path, TensionAt);
        BuildAccentLights(lighting, path, terrain, tensionCenter);
        DisableStrayLights(lighting);

        var tensionCam = BuildCameras(player, tensionCenter);
        BuildTensionZone(tensionCenter, tensionCam);
        BuildGlobalVolume();

        EditorSceneManager.MarkSceneDirty(lighting.gameObject.scene);
        Debug.Log("[Echoes] Iluminación del camino lista: 3 capas en '-- LIGHTING --', cámaras en '-- CAMERAS --', zona de tensión y Global Volume. Guarda la escena (Ctrl+S).");
    }

    /// <summary>Activa/desactiva las luces y el Volume de la semana 10, para la captura "antes".</summary>
    public static void SetAtmosphereEnabled(bool on)
    {
        var lighting = FindInactive(LightingGroup);
        if (lighting != null) lighting.SetActive(on);
        var volume = GameObject.Find("Global Volume");
        if (volume != null) volume.GetComponent<Volume>().enabled = on;
    }

    // ---------- Capas de luz ----------

    static void BuildKeyLight(Transform lighting)
    {
        var key = LayerLight(lighting, "Key Light", LightType.Directional);
        key.transform.rotation = Quaternion.Euler(38f, -35f, 0f); // luna: recorta siluetas y da volumen al relieve
        key.color = KeyColor;
        key.intensity = 0.35f;                                    // baja: el camino (farolillos) tiene que ganar
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.85f;
        RenderSettings.sun = key;
    }

    static void BuildFillLight(Transform lighting)
    {
        var fill = LayerLight(lighting, "Fill Light", LightType.Directional);
        fill.transform.rotation = Quaternion.Euler(35f, 150f, 0f); // contraluz desde el lado opuesto
        fill.color = FillColor;
        fill.intensity = 0.12f;
        fill.shadows = LightShadows.None;

        // El ambiente también es relleno: penumbra azulada para que fuera del camino se vea poco.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.16f, 0.2f, 0.34f);
        RenderSettings.ambientEquatorColor = new Color(0.1f, 0.1f, 0.15f);
        RenderSettings.ambientGroundColor = new Color(0.04f, 0.04f, 0.05f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.07f, 0.08f, 0.13f);
        RenderSettings.fogDensity = 0.0035f;
    }

    static void BuildAccentLights(Transform lighting, List<Vector3> path, Terrain terrain, Vector3 tensionCenter)
    {
        var accent = Child(lighting, "Accent Light");
        ClearChildren(accent);
        var glow = LanternMaterial();

        float distance = 0f, next = 0f;
        int index = 0;
        for (int i = 1; i < path.Count; i++)
        {
            distance += Vector3.Distance(path[i - 1], path[i]);
            if (distance < next || i == path.Count - 1) continue;
            next += LanternSpacing;

            // Alterna los farolillos a cada lado del camino.
            Vector3 dir = (path[i] - path[i - 1]).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, dir) * (index % 2 == 0 ? 1f : -1f) * (PathBuilder.HalfWidth + 2.5f); // fuera del camino
            Vector3 basePos = path[i] + side;
            basePos.y = terrain.SampleHeight(basePos) + terrain.GetPosition().y;

            bool tense = Vector3.Distance(basePos, tensionCenter) < 30f;
            BuildLantern(accent, $"Farol {++index}", basePos, tense ? TensionColor : LanternColor, tense ? 25f : 45f, glow);
        }

        // Luz del objetivo: la más fuerte y de otro color, para que se vea desde lejos.
        Vector3 goal = path[path.Count - 1];
        var goalLight = LayerLight(accent, "Goal Light", LightType.Spot);
        goalLight.transform.position = goal + Vector3.up * 30f;
        goalLight.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        goalLight.color = GoalColor;
        goalLight.intensity = 400f;
        goalLight.range = 60f;
        goalLight.spotAngle = 55f;
        goalLight.innerSpotAngle = 25f;
        goalLight.shadows = LightShadows.Soft;

        var beacon = LayerLight(accent, "Goal Beacon", LightType.Point);
        beacon.transform.position = goal + Vector3.up * 4f;
        beacon.color = GoalColor;
        beacon.intensity = 60f;
        beacon.range = 40f;
        beacon.shadows = LightShadows.None;
    }

    static void BuildLantern(Transform parent, string name, Vector3 basePos, Color color, float intensity, Material glow)
    {
        var lantern = new GameObject(name).transform;
        lantern.SetParent(parent, false);
        lantern.position = basePos;

        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Poste";
        pole.transform.SetParent(lantern, false);
        pole.transform.localPosition = new Vector3(0f, 4f, 0f);
        pole.transform.localScale = new Vector3(0.35f, 4f, 0.35f);

        var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulb.name = "Bombilla";
        bulb.transform.SetParent(lantern, false);
        bulb.transform.localPosition = new Vector3(0f, 8.4f, 0f);
        bulb.transform.localScale = Vector3.one * 1.1f;
        Object.DestroyImmediate(bulb.GetComponent<Collider>());
        var r = bulb.GetComponent<MeshRenderer>();
        r.sharedMaterial = glow;
        r.shadowCastingMode = ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        block.SetColor("_EmissionColor", color * 6f);
        r.SetPropertyBlock(block);

        var light = new GameObject("Luz").AddComponent<Light>();
        light.transform.SetParent(lantern, false);
        light.transform.localPosition = new Vector3(0f, 8.4f, 0f);
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = 38f;
        light.shadows = LightShadows.None; // muchas luces con sombra cuestan caro
    }

    static Material LanternMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(LanternMaterialPath);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", Color.black);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        mat.SetColor("_EmissionColor", LanternColor * 6f);
        AssetDatabase.CreateAsset(mat, LanternMaterialPath);
        return mat;
    }

    // Apaga las luces sueltas fuera de las tres capas (p. ej. la Directional Light de la plantilla)
    // y las que sobran dentro de Key/Fill.
    static void DisableStrayLights(Transform lighting)
    {
        var accent = lighting.Find("Accent Light");
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
        {
            bool own = light.transform.parent == lighting || light.transform.IsChildOf(accent);
            if (own) continue;
            Undo.RecordObject(light.gameObject, "Iluminación");
            light.gameObject.SetActive(false);
            Debug.Log($"[Echoes] Luz '{light.name}' desactivada: la iluminación la llevan Key, Fill y Accent.");
        }
    }

    // ---------- Cámaras ----------

    static CinemachineCamera BuildCameras(PlayerController player, Vector3 tensionCenter)
    {
        var cam = player.playerCamera;
        var brain = GetOrAdd<CinemachineBrain>(cam.gameObject);
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.2f);
        var camData = GetOrAdd<UniversalAdditionalCameraData>(cam.gameObject);
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        var group = FindOrCreateRoot(CamerasGroup);
        ClearChildren(group);

        // Cámara principal: sigue el punto de vista del Player. La Dead Zone deja pasar el
        // balanceo de cabeza sin que la cámara reaccione y el Damping suaviza pasos y saltos.
        var main = NewVirtualCamera(group, "CM_PlayerCam", 10, player.walkFov);
        main.Follow = player.cameraPivot;
        var composer = main.gameObject.AddComponent<CinemachinePositionComposer>();
        composer.CameraDistance = 0.2f;
        composer.Damping = new Vector3(0.35f, 0.25f, 0.35f);
        composer.Composition.DeadZone.Enabled = true;
        composer.Composition.DeadZone.Size = new Vector2(0.06f, 0.06f);
        main.gameObject.AddComponent<CinemachineRotateWithFollowTarget>().Damping = 0f; // ratón sin retraso
        main.gameObject.AddComponent<PlayerCameraLens>().player = player;

        // Cámara de tensión: mismo punto de vista, pero lente cerrada, horizonte inclinado y
        // cámara en mano. Más damping: el jugador siente que la cámara le "pesa".
        var tension = NewVirtualCamera(group, "CM_TensionCam", 0, 52f);
        tension.Lens.Dutch = 8f;
        tension.Follow = player.cameraPivot;
        var tComposer = tension.gameObject.AddComponent<CinemachinePositionComposer>();
        tComposer.CameraDistance = 0.2f;
        tComposer.Damping = new Vector3(0.8f, 0.6f, 0.8f);
        tComposer.Composition.DeadZone.Enabled = true;
        tComposer.Composition.DeadZone.Size = new Vector2(0.1f, 0.1f);
        tension.gameObject.AddComponent<CinemachineRotateWithFollowTarget>().Damping = 0.08f;
        var noise = tension.gameObject.AddComponent<CinemachineBasicMultiChannelPerlin>();
        noise.NoiseProfile = AssetDatabase.LoadAssetAtPath<NoiseSettings>(NoisePath);
        noise.AmplitudeGain = 0.6f;
        noise.FrequencyGain = 0.8f;
        tension.transform.position = tensionCenter;
        return tension;
    }

    static CinemachineCamera NewVirtualCamera(Transform parent, string name, int priority, float fov)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var vcam = go.AddComponent<CinemachineCamera>();
        vcam.Priority = priority;
        vcam.Lens = LensSettings.Default;
        vcam.Lens.FieldOfView = fov;
        vcam.Lens.NearClipPlane = 0.05f;
        vcam.Lens.FarClipPlane = 2000f;
        return vcam;
    }

    static void BuildTensionZone(Vector3 center, CinemachineCamera tensionCam)
    {
        var group = FindOrCreateRoot(CamerasGroup);
        var zone = new GameObject("Zona de Tensión (Trigger)");
        zone.transform.SetParent(group, false);
        zone.transform.position = center + Vector3.up * 4f;

        var box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(22f, 12f, 22f);
        zone.AddComponent<CameraZoneTrigger>().zoneCamera = tensionCam;

        // Volume local: dentro de la zona el mundo se enfría y se cierra la viñeta.
        var volume = zone.AddComponent<Volume>();
        volume.isGlobal = false;
        volume.blendDistance = 8f;
        volume.priority = 10f;
        volume.sharedProfile = VolumeProfiles.Create(TensionProfilePath, p =>
        {
            VolumeProfiles.Vignette(p, 0.5f, 0.5f, new Color(0.15f, 0f, 0f));
            VolumeProfiles.ColorAdjustments(p, -0.3f, 30f, -40f, new Color(1f, 0.85f, 0.85f));
        });
    }

    // ---------- Post-procesado ----------

    static void BuildGlobalVolume()
    {
        var go = GameObject.Find("Global Volume");
        if (go == null) go = new GameObject("Global Volume");
        var volume = GetOrAdd<Volume>(go);
        volume.isGlobal = true;
        volume.enabled = true;
        volume.sharedProfile = VolumeProfiles.Create(AtmosphereProfilePath, p =>
        {
            // ACES: comprime el HDR (farolillos, bombillas con bloom) a la pantalla sin quemar.
            VolumeProfiles.Tonemapping(p, TonemappingMode.ACES);
            VolumeProfiles.Bloom(p, 0.9f, 1f, 0.7f, new Color(1f, 0.85f, 0.7f));
            VolumeProfiles.Vignette(p, 0.32f, 0.45f, Color.black);
            VolumeProfiles.ColorAdjustments(p, 0.8f, 15f, -5f, Color.white);
        });
    }

    // ---------- Utilidades ----------

    static List<Vector3> PathSamples(Terrain terrain)
    {
        var points = new List<Vector3>();
        foreach (Transform t in GameObject.Find(PathBuilder.PathObjectName).transform) points.Add(t.position);
        if (points.Count < 2)
        {
            Debug.LogError("[Echoes] El objeto 'Camino' necesita al menos 2 puntos.");
            return points;
        }
        var samples = PathBuilder.SampleSpline(points, 1f);
        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            s.y = terrain.SampleHeight(s) + terrain.GetPosition().y;
            samples[i] = s;
        }
        return samples;
    }

    static Vector3 PointAlong(List<Vector3> path, float t)
    {
        float total = 0f;
        for (int i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);
        float target = total * t, d = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            float seg = Vector3.Distance(path[i - 1], path[i]);
            if (d + seg >= target) return Vector3.Lerp(path[i - 1], path[i], (target - d) / seg);
            d += seg;
        }
        return path[path.Count - 1];
    }

    static Transform FindOrCreateRoot(string name)
    {
        var go = FindInactive(name);
        if (go == null)
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Iluminación");
        }
        go.SetActive(true);
        go.transform.SetParent(null);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    static GameObject FindInactive(string name)
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    static Transform Child(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t == null)
        {
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
        }
        t.gameObject.SetActive(true);
        t.localPosition = Vector3.zero;
        return t;
    }

    // La luz va en el propio objeto de la capa (Key Light, Fill Light...) para que el grupo se lea claro.
    static Light LayerLight(Transform parent, string name, LightType type)
    {
        var t = Child(parent, name);
        var light = GetOrAdd<Light>(t.gameObject);
        light.enabled = true;
        light.type = type;
        return light;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        return go.TryGetComponent<T>(out var c) ? c : Undo.AddComponent<T>(go);
    }

    static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject);
    }
}
