#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// VERSION 12
// EDITOR TOOL (runs inside Unity, not in the game).
// Menu: Tools > Rebuild CAD Lab Colliders
//
// Replaces the CAD lab's messy auto-generated colliders (every chair leg, cable, computer...)
// with clean invisible boxes: floor, walls (with real doorways left open), desks, benches and tables.
// The classroom doors are left OPEN (no box), so you can walk into both classrooms.
// Furniture boxes (desks, benches, tables) are ONLY made if that object still exists and is ticked on,
// so if you delete/untick furniture (e.g. the benches), its invisible box goes away too.
// The box positions were measured from FINAL_CAD_LAB.blend, so they line up with the model.
// Chairs, cables and computers get NO collider, so the player can't snag on them.
// Ctrl+Z undoes everything.
public static class CadLabColliders
{
    struct Box
    {
        public string name; public Vector3 centre, size; public float rotY;
        public Box(string n, float cx, float cy, float cz, float sx, float sy, float sz, float ry)
        { name = n; centre = new Vector3(cx, cy, cz); size = new Vector3(sx, sy, sz); rotY = ry; }
    }

    // Positions are LOCAL to the FINAL_CAD_LAB object (X, Y, Z, then size X, Y, Z, then Y rotation)
    static readonly Box[] Boxes =
    {
        new Box("Wall", -12.092f, 1.250f, -5.173f, 0.150f, 2.500f, 0.330f, -90.000f),
        new Box("Wall", 0.453f, 1.251f, -4.146f, 0.150f, 2.503f, 1.680f, 180.000f),
        new Box("Wall", -12.401f, 1.250f, -13.104f, 0.150f, 2.500f, 1.860f, 0.000f),
        new Box("Wall", -17.120f, 1.250f, -12.270f, 0.150f, 2.500f, 3.510f, 180.000f),
        new Box("Wall", -12.341f, 1.250f, -4.203f, 0.150f, 2.500f, 1.800f, 0.000f),
        new Box("Wall", -17.664f, 1.250f, -16.779f, 0.150f, 2.500f, 0.660f, 0.000f),
        new Box("Wall", -11.857f, 1.250f, -0.447f, 0.150f, 2.500f, 0.900f, 180.000f),
        new Box("Wall", -0.078f, 1.250f, -16.659f, 0.150f, 2.500f, 0.900f, 0.000f),
        new Box("Wall", -11.857f, 1.250f, -16.764f, 0.150f, 2.500f, 0.660f, 180.000f),
        new Box("Wall", 0.187f, 1.251f, 0.073f, 0.150f, 2.503f, 0.390f, 90.000f),
        new Box("Wall", 0.187f, 1.250f, -16.125f, 0.150f, 2.500f, 0.390f, 90.000f),
        new Box("Wall", -17.399f, 1.250f, -10.445f, 0.150f, 2.500f, 0.390f, 90.000f),
        new Box("Wall", 0.188f, 1.251f, -5.063f, 0.150f, 2.503f, 0.390f, -90.000f),
        new Box("Wall", 0.303f, 1.251f, -6.482f, 0.150f, 2.503f, 3.000f, 0.000f),
        new Box("Wall", -12.122f, 1.250f, -17.179f, 0.150f, 2.500f, 0.390f, -90.000f),
        new Box("Wall", -17.120f, 1.250f, -6.218f, 0.150f, 2.500f, 5.880f, 180.000f),
        new Box("Wall", -0.078f, 1.251f, -4.153f, 0.150f, 2.503f, 1.680f, 0.000f),
        new Box("Wall", 0.072f, 1.251f, -6.483f, 0.150f, 2.503f, 3.000f, 180.000f),
        new Box("Wall", -12.136f, 1.250f, -16.364f, 0.150f, 2.500f, 0.390f, 90.000f),
        new Box("Wall", -17.690f, 1.250f, -0.446f, 0.150f, 2.500f, 0.900f, 0.000f),
        new Box("Wall", 0.453f, 1.251f, -0.447f, 0.150f, 2.503f, 0.900f, 180.000f),
        new Box("Wall", 0.188f, 1.251f, -5.063f, 0.150f, 2.503f, 0.390f, -90.000f),
        new Box("Wall", -12.401f, 1.250f, -16.779f, 0.150f, 2.500f, 0.660f, 0.000f),
        new Box("Wall", -17.385f, 1.250f, -17.513f, 0.150f, 2.500f, 0.390f, -90.000f),
        new Box("Wall", -12.122f, 1.250f, -14.104f, 0.150f, 2.500f, 0.390f, -90.000f),
        new Box("Wall", -12.106f, 1.250f, 0.073f, 0.150f, 2.500f, 0.330f, 90.000f),
        new Box("Wall", -17.664f, 1.250f, -17.278f, 0.150f, 2.500f, 0.330f, 0.000f),
        new Box("Wall", -11.857f, 1.250f, -4.203f, 0.150f, 2.500f, 1.800f, 180.000f),
        new Box("Wall", 0.187f, 1.251f, -3.236f, 0.150f, 2.503f, 0.390f, 90.000f),
        new Box("Wall", -14.552f, 1.250f, -6.705f, 0.150f, 2.500f, 5.250f, -90.169f),
        new Box("Wall", -14.576f, 1.250f, -6.221f, 0.150f, 2.500f, 5.250f, 89.831f),
        new Box("Wall", -12.106f, 1.250f, -3.233f, 0.150f, 2.500f, 0.330f, 90.000f),
        new Box("Wall", -0.078f, 1.251f, -0.470f, 0.150f, 2.503f, 0.900f, 0.000f),
        new Box("Wall", -8.508f, 1.250f, -17.513f, 0.150f, 2.500f, 17.340f, -90.000f),
        new Box("Wall", 0.188f, 1.251f, -0.990f, 0.150f, 2.503f, 0.390f, -90.000f),
        new Box("Wall", -11.857f, 1.250f, -6.448f, 0.150f, 2.500f, 0.330f, 180.000f),
        new Box("Wall", -8.605f, 1.250f, -17.029f, 0.150f, 2.500f, 17.190f, 90.000f),
        new Box("Wall", -12.341f, 1.250f, -0.452f, 0.150f, 2.500f, 0.900f, 0.000f),
        new Box("Wall", -17.664f, 1.250f, -12.291f, 0.150f, 2.500f, 3.510f, 0.000f),
        new Box("Wall", 0.453f, 1.250f, -17.264f, 0.150f, 2.500f, 0.330f, 180.000f),
        new Box("Wall", -12.092f, 1.250f, -0.972f, 0.150f, 2.500f, 0.330f, -90.000f),
        new Box("Wall", 0.278f, 1.250f, -17.513f, 0.150f, 2.500f, 0.210f, -90.000f),
        new Box("Wall", -17.385f, 1.250f, -14.116f, 0.150f, 2.500f, 0.390f, -90.000f),
        new Box("Wall", 0.188f, 1.251f, -13.899f, 0.150f, 2.503f, 0.390f, -90.000f),
        new Box("Wall", 0.187f, 1.250f, -13.749f, 0.150f, 2.500f, 0.390f, 90.000f),
        new Box("Wall", -17.120f, 1.250f, -16.766f, 0.150f, 2.500f, 0.660f, 180.000f),
        new Box("Wall", 0.453f, 1.250f, -16.645f, 0.150f, 2.500f, 0.900f, 180.000f),
        new Box("Wall", -17.399f, 1.250f, -16.366f, 0.150f, 2.500f, 0.390f, 90.000f),
        new Box("Wall", 0.453f, 1.251f, -11.491f, 0.150f, 2.503f, 4.680f, 180.000f),
        new Box("Wall", 0.187f, 1.251f, -13.749f, 0.150f, 2.503f, 0.390f, 90.000f),
        new Box("Wall", -17.690f, 1.250f, -6.227f, 0.150f, 2.500f, 5.880f, 0.000f),
        new Box("Wall", -0.078f, 1.251f, -11.489f, 0.150f, 2.503f, 4.680f, 0.000f),
        new Box("Wall", -10.544f, 1.250f, -8.663f, 0.150f, 2.500f, 0.330f, 0.000f),
        new Box("Wall", 0.453f, 1.251f, 0.188f, 0.150f, 2.503f, 0.360f, 180.000f),
        new Box("Wall", 0.072f, 1.250f, -8.649f, 0.150f, 2.500f, 0.330f, 180.000f),
        new Box("Wall", -8.407f, 1.251f, -0.077f, 0.150f, 2.503f, 17.580f, -90.000f),
        new Box("Wall", -17.410f, 1.250f, -3.208f, 0.150f, 2.500f, 0.420f, 90.000f),
        new Box("Wall", -5.233f, 1.250f, -8.898f, 0.150f, 2.500f, 10.470f, -90.000f),
        new Box("Wall", -5.239f, 1.250f, -8.414f, 0.150f, 2.500f, 10.470f, 90.000f),
        new Box("Wall", -8.410f, 1.251f, 0.396f, 0.150f, 2.503f, 17.580f, 90.000f),
        new Box("Wall", -17.120f, 1.250f, -0.447f, 0.150f, 2.500f, 0.900f, 180.000f),
        new Box("Wall", -10.394f, 1.250f, -8.576f, 0.150f, 2.500f, 4.140f, 180.000f),
        new Box("Wall", -17.690f, 1.250f, 0.188f, 0.150f, 2.500f, 0.390f, 0.000f),
        new Box("Wall", -17.400f, 1.250f, -0.966f, 0.150f, 2.500f, 0.420f, -90.000f),
        new Box("Wall", -10.878f, 1.250f, -8.577f, 0.150f, 2.500f, 4.140f, 0.000f),
        new Box("Wall", -17.410f, 1.250f, 0.396f, 0.150f, 2.500f, 0.420f, 90.000f),
        new Box("Wall", -12.136f, 1.250f, -12.081f, 0.150f, 2.500f, 0.390f, 90.000f),
        new Box("Wall", 0.072f, 1.251f, -8.557f, 0.150f, 2.503f, 1.170f, 180.000f),
        new Box("Wall", -10.629f, 1.250f, -10.717f, 0.150f, 2.500f, 0.330f, -90.000f),
        new Box("Wall", -11.857f, 1.250f, -13.081f, 0.150f, 2.500f, 1.860f, 180.000f),
        new Box("Wall", 0.303f, 1.251f, -8.576f, 0.150f, 2.503f, 1.170f, 0.000f),
        new Box("Wall", -10.643f, 1.250f, -6.436f, 0.150f, 2.500f, 0.330f, 90.000f),
        new Box("Wall", -17.400f, 1.250f, -9.237f, 0.150f, 2.500f, 0.420f, -90.000f),
        new Box("Wall", -11.859f, 1.250f, -10.710f, 0.150f, 2.500f, 0.390f, 180.000f),
        new Box("Wall", -14.575f, 1.250f, -10.445f, 0.150f, 2.500f, 5.250f, 90.000f),
        new Box("Wall", -14.552f, 1.250f, -10.988f, 0.150f, 2.500f, 5.250f, -90.000f),
        new Box("Wall", -17.270f, 1.250f, -10.723f, 0.150f, 2.500f, 0.390f, 0.000f),
        new Box("Wall", 0.465f, 0.401f, -15.004f, 0.150f, 0.803f, 2.370f, 180.000f),
        new Box("Wall", 0.200f, 0.401f, -16.275f, 0.150f, 0.803f, 0.390f, -90.000f),
        new Box("Wall", 0.187f, 0.401f, -13.749f, 0.150f, 0.803f, 0.390f, 90.000f),
        new Box("Wall", -0.078f, 0.401f, -15.020f, 0.150f, 0.803f, 2.370f, 0.000f),
        new Box("Wall", -11.857f, 0.400f, -15.219f, 0.150f, 0.800f, 2.400f, 180.000f),
        new Box("Wall", -12.401f, 0.400f, -15.244f, 0.150f, 0.800f, 2.400f, 0.000f),
        new Box("Wall", -17.120f, 0.400f, -15.236f, 0.150f, 0.800f, 2.400f, 180.000f),
        new Box("Wall", -17.664f, 0.400f, -15.246f, 0.150f, 0.800f, 2.400f, 0.000f),
        new Box("Wall", -17.690f, 0.400f, -2.088f, 0.150f, 0.800f, 2.400f, 0.000f),
        new Box("Wall", -17.120f, 0.400f, -2.086f, 0.150f, 0.800f, 2.400f, 180.000f),
        new Box("Wall", -11.857f, 0.400f, -2.092f, 0.150f, 0.800f, 2.400f, 180.000f),
        new Box("Wall", -12.341f, 0.400f, -2.113f, 0.150f, 0.800f, 2.400f, 0.000f),
        new Box("Wall", 0.453f, 0.401f, -2.095f, 0.150f, 0.803f, 2.370f, 180.000f),
        new Box("Wall", -0.078f, 0.401f, -2.111f, 0.150f, 0.803f, 2.370f, 0.000f),
        new Box("Wall", -17.270f, 1.250f, -6.478f, 0.150f, 2.500f, 0.330f, 0.000f),
        new Box("Wall", -12.092f, 0.400f, -3.383f, 0.150f, 0.800f, 0.330f, -90.000f),
        new Box("Floor", -6.426f, -0.100f, -7.186f, 23.611f, 0.200f, 23.960f, 0.000f),
        new Box("Cube.009", -4.630f, 0.354f, -4.447f, 9.242f, 0.710f, 1.652f, 0.000f),
        new Box("Cube.025", -5.554f, 0.354f, -16.686f, 11.091f, 0.710f, 0.834f, 0.000f),
        new Box("Cube.025", -4.630f, 0.354f, -9.239f, 9.242f, 0.710f, 0.826f, 0.000f),
        new Box("Cube.025", -3.706f, 0.354f, -12.896f, 7.394f, 0.710f, 1.661f, 0.000f),
        new Box("Cube.030", -4.630f, 0.354f, -0.424f, 9.242f, 0.710f, 0.826f, 0.000f),
        new Box("Cube.030", -4.630f, 0.354f, -8.073f, 9.242f, 0.710f, 0.826f, 0.000f),
        new Box("Cube.003_Material.003_0.001", -13.378f, 0.424f, -2.590f, 1.966f, 0.879f, 5.061f, 0.000f),
        new Box("Cube.003_Material.003_0.002", -16.050f, 0.424f, -2.590f, 1.966f, 0.879f, 5.061f, 0.000f),
        new Box("Cube.003_Material.003_0.003", -15.897f, 0.424f, -14.598f, 1.966f, 0.879f, 4.983f, 0.000f),
        new Box("Cube.003_Material.003_0.004", -13.376f, 0.424f, -14.598f, 1.966f, 0.879f, 4.983f, 0.000f),
        new Box("teachers table", -8.917f, 0.351f, -11.136f, 0.708f, 0.689f, 1.314f, 0.000f),
        new Box("teachers table.001", -10.384f, 0.352f, -2.571f, 0.708f, 0.689f, 1.314f, 0.000f),
        new Box("EntranceBlock", -17.450f, 1.250f, -9.850f, 0.300f, 2.500f, 1.600f, 0.000f),
        new Box("Fence_N", -8.600f, 1.500f, 0.750f, 19.000f, 3.000f, 0.300f, 0.000f),
        new Box("Fence_S", -8.600f, 1.500f, -17.850f, 19.000f, 3.000f, 0.300f, 0.000f),
        new Box("Fence_W", -18.000f, 1.500f, -8.550f, 0.300f, 3.000f, 19.000f, 0.000f),
        new Box("Fence_E", 0.800f, 1.500f, -8.550f, 0.300f, 3.000f, 19.000f, 0.000f),
    };

    [MenuItem("Tools/Rebuild CAD Lab Colliders")]
    static void Rebuild()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null || !root.name.Contains("CAD")) root = GameObject.Find("FINAL_CAD_LAB");
        if (root == null)
        {
            EditorUtility.DisplayDialog("CAD Lab Colliders", "Couldn't find FINAL_CAD_LAB. Select the CAD lab model in the Hierarchy and run this again.", "OK");
            return;
        }

        // 1. Remove ALL old colliders inside the model (the cause of getting stuck)
        int removed = 0;
        foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
        {
            Undo.DestroyObjectImmediate(c);
            removed++;
        }

        // 2. Remove a previous run of this tool, if any
        Transform old = root.transform.Find("SimpleColliders");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // 3. Build the clean boxes
        GameObject holder = new GameObject("SimpleColliders");
        Undo.RegisterCreatedObjectUndo(holder, "Rebuild CAD Lab Colliders");
        holder.transform.SetParent(root.transform, false);
        holder.isStatic = true;

        // Names of furniture that still exists (and is switched on) inside the model
        var present = new System.Collections.Generic.HashSet<string>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.gameObject.activeInHierarchy) present.Add(t.name);

        int i = 0, skipped = 0;
        foreach (Box b in Boxes)
        {
            bool structural = b.name == "Wall" || b.name == "Floor" || b.name.StartsWith("Fence") || b.name == "EntranceBlock";
            if (!structural && !present.Contains(b.name)) { skipped++; continue; }   // furniture was deleted

            GameObject go = new GameObject(b.name + "_" + (++i));
            go.transform.SetParent(holder.transform, false);
            go.transform.localPosition = b.centre;
            go.transform.localRotation = Quaternion.Euler(0f, b.rotY, 0f);
            go.isStatic = true;
            go.AddComponent<BoxCollider>().size = b.size;
        }

        Selection.activeGameObject = holder;
        Debug.Log("[CadLabColliders] Removed " + removed + " old colliders, added " + i + " clean boxes, skipped " + skipped + " for deleted furniture.");
        EditorUtility.DisplayDialog("CAD Lab Colliders",
            "Done!\n\nRemoved " + removed + " old colliders\nAdded " + i + " clean boxes (green outlines in the Scene view)\nSkipped " + skipped + " boxes for furniture you deleted\n\nSave the scene (Ctrl+S).", "OK");
    }
}
#endif