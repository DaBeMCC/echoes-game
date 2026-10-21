using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Niebla estilo Silent Hill: niebla lineal muy cerrada y gris, sin cielo (el fondo de la
/// cámara es el mismo color que la niebla, así el horizonte desaparece) y bancos de niebla
/// que flotan alrededor del Player. Se aplica en Awake para mandar sobre cualquier otro
/// ajuste de RenderSettings; StormController la aclara durante los relámpagos.
/// Rendimiento: lo que queda detrás de la niebla no se ve, así que no se dibuja: la cámara
/// corta en fogEnd + margen y árboles, hierba y sombras se limitan a esa distancia.
/// </summary>
[DefaultExecutionOrder(-100)]
public class SilentHillFog : MonoBehaviour
{
    public Color fogColor = new Color(0.36f, 0.37f, 0.38f);
    [Tooltip("Hasta aquí se ve con nitidez (m).")]
    public float fogStart = 4f;
    [Tooltip("A partir de aquí todo es niebla (m). El Player mide 4,5 m.")]
    public float fogEnd = 60f;
    public bool hideSky = true;

    [Header("Rendimiento")]
    public bool cullBeyondFog = true;
    [Tooltip("Metros extra tras el final de la niebla antes de dejar de dibujar.")]
    public float cullMargin = 10f;

    [Header("Bancos de niebla")]
    public ParticleSystem wisps;
    public float wispsHeight = 4f;

    Transform player;
    Camera cam;
    float savedShadowDistance = -1f;

    void Awake()
    {
        Apply();
        if (cullBeyondFog) CullBeyondFog();
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    void OnValidate()
    {
        if (isActiveAndEnabled) Apply();
    }

    public void Apply()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
    }

    void CullBeyondFog()
    {
        float distance = fogEnd + cullMargin;

        // Con CinemachineBrain el plano lejano lo pone la cámara virtual activa.
        foreach (var vcam in FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include))
            vcam.Lens.FarClipPlane = distance;
        foreach (var c in Camera.allCameras) c.farClipPlane = distance;

        foreach (var terrain in Terrain.activeTerrains)
        {
            terrain.treeDistance = distance;
            terrain.treeBillboardDistance = Mathf.Min(terrain.treeBillboardDistance, fogEnd * 0.5f);
            terrain.detailObjectDistance = Mathf.Min(terrain.detailObjectDistance, fogEnd);
        }

        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            savedShadowDistance = urp.shadowDistance;
            urp.shadowDistance = Mathf.Min(urp.shadowDistance, fogEnd);
        }
    }

    // El asset de URP es compartido: devolverle su distancia de sombras al salir.
    void OnDestroy()
    {
        if (savedShadowDistance >= 0f && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            urp.shadowDistance = savedShadowDistance;
    }

    void LateUpdate()
    {
        if (wisps != null && player != null)
            wisps.transform.position = player.position + Vector3.up * wispsHeight;

        // El fondo sigue al color de la niebla (también cuando un relámpago la aclara).
        if (cam == null) cam = Camera.main;
        if (cam != null && hideSky)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
        }
    }
}
