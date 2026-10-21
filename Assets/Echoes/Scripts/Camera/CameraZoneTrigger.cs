using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Zona de tensión: mientras el Player está dentro del trigger, sube la prioridad de
/// <see cref="zoneCamera"/> para que el CinemachineBrain mezcle hacia ella; al salir vuelve.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CameraZoneTrigger : MonoBehaviour
{
    public CinemachineCamera zoneCamera;
    public int activePriority = 20;
    public int idlePriority = 0;

    void Reset() => GetComponent<Collider>().isTrigger = true;

    void Start()
    {
        if (zoneCamera != null) zoneCamera.Priority = idlePriority;
    }

    void OnTriggerEnter(Collider other)
    {
        if (zoneCamera != null && other.CompareTag("Player")) zoneCamera.Priority = activePriority;
    }

    void OnTriggerExit(Collider other)
    {
        if (zoneCamera != null && other.CompareTag("Player")) zoneCamera.Priority = idlePriority;
    }
}
