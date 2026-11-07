#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// VERSION 1
// EDITOR TOOL (runs inside Unity, not in the game).
// Builds the two cabin scenes from Assets/Models/CABINS.fbx (exported from cabin-nearviblab_edited_v7.blend):
//   Tools > Cabins > Build Tutorial Scene      -> Assets/Scenes/Cabin_Tutorial.unity (cabin A, normal shading, the tutorial)
//   Tools > Cabins > Build Combat Cabin Scene  -> Assets/Scenes/Cabin_Combat.unity   (cabins C+D joined, flat "realm" look)
//   Tools > Cabins > Build Both And Update Build List
// It can be run again at any time: it rebuilds the scenes from scratch (so don't put hand-made changes in them
// that you want to keep, or save them under another name first).
//
// All positions below are written in BLENDER coordinates (measured in the .blend), and converted to Unity's
// at run time. The conversion is worked out from the model itself (see BMap), so it does not matter how
// the FBX importer turned the axes.
public static class CabinSceneBuilder
{
    const string ModelPath = "Assets/Models/CABINS.fbx";
    const string PlayerPath = "Assets/Prefab/Player.prefab";
    const string TutorialPath = "Assets/Scenes/Cabin_Tutorial.unity";
    const string CombatPath = "Assets/Scenes/Cabin_Combat.unity";
    const string MatFolder = "Assets/Materials";

    // The realm colours of the combat cabin (the tutorial portal uses them too, so the screen opens onto this colour).
    // Placeholders: change them here and rebuild, or on the RealmStyle object in the scene.
    static readonly Color CombatFill = new Color(0.89f, 0.31f, 0.35f);   // coral red  #E34F59
    static readonly Color CombatLine = new Color(0.17f, 0.06f, 0.16f);   // deep plum  #2B0F29

    // The portal out of the combat cabin leads to the Classroom: placeholder colours for ITS realm (not set up there yet)
    static readonly Color NextFill = new Color(0.96f, 0.72f, 0.20f);     // mustard    #F5B833
    static readonly Color NextLine = new Color(0.20f, 0.10f, 0.05f);     // dark brown #331A0D

    // ---------------------------------------------------------------- menu
    [MenuItem("Tools/Cabins/Build Tutorial Scene")]
    public static void BuildTutorialMenu() { BuildTutorial(); }

    [MenuItem("Tools/Cabins/Build Combat Cabin Scene")]
    public static void BuildCombatMenu() { BuildCombat(); }

    [MenuItem("Tools/Cabins/Build Both And Update Build List")]
    public static void BuildBoth()
    {
        if (!BuildTutorial()) return;
        if (!BuildCombat()) return;
        UpdateBuildList();
    }

    // ---------------------------------------------------------------- Blender <-> Unity coordinates
    class BMap
    {
        public float sx = 1f, sy = 1f;       // Unity x = sx * blender x ; Unity z = sy * blender y ; Unity y = blender z
        public bool ok;
        public Vector3 P(float x, float y, float z) { return new Vector3(sx * x, z, sy * y); }
        public Vector3 Dir(Vector3 b) { return new Vector3(sx * b.x, b.z, sy * b.y); }
        public Vector3 Size(Vector3 b) { return new Vector3(Mathf.Abs(b.x), Mathf.Abs(b.z), Mathf.Abs(b.y)); }

        // ceiling.001 is centred at Blender (1.27, 5.02): see which way the importer put it
        public static BMap Calibrate(GameObject root)
        {
            var m = new BMap();
            Transform t = FindT(root, "ceiling.001");
            Renderer r = t != null ? t.GetComponent<Renderer>() : null;
            if (r == null) { Debug.LogError("[CabinSceneBuilder] ceiling.001 not found in the model."); return m; }
            Vector3 c = r.bounds.center;
            m.sx = c.x >= 0f ? 1f : -1f;
            m.sy = c.z >= 0f ? 1f : -1f;
            m.ok = Mathf.Abs(Mathf.Abs(c.x) - 1.27f) < 0.2f && Mathf.Abs(Mathf.Abs(c.z) - 5.02f) < 0.2f;
            Debug.Log("[CabinSceneBuilder] axis mapping: Unity x = " + (m.sx > 0 ? "+" : "-") + "blender x, Unity z = " + (m.sy > 0 ? "+" : "-")
                      + "blender y (ceiling.001 centre in Unity: " + c + ")" + (m.ok ? "" : "  <-- NOT what I expected, positions will be wrong!"));
            return m;
        }
    }

    // ---------------------------------------------------------------- helpers
    static Transform FindT(GameObject root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    }

    static void EnsureModelSettings()
    {
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
        var imp = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (imp == null) { Debug.LogError("[CabinSceneBuilder] " + ModelPath + " not found. Export it from Blender first."); return; }
        if (imp.materialImportMode != ModelImporterMaterialImportMode.None || imp.addCollider || imp.importCameras || imp.importLights)
        {
            imp.materialImportMode = ModelImporterMaterialImportMode.None;   // we paint our own simple materials
            imp.addCollider = false;                                         // and build clean colliders ourselves
            imp.importCameras = false;
            imp.importLights = false;
            imp.SaveAndReimport();
        }
    }

    static Material Mat(string name, Color c, float smoothness = 0.15f, float metallic = 0f, bool emissive = false, Color? glow = null, bool doubleSided = false)
    {
        if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets", "Materials");
        string path = MatFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Cull", doubleSided ? 0f : 2f);                    // 0 = draw both sides (the ceiling's faces point up), 2 = back faces hidden
        if (emissive)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", glow ?? Color.white);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Paint(GameObject root)
    {
        var wall = Mat("Cabin_Wall", new Color(0.86f, 0.84f, 0.78f));
        var floor = Mat("Cabin_Floor", new Color(0.50f, 0.47f, 0.44f), 0.3f, 0f, false, null, true);
        var ceiling = Mat("Cabin_Ceiling", new Color(0.93f, 0.93f, 0.90f), 0.15f, 0f, false, null, true);
        var wood = Mat("Cabin_Wood", new Color(0.42f, 0.28f, 0.17f), 0.3f);
        var chair = Mat("Cabin_Chair", new Color(0.15f, 0.16f, 0.18f), 0.25f);
        var metal = Mat("Cabin_Metal", new Color(0.35f, 0.36f, 0.38f), 0.5f, 0.8f);
        var glass = Mat("Cabin_Glass", new Color(0.62f, 0.78f, 0.9f), 0.9f);
        var white = Mat("Cabin_PanelWhite", new Color(0.92f, 0.92f, 0.90f), 0.35f);
        var black = Mat("Cabin_PanelBlack", new Color(0.04f, 0.04f, 0.045f), 0.8f);
        var glassNames = new HashSet<string> { "Cube.020", "Cube.021", "Cube.022", "Cube.023", "Cube.024", "Cube.025", "Cube.026", "Cube.028" };

        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            string n = r.name;
            Material[] mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            Material m;
            if (n == "Plane") m = wall;
            else if (n == "cabin-nearviblab") m = floor;
            else if (n.StartsWith("ceiling")) m = ceiling;
            else if (n.StartsWith("Chair") || n.StartsWith("Computer Chair")) m = chair;
            else if (n.StartsWith("Cylinder")) m = metal;
            else if (n == "panel_frame") m = white;
            else if (n.StartsWith("cabin_switch")) m = black;
            else if (n == "panel_outlet") m = white;
            else if (glassNames.Contains(n)) m = glass;
            else m = wood;                                           // desks, benches, cupboards
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            if (n == "panel_outlet") for (int i = 1; i < mats.Length; i++) mats[i] = black;   // the pins
            r.sharedMaterials = mats;
        }
    }

    // The false ceiling has rectangular cut-outs (light panels). Without something behind them you would see the
    // camera background through them, so a plain grey "plenum" plane sits just above the ceiling.
    static void AddPlenum(BMap map, float yFrom, float yTo)
    {
        var plenum = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plenum.name = "CeilingPlenum";
        Object.DestroyImmediate(plenum.GetComponent<Collider>());
        // a solid block whose underside is just below the cut-outs' top: the openings become shallow grey recesses
        plenum.transform.position = map.P(1.28f, (yFrom + yTo) / 2f, 2.61f);
        plenum.transform.localScale = map.Size(new Vector3(3.2f, yTo - yFrom, 0.20f));
        plenum.GetComponent<MeshRenderer>().sharedMaterial = Mat("Cabin_Plenum", new Color(0.30f, 0.30f, 0.32f), 0.05f);
    }

    // The windows have nothing behind them, so in the flat realm look you would see the empty "void" colour through
    // the open panes. A plain panel behind each side of windows shows the realm's fill colour instead
    // (the window frames keep their outlines, because the panel is further away than the wall).
    static void AddWindowBackings(BMap map, float yFrom, float yTo)
    {
        var mat = Mat("Cabin_WindowBacking", new Color(0.80f, 0.80f, 0.80f), 0.05f);
        foreach (float x in new[] { -0.32f, 2.88f })
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = "WindowBacking";
            Object.DestroyImmediate(b.GetComponent<Collider>());
            b.transform.position = map.P(x, (yFrom + yTo) / 2f, 1.45f);
            b.transform.localScale = map.Size(new Vector3(0.02f, yTo - yFrom, 2.9f));
            b.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }

    // keep only what belongs to the room: furniture outside [yMin, yMax] (Blender y) is deleted; the building shell stays
    static void Cull(GameObject root, BMap map, float yMin, float yMax)
    {
        var doomed = new List<GameObject>();
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            string n = r.name;
            if (n == "Plane" || n == "cabin-nearviblab" || n.StartsWith("ceiling")) continue;
            float by = map.sy * r.bounds.center.z;
            if (by < yMin || by > yMax) doomed.Add(r.gameObject);
        }
        foreach (var go in doomed) if (go != null) Object.DestroyImmediate(go);
    }

    static void BoxMinMax(Transform parent, BMap map, string name, Vector3 bMin, Vector3 bMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var bc = go.AddComponent<BoxCollider>();
        Vector3 centre = (bMin + bMax) / 2f;
        bc.center = map.P(centre.x, centre.y, centre.z);
        bc.size = map.Size(bMax - bMin);
    }

    static void MeshBox(GameObject root, string name, float grow = 0f)
    {
        Transform t = FindT(root, name);
        if (t == null) return;
        var mf = t.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        var bc = t.gameObject.GetComponent<BoxCollider>();
        if (bc == null) bc = t.gameObject.AddComponent<BoxCollider>();
        bc.center = mf.sharedMesh.bounds.center;
        bc.size = mf.sharedMesh.bounds.size + Vector3.one * grow;
    }

    // walls / floor / ceiling of the cabins as plain boxes (measured from the blend: inner faces x 0.002..2.566)
    static void ShellColliders(Transform parent, BMap map, float yFrom, float yTo, float yInnerFrom, float yInnerTo)
    {
        const float x0 = -0.243f, x1 = 2.793f, xi0 = 0.002f, xi1 = 2.566f, zLow = -0.6f, zHigh = 2.9f;
        BoxMinMax(parent, map, "WallWest", new Vector3(x0, yFrom, zLow), new Vector3(xi0, yTo, zHigh));
        BoxMinMax(parent, map, "WallEast", new Vector3(xi1, yFrom, zLow), new Vector3(x1, yTo, zHigh));      // also fills the old door frames
        BoxMinMax(parent, map, "WallSouth", new Vector3(x0, yFrom, zLow), new Vector3(x1, yInnerFrom, zHigh));
        BoxMinMax(parent, map, "WallNorth", new Vector3(x0, yInnerTo, zLow), new Vector3(x1, yTo, zHigh));
        BoxMinMax(parent, map, "Floor", new Vector3(x0, yFrom, zLow), new Vector3(x1, yTo, 0f));
        BoxMinMax(parent, map, "Ceiling", new Vector3(x0, yFrom, 2.5f), new Vector3(x1, yTo, zHigh));
    }

    static GameObject InstantiateModel(out BMap map)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (PrefabUtility.IsPartOfPrefabInstance(inst))
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.name = "Cabins";
        inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        inst.transform.localScale = Vector3.one;
        map = BMap.Calibrate(inst);
        return inst;
    }

    static GameObject SpawnPlayer(BMap map, Vector3 blenderPos, Vector3 blenderFacing)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        player.transform.position = map.P(blenderPos.x, blenderPos.y, blenderPos.z);
        Vector3 f = map.Dir(blenderFacing); f.y = 0f;
        player.transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
        return player;
    }

    static bool SceneIsClean()
    {
        if (EditorSceneManager.GetActiveScene().isDirty)
        {
            Debug.LogWarning("[CabinSceneBuilder] The open scene has unsaved changes. Save it (Ctrl+S) first, then run this again, so nothing gets lost.");
            return false;
        }
        return true;
    }

    // ================================================================ TUTORIAL (cabin A)
    public static bool BuildTutorial()
    {
        if (!SceneIsClean()) return false;
        EnsureModelSettings();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BMap map;
        GameObject root = InstantiateModel(out map);
        if (!map.ok) { Debug.LogError("[CabinSceneBuilder] Stopping: the axis mapping is not what I expected. Tell Claude what the log says."); return false; }

        Cull(root, map, 3.0f, 7.0f);                       // only room A's furniture
        Paint(root);
        AddPlenum(map, 3.0f, 7.05f);

        // ---- colliders
        var colRoot = new GameObject("Colliders").transform;
        ShellColliders(colRoot, map, 2.90f, 7.15f, 3.124f, 6.904f);
        foreach (var n in new[] { "Cube.005", "Computer Chair.002", "Chair2_1.004", "Chair2_1.005" }) MeshBox(root, n);

        // ---- the cupboard (body + two doors) with a dark inside, shelves and the note
        Transform body = FindT(root, "Cube.006"), door1 = FindT(root, "Cube.003"), door2 = FindT(root, "Cube.004");
        if (body == null || door1 == null || door2 == null) { Debug.LogError("[CabinSceneBuilder] Cupboard parts (Cube.006/.003/.004) not found."); return false; }
        door1.SetParent(body, true); door2.SetParent(body, true);
        MeshBox(root, "Cube.006"); MeshBox(root, "Cube.003"); MeshBox(root, "Cube.004");
        var cup = body.gameObject.AddComponent<OpenableCupboard>();
        cup.doors = new[] { door1, door2 };
        cup.frontDirection = map.Dir(new Vector3(0, -1, 0));              // it faces south, into the room
        cup.revealOnOpen = BuildCupboardInside(body, cup.frontDirection);

        // ---- ceiling lights
        var lightsGo = new GameObject("RoomLights");
        var roomLights = lightsGo.AddComponent<RoomLights>();
        var fixtureMat = Mat("Cabin_LightTube", new Color(0.95f, 0.95f, 0.9f), 0.2f, 0f, true, new Color(1.1f, 1.05f, 0.95f));
        var lightList = new List<Light>();
        var fixtureList = new List<Renderer>();
        foreach (float yb in new[] { 4.35f, 6.15f })
        {
            var tube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tube.name = "LightTube";
            Object.DestroyImmediate(tube.GetComponent<Collider>());
            tube.transform.position = map.P(1.28f, yb, 2.46f);
            tube.transform.localScale = map.Size(new Vector3(0.14f, 1.0f, 0.03f));
            tube.GetComponent<MeshRenderer>().sharedMaterial = fixtureMat;
            tube.transform.SetParent(lightsGo.transform, true);
            fixtureList.Add(tube.GetComponent<Renderer>());

            var lg = new GameObject("CeilingLight");
            lg.transform.SetParent(lightsGo.transform, true);
            lg.transform.position = map.P(1.28f, yb, 2.25f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 6f;
            l.intensity = 1.5f;
            l.color = new Color(1f, 0.96f, 0.88f);
            l.shadows = LightShadows.None;                               // cheap on low-end laptops
            lightList.Add(l);
        }
        roomLights.lights = lightList.ToArray();
        roomLights.fixtures = fixtureList.ToArray();
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = roomLights.ambientColor * roomLights.ambientOn;
        RenderSettings.skybox = null;

        // ---- the switch panel: only the left rocker works
        Transform rocker = FindT(root, "cabin_switch_lights");
        if (rocker == null) { Debug.LogError("[CabinSceneBuilder] cabin_switch_lights not found."); return false; }
        var rbc = rocker.gameObject.AddComponent<BoxCollider>();
        var rmf = rocker.GetComponent<MeshFilter>();
        rbc.center = rmf.sharedMesh.bounds.center;
        rbc.size = rmf.sharedMesh.bounds.size + Vector3.one * 0.01f;
        var ls = rocker.gameObject.AddComponent<LightSwitch>();
        ls.lights = roomLights;
        ls.frontDirection = map.Dir(new Vector3(0, 1, 0));                // the panel is on the south wall, facing north into the room

        // ---- the portal where the old door frame was (east wall), in front of the filled-in wall so you can see it
        var portal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        portal.name = "CabinPortal";
        portal.transform.position = map.P(2.49f, 3.749f, 1.15f);
        portal.transform.localScale = map.Size(new Vector3(0.20f, 0.95f, 2.30f));
        portal.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PortalGlow.mat");
        var p = portal.AddComponent<Portal>();
        p.sceneToLoad = "Cabin_Combat";
        p.portalColor = CombatFill;
        p.accentColor = CombatLine;
        p.armDelay = 2.5f;          // the player is probably standing at the panel right next to it when it opens: let them see it first

        // ---- the player, and the director that runs the tutorial
        SpawnPlayer(map, new Vector3(2.05f, 3.75f, 0f), new Vector3(0f, 1f, 0f));       // by the old door frame, facing north down the room (cupboard, desk)
        new GameObject("PlayerSpawn").AddComponent<PlayerSpawn>();                       // R puts the player back here
        var dirGo = new GameObject("TutorialDirector");
        var dir = dirGo.AddComponent<TutorialDirector>();
        dir.cupboard = cup;
        dir.lightSwitch = ls;
        dir.portal = portal;

        EditorSceneManager.SaveScene(scene, TutorialPath);
        Debug.Log("[CabinSceneBuilder] Built " + TutorialPath);
        return true;
    }

    // a dark panel in front of the cupboard body, three shelves, and the note taped on it (hidden until the doors open)
    static GameObject BuildCupboardInside(Transform body, Vector3 front)
    {
        front = front.normalized;
        Bounds bb = body.GetComponent<Renderer>().bounds;
        Vector3 widthDir = Vector3.Cross(Vector3.up, front).normalized;
        Vector3 absFront = new Vector3(Mathf.Abs(front.x), 0f, Mathf.Abs(front.z));
        Vector3 absWidth = new Vector3(Mathf.Abs(widthDir.x), 0f, Mathf.Abs(widthDir.z));
        float depthHalf = Vector3.Dot(bb.extents, absFront);
        float width = 2f * Vector3.Dot(bb.extents, absWidth);
        float height = bb.size.y;
        Vector3 faceCentre = new Vector3(bb.center.x, bb.min.y, bb.center.z) + front * depthHalf;

        var dark = Mat("Cabin_Inside", new Color(0.06f, 0.045f, 0.035f), 0.05f);
        var wood = Mat("Cabin_Wood", new Color(0.42f, 0.28f, 0.17f), 0.3f);
        var paper = Mat("Cabin_Paper", new Color(0.96f, 0.93f, 0.82f), 0.05f);

        var inside = new GameObject("CupboardInside");
        inside.transform.SetParent(body, true);

        var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
        back.name = "InsideBack";
        Object.DestroyImmediate(back.GetComponent<Collider>());
        back.transform.SetParent(inside.transform, true);
        back.transform.position = faceCentre + Vector3.up * (height / 2f) + front * 0.002f;
        back.transform.rotation = Quaternion.LookRotation(-front, Vector3.up);          // a quad faces its local -Z
        back.transform.localScale = new Vector3(width * 0.88f, height * 0.9f, 1f);
        back.GetComponent<MeshRenderer>().sharedMaterial = dark;

        foreach (float h in new[] { 0.45f, 0.95f, 1.40f })
        {
            var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shelf.name = "Shelf";
            Object.DestroyImmediate(shelf.GetComponent<Collider>());
            shelf.transform.SetParent(inside.transform, true);
            shelf.transform.rotation = Quaternion.LookRotation(front, Vector3.up);       // local Z = out of the cupboard
            shelf.transform.position = faceCentre + Vector3.up * h + front * 0.017f;       // inside the thickness of the closed doors
            shelf.transform.localScale = new Vector3(width * 0.88f, 0.03f, 0.03f);
            shelf.GetComponent<MeshRenderer>().sharedMaterial = wood;
        }

        // the note, taped between the two upper shelves
        // note + its text live in one plain group (no scaling), so the text is not stretched; the cupboard reveals the group
        var noteGroup = new GameObject("NoteGroup");
        noteGroup.transform.SetParent(inside.transform, true);
        noteGroup.transform.position = faceCentre + Vector3.up * 1.18f + front * 0.006f;
        noteGroup.transform.rotation = Quaternion.LookRotation(-front, Vector3.up) * Quaternion.Euler(0f, 0f, 4f);   // slightly crooked

        var note = GameObject.CreatePrimitive(PrimitiveType.Quad);
        note.name = "Note";
        Object.DestroyImmediate(note.GetComponent<Collider>());
        note.transform.SetParent(noteGroup.transform, false);
        note.transform.localPosition = Vector3.zero;
        note.transform.localRotation = Quaternion.identity;
        note.transform.localScale = new Vector3(0.17f, 0.21f, 1f);
        note.GetComponent<MeshRenderer>().sharedMaterial = paper;

        var textGo = new GameObject("NoteText");
        textGo.transform.SetParent(noteGroup.transform, false);
        textGo.transform.localPosition = new Vector3(0f, 0f, 0.002f * -1f);     // a hair in front of the paper (local -Z is the visible side)
        textGo.transform.localRotation = Quaternion.identity;
        var tm = textGo.AddComponent<TextMesh>();
        tm.text = "Switch off\nthe lights\nbefore\nleaving.";
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.fontSize = 100;
        tm.characterSize = 0.0016f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.1f, 0.1f, 0.25f);
        WorldTextUtil.Apply(tm);
        return noteGroup;
    }

    // ================================================================ COMBAT CABIN (C + D, realm look)
    public static bool BuildCombat()
    {
        if (!SceneIsClean()) return false;
        EnsureModelSettings();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BMap map;
        GameObject root = InstantiateModel(out map);
        if (!map.ok) { Debug.LogError("[CabinSceneBuilder] Stopping: the axis mapping is not what I expected. Tell Claude what the log says."); return false; }

        Cull(root, map, -7.3f, -0.15f);                    // only what stands in cabins C and D
        Paint(root);
        AddPlenum(map, -7.3f, -0.10f);
        AddWindowBackings(map, -7.2f, -0.24f);

        var colRoot = new GameObject("Colliders").transform;
        ShellColliders(colRoot, map, -7.40f, -0.05f, -7.20f, -0.24f);     // one long room: the wall between C and D is gone
        foreach (var n in new[] { "Cube", "Cube.013", "Cube.010", "Cube.012" }) MeshBox(root, n);   // bench + cupboard

        // ---- the flat "realm" look: the same renderer feature the CAD lab uses
        var features = new List<ScriptableRendererFeature>();
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            var f = data == null ? null : data.rendererFeatures.FirstOrDefault(x => x != null && x.name == "RealmOutline");
            if (f != null) features.Add(f);
        }
        if (features.Count == 0) { Debug.LogError("[CabinSceneBuilder] The RealmOutline renderer feature is missing: run Tools > Realm Outline > Install first."); return false; }
        var rsGo = new GameObject("RealmStyle_Combat");
        var rs = rsGo.AddComponent<RealmStyle>();
        rs.outlineFeatures = features.ToArray();
        rs.fillColor = CombatFill;
        rs.lineColor = CombatLine;
        rs.voidColor = CombatLine;
        rs.lineThickness = 4f;

        // ---- arrive at the south end of the long room, looking north (towards where the fight will be)
        GameObject playerGo = SpawnPlayer(map, new Vector3(1.28f, -6.4f, 0f), new Vector3(0f, 1f, 0f));
        new GameObject("PlayerSpawn").AddComponent<PlayerSpawn>();                       // R puts the player back here

        // ---- the fight: you (textbook, block, dodge), the weak tutorial professor, the on-screen bars and the lessons
        var combat = playerGo.AddComponent<PlayerCombat>();                              // also adds the player's Health

        var profGo = new GameObject("Professor");
        profGo.transform.position = map.P(1.28f, -1.7f, 0f);                             // the north end of the long room
        profGo.transform.rotation = Quaternion.LookRotation(map.Dir(new Vector3(0f, -1f, 0f)), Vector3.up);   // facing south, towards you
        var prof = profGo.AddComponent<ProfessorAI>();                                   // adds its CharacterController and Health
        prof.GetComponent<Health>().max = 120f;
        prof.displayName = "Asst. Prof. GPT";

        var hudGo = new GameObject("CombatHUD");
        var hud = hudGo.AddComponent<CombatHUD>();
        hud.player = combat; hud.enemy = prof;

        // the portal on to the next level appears where the old door of cabin C was, when he is beaten
        var exit = GameObject.CreatePrimitive(PrimitiveType.Cube);
        exit.name = "CombatPortal";
        exit.transform.position = map.P(2.49f, -2.34f, 1.15f);
        exit.transform.localScale = map.Size(new Vector3(0.20f, 0.95f, 2.30f));
        exit.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PortalGlow.mat");
        var exitPortal = exit.AddComponent<Portal>();
        exitPortal.sceneToLoad = "Classroom";
        exitPortal.portalColor = NextFill;
        exitPortal.accentColor = NextLine;
        exitPortal.armDelay = 2.5f;
        exit.SetActive(false);

        var tutGo = new GameObject("CombatTutorial");
        var tut = tutGo.AddComponent<CombatTutorial>();
        tut.professor = prof; tut.player = combat; tut.portal = exit;

        RenderSettings.skybox = null;
        EditorSceneManager.SaveScene(scene, CombatPath);
        Debug.Log("[CabinSceneBuilder] Built " + CombatPath);
        return true;
    }

    // ================================================================ build list: the cabins come first
    public static void UpdateBuildList()
    {
        var wanted = new[] { TutorialPath, CombatPath, "Assets/Scenes/Classroom.unity", "Assets/Scenes/Cad_lab.unity" };
        var list = new List<EditorBuildSettingsScene>();
        foreach (string p in wanted)
            if (System.IO.File.Exists(p)) list.Add(new EditorBuildSettingsScene(p, true));
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log("[CabinSceneBuilder] Build list: " + string.Join(" -> ", list.Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))));
    }
}
#endif
