using UnityEngine;

/// <summary>
/// Sonido del jugador: pasos en bucle mientras camina (más rápidos al correr, más suaves
/// agachado) y sonido ambiente + música de fondo en bucle.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerAudio : MonoBehaviour
{
    [Header("Pasos")]
    public AudioClip footstepsClip;
    [Range(0f, 1f)] public float footstepsVolume = 0.6f;
    public float sprintPitch = 1.35f;
    public float crouchVolumeMultiplier = 0.4f;
    public float fadeSpeed = 6f;
    [Tooltip("Tiempo en el aire antes de cortar los pasos (evita cortes en pendientes).")]
    public float airGraceTime = 0.15f;

    [Header("Ambiente y música")]
    public AudioClip ambientClip;
    [Range(0f, 1f)] public float ambientVolume = 0.5f;
    public AudioClip musicClip;
    [Range(0f, 1f)] public float musicVolume = 0.3f;

    [Header("Gruñido lejano (aleatorio)")]
    public AudioClip roarClip;
    [Range(0f, 1f)] public float roarVolume = 0.85f;
    [Tooltip("Segundos mínimos y máximos entre gruñidos.")]
    public Vector2 roarInterval = new Vector2(20f, 50f);
    [Tooltip("Segundos hasta el primer gruñido al empezar.")]
    public Vector2 firstRoarDelay = new Vector2(4f, 8f);
    [Tooltip("Distancia (m) a la que suena el gruñido, en una dirección al azar.")]
    public Vector2 roarDistance = new Vector2(120f, 250f);
    public Vector2 roarPitch = new Vector2(0.8f, 1.05f);
    [Tooltip("Filtro de graves: cuanto más bajo, más apagado y lejano suena.")]
    public float roarLowPassCutoff = 2200f;

    PlayerController player;
    AudioSource footsteps;
    AudioSource roar;
    float airTime;
    float nextRoarTime;

    void Awake()
    {
        player = GetComponent<PlayerController>();
        footsteps = CreateSource("Footsteps", footstepsClip, 0f);
        CreateSource("Ambient", ambientClip, ambientVolume)?.Play();
        CreateSource("Music", musicClip, musicVolume)?.Play();
        CreateRoarSource();
        nextRoarTime = Time.time + Random.Range(firstRoarDelay.x, firstRoarDelay.y);
        if (roarClip == null) Debug.LogWarning("[Echoes] PlayerAudio no tiene asignado el gruñido (Roar Clip).");
    }

    void CreateRoarSource()
    {
        if (roarClip == null) return;

        // Fuente separada que no sigue al jugador: el sonido queda "anclado" en un punto lejano.
        var go = new GameObject("DistantRoar");
        roar = go.AddComponent<AudioSource>();
        roar.clip = roarClip;
        roar.playOnAwake = false;
        roar.spatialBlend = 0.6f; // se oye de qué lado viene, sin desaparecer por la distancia
        roar.rolloffMode = AudioRolloffMode.Linear;
        roar.minDistance = 10f;
        roar.maxDistance = 100000f;
        roar.dopplerLevel = 0f;
        roar.spread = 60f;
        var lowPass = go.AddComponent<AudioLowPassFilter>();
        lowPass.cutoffFrequency = roarLowPassCutoff;
    }

    void ScheduleNextRoar() => nextRoarTime = Time.time + Random.Range(roarInterval.x, roarInterval.y);

    void PlayDistantRoar()
    {
        Vector2 dir = Random.insideUnitCircle.normalized;
        float dist = Random.Range(roarDistance.x, roarDistance.y);
        roar.transform.position = transform.position + new Vector3(dir.x, 0.2f, dir.y) * dist;
        roar.pitch = Random.Range(roarPitch.x, roarPitch.y);
        roar.volume = roarVolume * Random.Range(0.85f, 1f);
        roar.Play();
        Debug.Log($"[Echoes] Gruñido a {dist:0} m (volumen {roar.volume:0.00}).");
    }

    void OnDestroy()
    {
        if (roar != null) Destroy(roar.gameObject);
    }

    AudioSource CreateSource(string name, AudioClip clip, float volume)
    {
        if (clip == null) return null;
        var src = gameObject.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f; // 2D: suena igual en ambos oídos
        src.volume = volume;
        return src;
    }

    void Update()
    {
        if (roar != null && Time.time >= nextRoarTime)
        {
            if (!roar.isPlaying) PlayDistantRoar();
            ScheduleNextRoar();
        }

        if (footsteps == null) return;

        airTime = player.IsGrounded ? 0f : airTime + Time.deltaTime;
        bool walking = airTime < airGraceTime && player.SpeedFactor > 0.15f;

        float target = walking ? footstepsVolume * Mathf.Clamp01(player.SpeedFactor) : 0f;
        if (player.IsCrouching) target *= crouchVolumeMultiplier;
        footsteps.volume = Mathf.MoveTowards(footsteps.volume, target, fadeSpeed * Time.deltaTime);
        footsteps.pitch = Mathf.Lerp(footsteps.pitch, player.IsSprinting ? sprintPitch : 1f, 8f * Time.deltaTime);

        if (walking && !footsteps.isPlaying) footsteps.Play();
        else if (!walking && footsteps.isPlaying && footsteps.volume <= 0.001f) footsteps.Pause();
    }
}
