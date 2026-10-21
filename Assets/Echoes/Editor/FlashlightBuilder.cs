using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menú "Echoes > Añadir linterna al Player": pone una <see cref="Flashlight"/> en la cámara del
/// Player (en el prefab, así llega a todas las escenas). Genera el cookie del foco y el clic.
/// </summary>
public static class FlashlightBuilder
{
    const string PrefabPath = "Assets/Echoes/Prefabs/Player.prefab";
    const string Folder = "Assets/Echoes/Player";
    const string CookiePath = Folder + "/Flashlight_Cookie.png";
    const string ClickPath = Folder + "/Flashlight_Click.wav";

    [MenuItem("Echoes/Añadir linterna al Player")]
    public static void AddToPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        AddTo(root);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[Echoes] Linterna añadida al Player. F (o Y/Triángulo) la enciende y apaga.");
    }

    public static void AddTo(GameObject player)
    {
        var controller = player.GetComponent<PlayerController>();
        if (controller == null || controller.playerCamera == null) return;
        var cam = controller.playerCamera.transform;

        var old = cam.Find("Linterna");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var go = new GameObject("Linterna");
        go.transform.SetParent(cam, false);
        float s = controller.playerScale;
        go.transform.localPosition = new Vector3(0.25f, -0.2f, 0.15f) * s; // en la mano derecha

        var spot = go.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = new Color(1f, 0.95f, 0.85f);
        spot.intensity = 350f;
        spot.range = 22f * s;
        spot.spotAngle = 70f;
        spot.innerSpotAngle = 30f;
        spot.shadows = LightShadows.Soft;
        spot.cookie = Cookie();

        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        var flashlight = go.AddComponent<Flashlight>();
        flashlight.spot = spot;
        flashlight.audioSource = audio;
        flashlight.clickClip = Click();
    }

    // Haz con centro caliente, anillo y bordes irregulares: parece una linterna de verdad.
    static Texture2D Cookie()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(CookiePath);
        if (existing != null) return existing;
        VolumeProfiles.EnsureFolder(Folder);

        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (size - 1f) * 2f - 1f, v = y / (size - 1f) * 2f - 1f;
            float r = Mathf.Sqrt(u * u + v * v);
            float hot = Mathf.Exp(-r * r * 9f);                      // centro
            float ring = Mathf.Exp(-Mathf.Pow((r - 0.55f) / 0.06f, 2f)) * 0.35f; // reflejo de la parábola
            float spill = Mathf.Clamp01(1f - r) * 0.35f;              // luz de relleno
            float dirt = Mathf.Lerp(0.85f, 1f, Mathf.PerlinNoise(x * 0.08f, y * 0.08f));
            float a = Mathf.Clamp01((hot + ring + spill) * dirt) * Mathf.Clamp01((1f - r) * 6f);
            tex.SetPixel(x, y, new Color(a, a, a, a));
        }
        tex.Apply();
        File.WriteAllBytes(CookiePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(CookiePath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(CookiePath);
        importer.textureType = TextureImporterType.Cookie;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaSource = TextureImporterAlphaSource.FromGrayScale;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(CookiePath);
    }

    // Clic del interruptor: dos golpes cortos y secos.
    static AudioClip Click()
    {
        var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(ClickPath);
        if (existing != null) return existing;
        VolumeProfiles.EnsureFolder(Folder);

        const int rate = 22050;
        int n = rate / 8;
        var rng = new System.Random(3);
        using (var w = new BinaryWriter(File.Create(ClickPath)))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
            w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2);
            w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float tone = Mathf.Sin(t * 2f * Mathf.PI * 2400f);
                float env = Mathf.Exp(-t * 400f) + 0.6f * (t > 0.045f ? Mathf.Exp(-(t - 0.045f) * 500f) : 0f);
                w.Write((short)(Mathf.Clamp((noise * 0.6f + tone * 0.4f) * env, -1f, 1f) * short.MaxValue * 0.8f));
            }
        }
        AssetDatabase.ImportAsset(ClickPath);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(ClickPath);
    }
}
