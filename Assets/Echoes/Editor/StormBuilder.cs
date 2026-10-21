using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Menú "Echoes > Tormenta (lluvia, rayos y truenos)": crea "-- WEATHER --" con lluvia que
/// sigue al Player, relámpagos que funden farolillos y truenos. Como no hay audios de
/// lluvia ni de trueno en el proyecto, los sintetiza como WAV en Assets/Echoes/Weather.
/// También añade el parpadeo (LanternFlicker) a los farolillos del camino.
/// </summary>
public static class StormBuilder
{
    internal const string Folder = "Assets/Echoes/Weather";
    const string WeatherGroup = "-- WEATHER --";
    const int SampleRate = 22050;

    [MenuItem("Echoes/Tormenta (lluvia, rayos y truenos)")]
    public static void Build()
    {
        VolumeProfiles.EnsureFolder(Folder);
        int lanterns = AddFlickerToLanterns();

        var old = GameObject.Find(WeatherGroup);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject(WeatherGroup);
        Undo.RegisterCreatedObjectUndo(root, "Tormenta");
        var storm = root.AddComponent<StormController>();

        var rainMat = ParticleMaterial("Rain.mat", RainTexture(), new Color(0.75f, 0.82f, 1f, 0.45f));
        var sparkMat = ParticleMaterial("Spark.mat", null, new Color(4f, 2.2f, 0.8f, 1f));
        var boltMat = ParticleMaterial("Bolt.mat", null, new Color(3f, 3.4f, 5f, 1f));

        storm.rain = BuildRain(root.transform, rainMat);
        storm.sparks = BuildSparks(root.transform, sparkMat);
        storm.bolt = BuildBolt(root.transform, boltMat);

        // Relámpago: direccional frío y potente, apagado salvo durante el destello.
        var flash = new GameObject("Relámpago").AddComponent<Light>();
        flash.transform.SetParent(root.transform, false);
        flash.transform.rotation = Quaternion.Euler(60f, 20f, 0f);
        flash.type = LightType.Directional;
        flash.color = new Color(0.8f, 0.87f, 1f);
        flash.intensity = 0f;
        flash.shadows = LightShadows.Hard;
        flash.enabled = false;
        storm.flashLight = flash;

        storm.rainAudio = AudioSourceOn(root.transform, "Audio Lluvia", true, 0.5f);
        storm.rainAudio.clip = SaveWav("Rain_Loop.wav", RainSamples(6f));
        storm.thunderAudio = AudioSourceOn(root.transform, "Audio Truenos", false, 1f);
        storm.thunderClips = new[]
        {
            SaveWav("Thunder_1.wav", ThunderSamples(7f, 11)),
            SaveWav("Thunder_2.wav", ThunderSamples(9f, 23)),
            SaveWav("Thunder_3.wav", ThunderSamples(6f, 37)),
        };

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log($"[Echoes] Tormenta lista en '{WeatherGroup}'. {lanterns} farolillos parpadean y algunos se romperán con los rayos. Guarda la escena (Ctrl+S).");
    }

    /// <summary>Añade/configura LanternFlicker en cada farolillo del Accent Light.</summary>
    public static int AddFlickerToLanterns()
    {
        var lighting = GameObject.Find("-- LIGHTING --");
        var accent = lighting != null ? lighting.transform.Find("Accent Light") : null;
        if (accent == null) return 0;

        int count = 0;
        foreach (Transform lantern in accent)
        {
            var light = lantern.GetComponentInChildren<Light>();
            var bulb = lantern.Find("Bombilla");
            if (light == null || bulb == null) continue; // Goal Light / Goal Beacon no parpadean
            SetupFlicker(lantern.gameObject, light, bulb.GetComponent<Renderer>());
            count++;
        }
        return count;
    }

    public static void SetupFlicker(GameObject lantern, Light light, Renderer bulb)
    {
        if (!lantern.TryGetComponent<LanternFlicker>(out var f)) f = Undo.AddComponent<LanternFlicker>(lantern);
        f.lightSource = light;
        f.bulb = bulb;
        f.emissionColor = light.color;
        bool tense = light.color.g < 0.4f; // farolillos rojos de la zona de tensión: más inestables
        f.flickerAmount = tense ? 0.55f : 0.25f;
        f.flickerSpeed = tense ? 12f : 6f;
        f.glitchesPerMinute = tense ? 12f : 3f;
    }

    // ---------- Partículas ----------

    static ParticleSystem BuildRain(Transform parent, Material mat)
    {
        var go = new GameObject("Lluvia");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = 1.3f;
        main.startSpeed = 0f;
        main.startSize = 0.08f;
        main.maxParticles = 9000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;

        var emission = ps.emission;
        emission.rateOverTime = 6000f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(90f, 1f, 90f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-5f, -3f);
        velocity.y = new ParticleSystem.MinMaxCurve(-42f, -36f);
        velocity.z = new ParticleSystem.MinMaxCurve(1f, 2f);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.035f;
        r.lengthScale = 1f;
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    static ParticleSystem BuildSparks(Transform parent, Material mat)
    {
        var go = new GameObject("Chispas");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 16f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.gravityModifier = 2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.04f;
        r.lengthScale = 1f;
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return ps;
    }

    static LineRenderer BuildBolt(Transform parent, Material mat)
    {
        var go = new GameObject("Rayo");
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.sharedMaterial = mat;
        lr.widthCurve = new AnimationCurve(new Keyframe(0f, 1.2f), new Keyframe(1f, 2.2f));
        lr.numCapVertices = 2;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.positionCount = 0;
        lr.enabled = false;
        return lr;
    }

    internal static Material ParticleMaterial(string file, Texture2D texture, Color color)
    {
        string path = $"{Folder}/{file}";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        // Transparente con mezcla alfa.
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetColor("_BaseColor", color);
        if (texture != null) mat.SetTexture("_BaseMap", texture);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Gota: degradado vertical suave, más opaco en el centro.
    static Texture2D RainTexture()
    {
        string path = $"{Folder}/Rain_Drop.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        const int w = 16, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float u = Mathf.Abs(x / (w - 1f) - 0.5f) * 2f;
            float v = Mathf.Abs(y / (h - 1f) - 0.5f) * 2f;
            float a = Mathf.Clamp01(1f - u * u) * Mathf.Clamp01(1f - v * v * v);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------- Audio sintetizado ----------

    static AudioSource AudioSourceOn(Transform parent, string name, bool loop, float volume)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var a = go.AddComponent<AudioSource>();
        a.loop = loop;
        a.playOnAwake = loop;
        a.volume = volume;
        a.spatialBlend = 0f;
        return a;
    }

    // Lluvia: ruido filtrado (rumor) + gotas sueltas; los extremos se funden para que el bucle no corte.
    static float[] RainSamples(float seconds)
    {
        var rng = new System.Random(5);
        int n = (int)(seconds * SampleRate);
        var s = new float[n];
        float lp = 0f, hp = 0f, prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float white = (float)rng.NextDouble() * 2f - 1f;
            lp += (white - lp) * 0.35f;                    // quita el siseo más agudo
            hp = 0.97f * (hp + lp - prev); prev = lp;      // quita graves: suena a lluvia, no a viento
            float drop = rng.NextDouble() < 0.0015 ? (float)rng.NextDouble() * 0.6f : 0f;
            s[i] = hp * 0.55f + drop * white;
        }
        int fade = SampleRate / 2;
        for (int i = 0; i < fade; i++)
        {
            float t = i / (float)fade;
            s[i] = s[i] * t + s[n - fade + i] * (1f - t);
        }
        System.Array.Resize(ref s, n - fade);
        return s;
    }

    // Trueno: chasquido inicial + retumbo grave con varias oleadas que se apagan.
    static float[] ThunderSamples(float seconds, int seed)
    {
        var rng = new System.Random(seed);
        int n = (int)(seconds * SampleRate);
        var s = new float[n];
        var swells = new float[5];
        for (int k = 0; k < swells.Length; k++) swells[k] = (float)rng.NextDouble() * seconds * 0.6f + 0.2f;

        float brown = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float white = (float)rng.NextDouble() * 2f - 1f;
            brown = Mathf.Clamp(brown * 0.995f + white * 0.08f, -1f, 1f);
            lp += (brown - lp) * 0.08f;

            float crack = t < 0.25f ? white * Mathf.Exp(-t * 18f) : 0f;
            float env = Mathf.Exp(-t * 0.55f) * 0.6f;
            foreach (float c in swells) env += Mathf.Exp(-Mathf.Pow((t - c) / 0.45f, 2f)) * 0.5f;
            env *= Mathf.Clamp01((seconds - t) / 1.5f); // cola sin cortes
            s[i] = crack * 0.7f + lp * env * 2.2f;
        }
        float peak = 0.001f;
        foreach (float v in s) peak = Mathf.Max(peak, Mathf.Abs(v));
        for (int i = 0; i < n; i++) s[i] = s[i] / peak * 0.9f;
        return s;
    }

    static AudioClip SaveWav(string file, float[] samples)
    {
        string path = $"{Folder}/{file}";
        using (var w = new BinaryWriter(File.Create(path)))
        {
            int bytes = samples.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
            w.Write((short)1); w.Write((short)1); w.Write(SampleRate); w.Write(SampleRate * 2);
            w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
            foreach (float v in samples) w.Write((short)(Mathf.Clamp(v, -1f, 1f) * short.MaxValue));
        }
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
