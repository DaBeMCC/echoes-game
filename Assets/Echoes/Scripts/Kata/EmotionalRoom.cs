using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Dev Kata S10 "La Habitación Emocional": la geometría no cambia, solo la luz.
/// Cada perfil es un hijo con sus luces y su Volume; 1/2/3 (o los botones) cambian entre
/// Terror, Calma y Acción.
/// </summary>
public class EmotionalRoom : MonoBehaviour
{
    [System.Serializable]
    public class Profile
    {
        public string name;
        public GameObject root;
        public Color ambient = Color.gray;
    }

    public Profile[] profiles;
    public int current;

    void Start() => Apply(current);

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame) Apply(0);
        if (kb.digit2Key.wasPressedThisFrame) Apply(1);
        if (kb.digit3Key.wasPressedThisFrame) Apply(2);
    }

    public void Apply(int index)
    {
        if (profiles == null || profiles.Length == 0) return;
        current = Mathf.Clamp(index, 0, profiles.Length - 1);
        for (int i = 0; i < profiles.Length; i++)
            if (profiles[i].root != null) profiles[i].root.SetActive(i == current);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = profiles[current].ambient;
    }

    void OnGUI()
    {
        if (profiles == null) return;
        GUILayout.BeginArea(new Rect(16, 16, 420, 40));
        GUILayout.BeginHorizontal();
        for (int i = 0; i < profiles.Length; i++)
        {
            string label = $"{i + 1} · {profiles[i].name}";
            if (GUILayout.Toggle(i == current, label, "Button", GUILayout.Height(32)) && i != current) Apply(i);
        }
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }
}
