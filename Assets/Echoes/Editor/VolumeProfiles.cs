using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Crea (o rehace) perfiles de Volume de URP como assets. Los usan AtmosphereBuilder y
/// EmotionalRoomBuilder.
/// </summary>
public static class VolumeProfiles
{
    /// <summary>Crea el perfil en <paramref name="path"/> vaciándolo si ya existía y llama a <paramref name="fill"/>.</summary>
    public static VolumeProfile Create(string path, Action<VolumeProfile> fill)
    {
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }
        else
        {
            foreach (var c in profile.components.ToArray())
            {
                profile.components.Remove(c);
                UnityEngine.Object.DestroyImmediate(c, true);
            }
        }

        fill(profile);
        foreach (var c in profile.components)
        {
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            if (!AssetDatabase.Contains(c)) AssetDatabase.AddObjectToAsset(c, profile);
        }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    public static void Tonemapping(VolumeProfile p, TonemappingMode mode = TonemappingMode.ACES)
    {
        p.Add<Tonemapping>(true).mode.Override(mode);
    }

    public static void Bloom(VolumeProfile p, float intensity, float threshold, float scatter, Color tint)
    {
        var b = p.Add<Bloom>(true);
        b.intensity.Override(intensity);
        b.threshold.Override(threshold);
        b.scatter.Override(scatter);
        b.tint.Override(tint);
    }

    public static void Vignette(VolumeProfile p, float intensity, float smoothness, Color color)
    {
        var v = p.Add<Vignette>(true);
        v.intensity.Override(intensity);
        v.smoothness.Override(smoothness);
        v.color.Override(color);
    }

    public static void ColorAdjustments(VolumeProfile p, float exposure, float contrast, float saturation, Color filter, float hueShift = 0f)
    {
        var c = p.Add<ColorAdjustments>(true);
        c.postExposure.Override(exposure);
        c.contrast.Override(contrast);
        c.saturation.Override(saturation);
        c.colorFilter.Override(filter);
        c.hueShift.Override(hueShift);
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, i));
        AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
    }
}
