using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menú "Echoes > Crear camino": traza un camino de tierra sobre el terreno siguiendo los hijos
/// del objeto "Camino" (puntos de control). Si no existe, lo crea con un recorrido serpenteante.
/// Pinta una capa de tierra, suaviza el relieve bajo el camino y quita la hierba encima.
/// Mueve los puntos en la escena y vuelve a usar el menú para retrazarlo.
/// </summary>
public static class PathBuilder
{
    const string Root = "Assets/Echoes";
    const string TerrainFolder = Root + "/Terrain";
    const string TexturePath = TerrainFolder + "/Path_Dirt.png";
    const string LayerPath = TerrainFolder + "/Path_Dirt.terrainlayer";
    const string GroundLayerPath = "Assets/Polytope Studio/Lowpoly_Demos/Environment_Free/Helpers/Ground_Layer_01.terrainlayer";
    internal const string PathObjectName = "Camino";

    // Medidas para una persona de 1,8 m; se multiplican por el playerScale del Player.
    const float BaseWidth = 3f;      // ancho del camino en metros
    const float BaseFalloff = 1.5f;  // borde difuminado a cada lado

    static float scale = 1f; // se lee del Player al empezar a trazar
    static float Width => BaseWidth * scale;
    static float Falloff => BaseFalloff * scale;
    internal static float HalfWidth => BaseWidth * PlayerScale() * 0.5f;
    const float FlattenAmount = 0.8f; // 0 = no tocar el relieve, 1 = aplanar del todo

    [MenuItem("Echoes/Crear camino")]
    public static void CreatePath()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("[Echoes] No se encontró un Terrain activo en la escena.");
            return;
        }

        scale = PlayerScale();
        var pathGo = GameObject.Find(PathObjectName);
        if (pathGo == null) pathGo = CreateDefaultWaypoints(terrain);

        var waypoints = new List<Vector3>();
        foreach (Transform child in pathGo.transform) waypoints.Add(child.position);
        if (waypoints.Count < 2)
        {
            Debug.LogError($"[Echoes] '{PathObjectName}' necesita al menos 2 puntos hijos.");
            return;
        }

        var data = terrain.terrainData;
        Undo.RegisterCompleteObjectUndo(data, "Crear camino");
        Undo.RegisterCompleteObjectUndo(data.alphamapTextures, "Crear camino");

        int layer = EnsurePathLayer(data);
        List<Vector3> samples = SampleSpline(waypoints, 0.5f);

        FlattenHeights(terrain, samples);
        PaintAlphamap(terrain, samples, layer);
        ClearDetails(terrain, samples);
        ClearTrees(terrain, samples);

        // Coloca los puntos de control sobre el relieve resultante.
        foreach (Transform child in pathGo.transform)
        {
            Undo.RecordObject(child, "Crear camino");
            Vector3 p = child.position;
            p.y = terrain.SampleHeight(p) + terrain.GetPosition().y;
            child.position = p;
        }

        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(pathGo.scene);
        Selection.activeGameObject = pathGo;
        Debug.Log($"[Echoes] Camino trazado con {waypoints.Count} puntos. Mueve los hijos de '{PathObjectName}' y vuelve a usar el menú para retocarlo.");
    }

    // Escala del personaje: así el camino tiene el ancho que pide el Player (2,5 = 4,5 m de alto).
    static float PlayerScale()
    {
        var player = Object.FindFirstObjectByType<PlayerController>();
        return player != null ? player.playerScale : 1f;
    }

    static GameObject CreateDefaultWaypoints(Terrain terrain)
    {
        Vector3 tPos = terrain.GetPosition();
        Vector3 size = terrain.terrainData.size;

        // Empieza cerca del Player si existe; si no, en el centro del terreno.
        Vector3 start = tPos + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        var player = GameObject.Find("Player");
        if (player != null) start = player.transform.position;

        var go = new GameObject(PathObjectName);
        Undo.RegisterCreatedObjectUndo(go, "Crear camino");

        // Recorrido en S hacia el lado más largo del terreno, sin salirse de los bordes.
        float length = Mathf.Min(size.x, size.z) * 0.6f;
        Vector3 dir = (start.z - tPos.z) < size.z * 0.5f ? Vector3.forward : Vector3.back;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        const int count = 6;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            Vector3 p = start + dir * (length * t) + side * (Mathf.Sin(t * Mathf.PI * 2f) * length * 0.15f);
            p.x = Mathf.Clamp(p.x, tPos.x + 2f, tPos.x + size.x - 2f);
            p.z = Mathf.Clamp(p.z, tPos.z + 2f, tPos.z + size.z - 2f);
            p.y = terrain.SampleHeight(p) + tPos.y;

            var point = new GameObject($"Punto {i + 1}").transform;
            point.SetParent(go.transform, false);
            point.position = p;
        }
        return go;
    }

    // Catmull-Rom a través de los puntos, muestreado cada `step` metros.
    internal static List<Vector3> SampleSpline(List<Vector3> pts, float step)
    {
        var result = new List<Vector3>();
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 p0 = pts[Mathf.Max(i - 1, 0)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(i + 2, pts.Count - 1)];
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(p1, p2) / step));
            for (int s = 0; s < steps; s++)
            {
                float t = s / (float)steps, t2 = t * t, t3 = t2 * t;
                result.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3));
            }
        }
        result.Add(pts[pts.Count - 1]);
        return result;
    }

    // Distancia horizontal al camino y altura del camino en ese punto (suavizada).
    static float DistanceToPath(List<Vector3> samples, float x, float z, out float pathHeight)
    {
        float best = float.MaxValue;
        pathHeight = 0f;
        for (int i = 0; i < samples.Count - 1; i++)
        {
            Vector2 a = new Vector2(samples[i].x, samples[i].z), b = new Vector2(samples[i + 1].x, samples[i + 1].z);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(new Vector2(x, z) - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            float d = Vector2.Distance(new Vector2(x, z), a + ab * t);
            if (d < best)
            {
                best = d;
                pathHeight = Mathf.Lerp(samples[i].y, samples[i + 1].y, t);
            }
        }
        return best;
    }

    // 1 en el centro del camino, 0 fuera, con borde suave.
    static float Influence(float distance)
    {
        float half = Width * 0.5f;
        if (distance <= half) return 1f;
        return Mathf.SmoothStep(1f, 0f, (distance - half) / Falloff);
    }

    static RectInt Bounds(Terrain terrain, List<Vector3> samples, int resX, int resZ, float cellOffset)
    {
        Vector3 tPos = terrain.GetPosition();
        Vector3 size = terrain.terrainData.size;
        float pad = Width * 0.5f + Falloff;
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (var s in samples)
        {
            minX = Mathf.Min(minX, s.x); maxX = Mathf.Max(maxX, s.x);
            minZ = Mathf.Min(minZ, s.z); maxZ = Mathf.Max(maxZ, s.z);
        }
        int x0 = Mathf.Clamp(Mathf.FloorToInt((minX - pad - tPos.x) / size.x * (resX - cellOffset)), 0, resX - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((maxX + pad - tPos.x) / size.x * (resX - cellOffset)), 0, resX - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((minZ - pad - tPos.z) / size.z * (resZ - cellOffset)), 0, resZ - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((maxZ + pad - tPos.z) / size.z * (resZ - cellOffset)), 0, resZ - 1);
        return new RectInt(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
    }

    static void FlattenHeights(Terrain terrain, List<Vector3> rawSamples)
    {
        var data = terrain.terrainData;
        Vector3 tPos = terrain.GetPosition();
        Vector3 size = data.size;

        // Altura del camino = terreno bajo cada muestra, suavizada a lo largo para evitar escalones.
        var samples = new List<Vector3>(rawSamples);
        for (int i = 0; i < samples.Count; i++)
        {
            Vector3 s = samples[i];
            s.y = terrain.SampleHeight(s);
            samples[i] = s;
        }
        for (int pass = 0; pass < 3; pass++)
        {
            var smoothed = new List<Vector3>(samples);
            for (int i = 0; i < samples.Count; i++)
            {
                float sum = 0f; int n = 0;
                for (int k = -6; k <= 6; k++)
                {
                    int j = Mathf.Clamp(i + k, 0, samples.Count - 1);
                    sum += samples[j].y; n++;
                }
                Vector3 s = smoothed[i]; s.y = sum / n; smoothed[i] = s;
            }
            samples = smoothed;
        }

        int res = data.heightmapResolution;
        RectInt r = Bounds(terrain, samples, res, res, 1f);
        float[,] heights = data.GetHeights(r.x, r.y, r.width, r.height);
        for (int z = 0; z < r.height; z++)
        for (int x = 0; x < r.width; x++)
        {
            float wx = tPos.x + (r.x + x) / (float)(res - 1) * size.x;
            float wz = tPos.z + (r.y + z) / (float)(res - 1) * size.z;
            float d = DistanceToPath(samples, wx, wz, out float pathY);
            float w = Influence(d) * FlattenAmount;
            if (w <= 0f) continue;
            heights[z, x] = Mathf.Lerp(heights[z, x], pathY / size.y, w);
        }
        data.SetHeights(r.x, r.y, heights);
    }

    static void PaintAlphamap(Terrain terrain, List<Vector3> samples, int layer)
    {
        var data = terrain.terrainData;
        Vector3 tPos = terrain.GetPosition();
        Vector3 size = data.size;
        int res = data.alphamapResolution;
        int layers = data.alphamapLayers;

        RectInt r = Bounds(terrain, samples, res, res, 0f);
        float[,,] maps = data.GetAlphamaps(r.x, r.y, r.width, r.height);
        for (int z = 0; z < r.height; z++)
        for (int x = 0; x < r.width; x++)
        {
            float wx = tPos.x + (r.x + x + 0.5f) / res * size.x;
            float wz = tPos.z + (r.y + z + 0.5f) / res * size.z;
            float w = Influence(DistanceToPath(samples, wx, wz, out _));
            if (w <= 0f) continue;
            for (int l = 0; l < layers; l++)
                maps[z, x, l] = l == layer ? Mathf.Lerp(maps[z, x, l], 1f, w) : maps[z, x, l] * (1f - w);
        }
        data.SetAlphamaps(r.x, r.y, maps);
    }

    static void ClearDetails(Terrain terrain, List<Vector3> samples)
    {
        var data = terrain.terrainData;
        if (data.detailPrototypes.Length == 0) return;
        Vector3 tPos = terrain.GetPosition();
        Vector3 size = data.size;
        int res = data.detailResolution;

        RectInt r = Bounds(terrain, samples, res, res, 0f);
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
        {
            int[,] details = data.GetDetailLayer(r.x, r.y, r.width, r.height, layer);
            for (int z = 0; z < r.height; z++)
            for (int x = 0; x < r.width; x++)
            {
                float wx = tPos.x + (r.x + x + 0.5f) / res * size.x;
                float wz = tPos.z + (r.y + z + 0.5f) / res * size.z;
                if (Influence(DistanceToPath(samples, wx, wz, out _)) > 0.3f) details[z, x] = 0;
            }
            data.SetDetailLayer(r.x, r.y, layer, details);
        }
    }

    // Quita los árboles del terreno que caen sobre el camino (y un margen para que las copas no lo tapen).
    static void ClearTrees(Terrain terrain, List<Vector3> samples)
    {
        var data = terrain.terrainData;
        Vector3 tPos = terrain.GetPosition();
        Vector3 size = data.size;
        float clearance = Width * 0.5f + Falloff + 2f * scale;

        var kept = new List<TreeInstance>();
        foreach (var tree in data.treeInstances)
        {
            float wx = tPos.x + tree.position.x * size.x;
            float wz = tPos.z + tree.position.z * size.z;
            if (DistanceToPath(samples, wx, wz, out _) > clearance) kept.Add(tree);
        }
        int removed = data.treeInstanceCount - kept.Count;
        if (removed == 0) return;
        data.treeInstances = kept.ToArray();
        terrain.Flush();
        Debug.Log($"[Echoes] {removed} árboles quitados del camino.");
    }

    // Devuelve el índice de la capa de tierra en el terreno, creándola si hace falta.
    static int EnsurePathLayer(TerrainData data)
    {
        var pathLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerPath);
        if (pathLayer == null)
        {
            EnsureFolder(Root);
            EnsureFolder(TerrainFolder);
            pathLayer = new TerrainLayer
            {
                diffuseTexture = CreateDirtTexture(),
                tileSize = new Vector2(4f, 4f),
            };
            AssetDatabase.CreateAsset(pathLayer, LayerPath);
            AssetDatabase.SaveAssets();
        }

        var layers = new List<TerrainLayer>(data.terrainLayers);
        int index = layers.IndexOf(pathLayer);
        if (index >= 0) return index;

        // Sin capas, la primera que se añade cubre todo el terreno: pon antes una base de hierba.
        if (layers.Count == 0)
        {
            var ground = AssetDatabase.LoadAssetAtPath<TerrainLayer>(GroundLayerPath);
            if (ground != null) layers.Add(ground);
            else Debug.LogWarning($"[Echoes] El terreno no tenía capas y no se encontró {GroundLayerPath}.");
        }
        layers.Add(pathLayer);
        data.terrainLayers = layers.ToArray();
        return layers.Count - 1;
    }

    // Textura de tierra low-poly generada por código (marrón con algo de ruido).
    static Texture2D CreateDirtTexture()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        var baseColor = new Color(0.47f, 0.36f, 0.24f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.6f + Mathf.PerlinNoise(x * 0.3f + 50f, y * 0.3f) * 0.4f;
            tex.SetPixel(x, y, baseColor * Mathf.Lerp(0.85f, 1.12f, n));
        }
        tex.Apply();
        File.WriteAllBytes(TexturePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(TexturePath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
