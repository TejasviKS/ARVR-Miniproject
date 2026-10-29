#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// EDITOR TOOL (runs inside Unity, not in the game).
// Menu: Tools > Make Box Colliders For Selected
//
// What it does:
// 1. Reads the selected object's mesh (e.g. all 15 benches as one object).
// 2. Finds the separate pieces inside it (each bench, desk, leg...).
// 3. Merges pieces that touch or are very close into one group (= one bench unit).
// 4. Puts one simple invisible Box Collider around each group.
// 5. Removes the detailed Mesh Collider so the player stops getting stuck.
// Ctrl+Z undoes everything.
public static class BenchColliderMaker
{
    const float MergeGap = 0.05f;   // pieces closer than 5 cm count as the same bench
    const float WeldStep = 0.001f;  // vertices within 1 mm count as the same point
    const float MinSize = 0.05f;    // ignore leftover specks smaller than 5 cm

    [MenuItem("Tools/Make Box Colliders For Selected")]
    static void Make()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Box Colliders", "Click the benches object in the Hierarchy first, then run this again.", "OK");
            return;
        }

        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            EditorUtility.DisplayDialog("Box Colliders", "\"" + go.name + "\" has no mesh. Select the actual benches object (the one with a Mesh Filter).", "OK");
            return;
        }

        Mesh mesh = mf.sharedMesh;
        Vector3[] verts;
        int[] tris;
        try
        {
            verts = mesh.vertices;
            tris = mesh.triangles;
        }
        catch
        {
            EditorUtility.DisplayDialog("Box Colliders", "Can't read the mesh. Click the FBX in the Project window > Model tab > tick Read/Write > Apply, then try again.", "OK");
            return;
        }

        // --- Step 1: world positions, and weld vertices that sit on the same spot ---
        Transform t = go.transform;
        Vector3[] world = new Vector3[verts.Length];
        int[] weldId = new int[verts.Length];
        var lookup = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < verts.Length; i++)
        {
            world[i] = t.TransformPoint(verts[i]);
            Vector3Int key = Vector3Int.RoundToInt(world[i] / WeldStep);
            if (!lookup.TryGetValue(key, out int id))
            {
                id = lookup.Count;
                lookup.Add(key, id);
            }
            weldId[i] = id;
        }

        // --- Step 2: connect vertices that share a triangle (union-find) ---
        int[] parent = new int[lookup.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        for (int i = 0; i < tris.Length; i += 3)
        {
            Union(parent, weldId[tris[i]], weldId[tris[i + 1]]);
            Union(parent, weldId[tris[i]], weldId[tris[i + 2]]);
        }

        // --- Step 3: one bounding box per connected piece ---
        var pieceBounds = new Dictionary<int, Bounds>();
        for (int i = 0; i < verts.Length; i++)
        {
            int root = Find(parent, weldId[i]);
            if (pieceBounds.TryGetValue(root, out Bounds b)) { b.Encapsulate(world[i]); pieceBounds[root] = b; }
            else pieceBounds[root] = new Bounds(world[i], Vector3.zero);
        }
        var boxes = new List<Bounds>(pieceBounds.Values);

        // --- Step 4: merge boxes that touch or nearly touch ---
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = 0; i < boxes.Count; i++)
            {
                Bounds grown = boxes[i];
                grown.Expand(MergeGap);
                for (int j = boxes.Count - 1; j > i; j--)
                {
                    if (grown.Intersects(boxes[j]))
                    {
                        Bounds merged = boxes[i];
                        merged.Encapsulate(boxes[j]);
                        boxes[i] = merged;
                        boxes.RemoveAt(j);
                        grown = merged;
                        grown.Expand(MergeGap);
                        changed = true;
                    }
                }
            }
        }

        // --- Step 5: create the invisible box colliders ---
        GameObject holder = new GameObject(go.name + "_BoxColliders");
        Undo.RegisterCreatedObjectUndo(holder, "Make Box Colliders");
        int count = 0;
        foreach (Bounds b in boxes)
        {
            Vector3 s = b.size;
            if (s.x < MinSize && s.y < MinSize && s.z < MinSize) continue;

            GameObject box = new GameObject("Box_" + (count + 1));
            box.transform.SetParent(holder.transform, false);
            box.transform.position = b.center;
            BoxCollider col = box.AddComponent<BoxCollider>();
            col.size = new Vector3(Mathf.Max(s.x, 0.02f), Mathf.Max(s.y, 0.02f), Mathf.Max(s.z, 0.02f));
            count++;
        }

        // --- Step 6: remove the detailed mesh collider that causes snagging ---
        MeshCollider mc = go.GetComponent<MeshCollider>();
        if (mc != null) Undo.DestroyObjectImmediate(mc);

        Selection.activeGameObject = holder;
        Debug.Log("[BenchColliderMaker] Made " + count + " box colliders for \"" + go.name + "\" from " + pieceBounds.Count + " pieces.");

        if (count <= 2)
            EditorUtility.DisplayDialog("Box Colliders",
                "Only made " + count + " box(es). The selected object might also contain the floor or walls, so everything got merged. Press Ctrl+Z and make sure you selected only the benches.", "OK");
    }

    static int Find(int[] p, int x)
    {
        while (p[x] != x) { p[x] = p[p[x]]; x = p[x]; }
        return x;
    }

    static void Union(int[] p, int a, int b)
    {
        a = Find(p, a); b = Find(p, b);
        if (a != b) p[a] = b;
    }
}
#endif
