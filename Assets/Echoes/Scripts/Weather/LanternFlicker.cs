using System.Collections;
using UnityEngine;

/// <summary>
/// Farolillo con la luz inestable: parpadeo suave continuo y, de vez en cuando, un fallo
/// brusco. <see cref="Break"/> lo funde (chisporroteo y se apaga para siempre); lo usa
/// StormController cuando cae un rayo.
/// </summary>
public class LanternFlicker : MonoBehaviour
{
    public Light lightSource;
    public Renderer bulb;
    public Color emissionColor = new Color(1f, 0.68f, 0.35f);
    public float emissionStrength = 6f;

    [Header("Parpadeo")]
    [Range(0f, 1f)] public float flickerAmount = 0.25f;
    public float flickerSpeed = 6f;
    [Tooltip("Fallos bruscos por minuto (la luz se corta y vuelve).")]
    public float glitchesPerMinute = 4f;

    public bool IsBroken { get; private set; }

    float baseIntensity;
    float seed;
    float glitchUntil;
    float blackoutUntil;
    MaterialPropertyBlock block;

    void Awake()
    {
        baseIntensity = lightSource != null ? lightSource.intensity : 1f;
        seed = Random.value * 100f;
        block = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (IsBroken) return;

        float t = Time.time;
        if (t < blackoutUntil) { SetLevel(0f); return; }
        if (t > glitchUntil && Random.value < glitchesPerMinute / 60f * Time.deltaTime)
            glitchUntil = t + Random.Range(0.15f, 0.6f);

        float level;
        if (t < glitchUntil)
            level = Mathf.PerlinNoise(seed, t * 40f) > 0.5f ? 1f : 0.05f; // fallo: on/off rápido
        else
            level = 1f - flickerAmount + flickerAmount * Mathf.PerlinNoise(seed, t * flickerSpeed);
        SetLevel(level);
    }

    /// <summary>Apagón momentáneo (sobretensión al romperse un farolillo vecino).</summary>
    public void Blackout(float seconds)
    {
        blackoutUntil = Mathf.Max(blackoutUntil, Time.time + seconds);
    }

    /// <summary>Funde el farolillo: chisporrotea un momento y se queda apagado.</summary>
    public void Break()
    {
        if (IsBroken) return;
        IsBroken = true;
        StartCoroutine(BreakRoutine());
    }

    IEnumerator BreakRoutine()
    {
        SetLevel(3f); // sobretensión
        yield return new WaitForSeconds(0.08f);
        for (int i = 0; i < 6; i++)
        {
            SetLevel(i % 2 == 0 ? 0f : Random.Range(0.3f, 1.2f));
            yield return new WaitForSeconds(Random.Range(0.03f, 0.12f));
        }
        SetLevel(0f);
    }

    void SetLevel(float level)
    {
        if (lightSource != null)
        {
            lightSource.intensity = baseIntensity * level;
            lightSource.enabled = level > 0.01f;
        }
        if (bulb != null)
        {
            block.SetColor("_EmissionColor", emissionColor * emissionStrength * level);
            bulb.SetPropertyBlock(block);
        }
    }
}
