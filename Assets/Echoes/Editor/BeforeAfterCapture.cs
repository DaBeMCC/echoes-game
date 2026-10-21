using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Menú "Echoes > Capturas antes y después (S10)": saca dos PNG con el mismo encuadre
/// (inicio del camino mirando hacia delante): ANTES sin luces ni Volume, DESPUÉS con las
/// tres capas, cámara y Volume. Se guardan en Docs/Capturas para el Journal.
/// </summary>
public static class BeforeAfterCapture
{
    const string OutputFolder = "Docs/Capturas";
    const int Width = 1920, Height = 1080;

    [MenuItem("Echoes/Capturas antes y después (S10)")]
    public static void Capture()
    {
        var path = GameObject.Find(PathBuilder.PathObjectName);
        if (path == null || path.transform.childCount < 2)
        {
            Debug.LogError("[Echoes] Primero crea el camino (Echoes > Crear camino).");
            return;
        }

        var points = new List<Vector3>();
        foreach (Transform t in path.transform) points.Add(t.position);
        var samples = PathBuilder.SampleSpline(points, 1f);

        // Detrás del inicio del camino y por encima de la hierba, mirando unos 90 m hacia delante.
        var terrain = Terrain.activeTerrain;
        Vector3 ahead = samples[Mathf.Min(90, samples.Count - 1)];
        Vector3 back = samples[0] - (ahead - samples[0]).normalized * 25f;
        back.y = terrain.SampleHeight(back) + terrain.GetPosition().y + 16f;
        ahead.y = terrain.SampleHeight(ahead) + terrain.GetPosition().y + 2f;
        Vector3 from = back, to = ahead;

        var go = new GameObject("Captura (temporal)") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(to - from));
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 2000f;
            cam.enabled = false;
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

            Directory.CreateDirectory(OutputFolder);
            AtmosphereBuilder.SetAtmosphereEnabled(false);
            Save(cam, Path.Combine(OutputFolder, "S10_antes.png"));
            AtmosphereBuilder.SetAtmosphereEnabled(true);
            Save(cam, Path.Combine(OutputFolder, "S10_despues.png"));
        }
        finally
        {
            AtmosphereBuilder.SetAtmosphereEnabled(true);
            Object.DestroyImmediate(go);
        }
        Debug.Log($"[Echoes] Capturas guardadas en {Path.GetFullPath(OutputFolder)}");
    }

    static void Save(Camera cam, string file)
    {
        var rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        RenderTexture.active = prev;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
    }
}
