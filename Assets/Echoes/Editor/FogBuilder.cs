using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Menú "Echoes > Niebla Silent Hill": crea "-- FOG --" con <see cref="SilentHillFog"/>,
/// bancos de niebla que flotan alrededor del Player y un Volume con grano y color lavado.
/// </summary>
public static class FogBuilder
{
    const string FogGroup = "-- FOG --";
    const string ProfilePath = "Assets/Echoes/Lighting/Echoes_SilentHill.asset";

    [MenuItem("Echoes/Niebla Silent Hill")]
    public static void Build()
    {
        VolumeProfiles.EnsureFolder(StormBuilder.Folder);

        var old = GameObject.Find(FogGroup);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject(FogGroup);
        Undo.RegisterCreatedObjectUndo(root, "Niebla");

        var fog = root.AddComponent<SilentHillFog>();
        fog.wisps = BuildWisps(root.transform, fog.fogColor);
        fog.Apply(); // para verlo ya en el editor

        // Grano y color lavado: la firma visual de Silent Hill. Prioridad sobre el Global Volume,
        // solo pisa los parámetros que toca (saturación y contraste).
        var volume = root.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 5f;
        volume.sharedProfile = VolumeProfiles.Create(ProfilePath, p =>
        {
            var grain = p.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0.55f);
            grain.response.Override(0.7f);
            var color = p.Add<ColorAdjustments>(true);
            color.saturation.Override(-35f);
            color.contrast.Override(8f);
        });

        // Sin cielo también en el editor: el fondo de la cámara del Player es la niebla.
        var player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null && player.playerCamera != null)
        {
            Undo.RecordObject(player.playerCamera, "Niebla");
            player.playerCamera.clearFlags = CameraClearFlags.SolidColor;
            player.playerCamera.backgroundColor = fog.fogColor;
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log($"[Echoes] Niebla Silent Hill lista en '{FogGroup}': visibilidad {fog.fogEnd} m; no se dibuja nada más allá de {fog.fogEnd + fog.cullMargin} m.");
    }

    static ParticleSystem BuildWisps(Transform parent, Color fogColor)
    {
        var go = new GameObject("Bancos de niebla");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 22f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(18f, 34f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 22f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(130f, 10f, 130f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.1f, 0.15f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

        // Aparecen y desaparecen poco a poco.
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        var tint = Color.Lerp(fogColor, Color.white, 0.35f);
        tint.a = 0.16f;
        var mat = StormBuilder.ParticleMaterial("Fog_Wisp.mat", WispTexture(), tint);
        // Partículas suaves (sin cortes al tocar el suelo) y que se desvanecen al acercarse a la cámara.
        mat.SetFloat("_SoftParticlesEnabled", 1f);
        mat.SetFloat("_SoftParticlesNearFadeDistance", 0f);
        mat.SetFloat("_SoftParticlesFarFadeDistance", 6f);
        mat.EnableKeyword("_SOFTPARTICLES_ON");
        mat.SetFloat("_CameraFadingEnabled", 1f);
        mat.SetFloat("_CameraNearFadeDistance", 2f);
        mat.SetFloat("_CameraFarFadeDistance", 12f);
        mat.EnableKeyword("_FADING_ON");
        EditorUtility.SetDirty(mat);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.maxParticleSize = 3f;
        return ps;
    }

    // Mancha de niebla: degradado radial con ruido para que no se vea un círculo perfecto.
    static Texture2D WispTexture()
    {
        string path = $"{StormBuilder.Folder}/Fog_Wisp.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (size - 1f) * 2f - 1f, v = y / (size - 1f) * 2f - 1f;
            float radial = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
            float noise = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.6f + Mathf.PerlinNoise(x * 0.12f + 40f, y * 0.12f) * 0.4f;
            float a = Mathf.SmoothStep(0f, 1f, radial) * Mathf.Lerp(0.4f, 1f, noise);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
