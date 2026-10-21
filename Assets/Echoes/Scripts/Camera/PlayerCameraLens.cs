using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Con un CinemachineBrain en la cámara, el FOV lo decide la CinemachineCamera activa.
/// Este componente copia a la cámara virtual el FOV que calcula PlayerController
/// (caminar/correr), así el efecto de sprint sigue funcionando.
/// </summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(CinemachineCamera))]
public class PlayerCameraLens : MonoBehaviour
{
    public PlayerController player;

    CinemachineCamera vcam;

    void Awake() => vcam = GetComponent<CinemachineCamera>();

    void Update()
    {
        if (player == null || player.playerCamera == null) return;
        vcam.Lens.FieldOfView = player.playerCamera.fieldOfView;
    }
}
