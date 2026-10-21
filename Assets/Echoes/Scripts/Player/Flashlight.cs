using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Linterna del Player: foco que cuelga de la cámara y la sigue con un poco de retraso, como
/// si la llevara en la mano. F (o Y/Triángulo en mando) la enciende y apaga.
/// </summary>
[DefaultExecutionOrder(1000)] // después de que Cinemachine mueva la cámara
public class Flashlight : MonoBehaviour
{
    public Light spot;
    public bool startOn = true;
    [Tooltip("Cuánto tarda en alcanzar a la cámara al girar (más alto = más rígida).")]
    public float followSharpness = 12f;

    [Header("Sonido")]
    public AudioSource audioSource;
    public AudioClip clickClip;

    Transform anchor;
    Vector3 localOffset;

    void Awake()
    {
        anchor = transform.parent;
        localOffset = transform.localPosition;
        // Se mueve en espacio de mundo para poder ir "por detrás" de la cámara.
        transform.SetParent(null, true);
        if (spot != null) spot.enabled = startOn;
    }

    void Update()
    {
        var kb = Keyboard.current;
        var pad = Gamepad.current;
        if ((kb != null && kb.fKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame))
            Toggle();
    }

    void LateUpdate()
    {
        if (anchor == null) { Destroy(gameObject); return; } // el Player ya no existe
        float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.position = anchor.TransformPoint(localOffset);
        transform.rotation = Quaternion.Slerp(transform.rotation, anchor.rotation, k);
    }

    public void Toggle()
    {
        if (spot == null) return;
        spot.enabled = !spot.enabled;
        if (audioSource != null && clickClip != null) audioSource.PlayOneShot(clickClip, 0.7f);
    }
}
