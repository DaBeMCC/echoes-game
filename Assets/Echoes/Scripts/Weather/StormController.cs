using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tormenta: lluvia que sigue al Player, relámpagos cada cierto tiempo y truenos con el
/// retraso que corresponde a la distancia. Los farolillos se van rompiendo (por rayos y por
/// desgaste con el tiempo, ver <see cref="LanternFlicker.Break"/>) y con cada uno que se
/// rompe el mundo se oscurece: niebla, ambiente y luna pierden luz. Al final solo queda la
/// linterna del Player.
/// </summary>
public class StormController : MonoBehaviour
{
    [Header("Lluvia")]
    public ParticleSystem rain;
    public float rainHeight = 30f;
    public AudioSource rainAudio;

    [Header("Relámpagos")]
    public Light flashLight;
    public float flashIntensity = 4f;
    public Vector2 secondsBetweenStrikes = new Vector2(7f, 18f);
    public LineRenderer bolt;
    public float boltHeight = 160f;

    [Header("Truenos")]
    public AudioSource thunderAudio;
    public AudioClip[] thunderClips;

    [Header("Farolillos")]
    [Range(0f, 1f)] public float breakChance = 0.6f;
    [Tooltip("Fracción de farolillos que pueden llegar a romperse.")]
    [Range(0f, 1f)] public float maxBrokenFraction = 0.85f;
    public float breakRadius = 120f;
    [Tooltip("Además de los rayos, cada cuánto se funde uno solo (s).")]
    public Vector2 secondsBetweenBurnouts = new Vector2(18f, 35f);
    [Tooltip("Radio del apagón momentáneo de los farolillos vecinos al romperse uno (m).")]
    public float blackoutRadius = 50f;
    public ParticleSystem sparks;

    [Header("Oscuridad")]
    [Tooltip("Cuánta luz se pierde cuando se han roto todos los que se pueden romper (0-1).")]
    [Range(0f, 1f)] public float maxDarkness = 0.85f;
    public float darkenSpeed = 0.25f;

    [Header("Ambiente")]
    public float fogMultiplier = 1.6f;

    Transform player;
    LanternFlicker[] lanterns;
    float baseFog;
    Color baseFogColor, baseSky, baseEquator, baseGround, baseAmbient;
    float baseSunIntensity;
    float darkness, flashLevel;

    public float Darkness => darkness;

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        lanterns = FindObjectsByType<LanternFlicker>(FindObjectsInactive.Exclude);

        baseFog = RenderSettings.fogDensity;
        baseFogColor = RenderSettings.fogColor;
        baseSky = RenderSettings.ambientSkyColor;
        baseEquator = RenderSettings.ambientEquatorColor;
        baseGround = RenderSettings.ambientGroundColor;
        baseAmbient = RenderSettings.ambientLight;
        if (RenderSettings.sun != null) baseSunIntensity = RenderSettings.sun.intensity;

        RenderSettings.fogDensity = baseFog * fogMultiplier; // la lluvia cierra la distancia de visión
        if (flashLight != null) flashLight.enabled = false;
        if (bolt != null) bolt.enabled = false;
        if (rainAudio != null && !rainAudio.isPlaying) rainAudio.Play();

        StartCoroutine(StormLoop());
        StartCoroutine(BurnoutLoop());
    }

    void OnDisable() => RenderSettings.fogDensity = baseFog > 0f ? baseFog : RenderSettings.fogDensity;

    void Update()
    {
        // Cada farolillo roto acerca el mundo a la oscuridad total (suave, no de golpe).
        darkness = Mathf.MoveTowards(darkness, TargetDarkness(), darkenSpeed * Time.deltaTime);
        float light = 1f - darkness;

        Color fog = baseFogColor * light;
        fog.a = 1f;
        // El relámpago ilumina también la niebla (y con ella el fondo si hay SilentHillFog).
        RenderSettings.fogColor = Color.Lerp(fog, new Color(0.85f, 0.9f, 1f), Mathf.Clamp01(flashLevel) * 0.6f);
        RenderSettings.ambientSkyColor = baseSky * light;
        RenderSettings.ambientEquatorColor = baseEquator * light;
        RenderSettings.ambientGroundColor = baseGround * light;
        RenderSettings.ambientLight = baseAmbient * light;
        if (RenderSettings.sun != null) RenderSettings.sun.intensity = baseSunIntensity * light;
    }

    void LateUpdate()
    {
        if (rain != null && player != null)
            rain.transform.position = player.position + Vector3.up * rainHeight;
    }

    float TargetDarkness()
    {
        if (lanterns == null || lanterns.Length == 0) return 0f;
        int broken = 0;
        foreach (var l in lanterns) if (l != null && l.IsBroken) broken++;
        float breakable = Mathf.Max(1f, lanterns.Length * maxBrokenFraction);
        return Mathf.Clamp01(broken / breakable) * maxDarkness;
    }

    IEnumerator StormLoop()
    {
        yield return new WaitForSeconds(Random.Range(2f, 5f));
        while (enabled)
        {
            yield return Strike();
            yield return new WaitForSeconds(Random.Range(secondsBetweenStrikes.x, secondsBetweenStrikes.y));
        }
    }

    // Desgaste: aunque no caiga un rayo, de vez en cuando un farolillo cercano se funde solo.
    IEnumerator BurnoutLoop()
    {
        while (enabled)
        {
            yield return new WaitForSeconds(Random.Range(secondsBetweenBurnouts.x, secondsBetweenBurnouts.y));
            var target = PickLantern();
            if (target != null) StartCoroutine(BreakAfter(target, 0f));
        }
    }

    IEnumerator Strike()
    {
        var target = Random.value < breakChance ? PickLantern() : null;
        Vector3 origin = player != null ? player.position : transform.position;
        Vector3 hit = target != null
            ? target.transform.position + Vector3.up * 8f
            : origin + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(150f, 400f);

        if (bolt != null) BuildBolt(hit);
        if (target != null) StartCoroutine(BreakAfter(target, 0.05f));

        // Trueno: el sonido llega más tarde cuanto más lejos cae (≈ 340 m/s, acortado para el juego).
        float distance = Vector3.Distance(origin, hit);
        StartCoroutine(Thunder(Mathf.Clamp(distance / 340f, 0.05f, 2.5f), Mathf.Lerp(1f, 0.45f, distance / 400f)));

        yield return Flash();
    }

    IEnumerator Flash()
    {
        float[] pulses = { 1f, 0f, 0.6f, 0f, 1.2f };
        float[] times = { 0.06f, 0.07f, 0.05f, 0.1f, 0.05f };
        for (int i = 0; i < pulses.Length; i++)
        {
            SetFlash(pulses[i]);
            if (bolt != null) bolt.enabled = pulses[i] > 0f;
            yield return new WaitForSeconds(times[i]);
        }
        if (bolt != null) bolt.enabled = false;
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            SetFlash(1.2f * (1f - t / 0.5f));
            yield return null;
        }
        SetFlash(0f);
    }

    void SetFlash(float level)
    {
        flashLevel = level;
        if (flashLight == null) return;
        flashLight.enabled = level > 0f;
        flashLight.intensity = flashIntensity * level;
    }

    IEnumerator Thunder(float delay, float volume)
    {
        yield return new WaitForSeconds(delay);
        if (thunderAudio == null || thunderClips == null || thunderClips.Length == 0) yield break;
        thunderAudio.pitch = Random.Range(0.85f, 1.1f);
        thunderAudio.PlayOneShot(thunderClips[Random.Range(0, thunderClips.Length)], volume);
    }

    IEnumerator BreakAfter(LanternFlicker lantern, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (sparks != null && lantern.bulb != null)
        {
            sparks.transform.position = lantern.bulb.transform.position;
            sparks.Emit(60);
        }
        lantern.Break();

        // La sobretensión deja a oscuras un momento a los farolillos de alrededor.
        foreach (var l in lanterns)
        {
            if (l == null || l == lantern || l.IsBroken) continue;
            float d = Vector3.Distance(l.transform.position, lantern.transform.position);
            if (d < blackoutRadius) l.Blackout(Random.Range(0.6f, 1.6f) * (1f - d / blackoutRadius) + 0.3f);
        }
    }

    // Un farolillo encendido cerca del Player, si aún se pueden romper más.
    LanternFlicker PickLantern()
    {
        if (lanterns == null || lanterns.Length == 0 || player == null) return null;
        int broken = 0;
        var candidates = new List<LanternFlicker>();
        foreach (var l in lanterns)
        {
            if (l == null) continue;
            if (l.IsBroken) { broken++; continue; }
            if (Vector3.Distance(l.transform.position, player.position) < breakRadius) candidates.Add(l);
        }
        if (broken + 1 > lanterns.Length * maxBrokenFraction || candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    // Rayo en zigzag desde el cielo hasta el punto de impacto.
    void BuildBolt(Vector3 hit)
    {
        const int segments = 14;
        Vector3 top = hit + new Vector3(Random.Range(-30f, 30f), boltHeight, Random.Range(-30f, 30f));
        bolt.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 p = Vector3.Lerp(top, hit, t);
            if (i > 0 && i < segments) p += new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * 9f * (1f - t * 0.5f);
            bolt.SetPosition(i, p);
        }
    }
}
