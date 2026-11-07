#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Editor tools for the "realm" look (see RealmStyle.cs).
//  Tools > Realm Outline > Install            : adds the outline pass to every URP renderer in the project (switched OFF; RealmStyle turns it on)
//  Tools > Realm Outline > Add To Open Scene  : adds a RealmStyle object to the open scene, wired to the feature and to the portal named "LabPortal"
// Both can be run again safely: they never add things twice.
public static class RealmOutlineSetup
{
    const string FeatureName = "RealmOutline";
    const string ShaderPath = "Assets/Shaders/RealmOutline.shader";
    const string MaterialPath = "Assets/Materials/RealmOutline.mat";

    [MenuItem("Tools/Realm Outline/Install")]
    public static void Install()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null) { Debug.LogError("[RealmOutline] Shader not found at " + ShaderPath); return; }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(shader) { name = "RealmOutline" };
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null) continue;

            // Renderers inside packages (e.g. URP's built-in default) are read-only: never touch them,
            // and undo it if an earlier run did.
            if (!path.StartsWith("Assets/"))
            {
                var stray = data.rendererFeatures.Where(f => f != null && f.name == FeatureName).ToList();
                foreach (var s in stray)
                {
                    data.rendererFeatures.Remove(s);
                    AssetDatabase.RemoveObjectFromAsset(s);
                    Object.DestroyImmediate(s, true);
                }
                if (stray.Count > 0) { EditorUtility.SetDirty(data); Debug.Log("[RealmOutline] Removed a stray feature from package asset " + path); }
                continue;
            }

            var feature = data.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f => f != null && f.name == FeatureName);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = FeatureName;
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
                Debug.Log("[RealmOutline] Added the feature to " + path);
            }

            feature.passMaterial = mat;
            feature.passIndex = 0;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;
            feature.fetchColorBuffer = false;                 // the pass paints over the scene, it doesn't need it
            feature.bindDepthStencilAttachment = false;
            feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            feature.SetActive(false);                         // RealmStyle switches it on only inside a realm

            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();   // gives the new feature its file id

        // URP keeps a list of the features' file ids next to the list of features (m_RendererFeatureMap).
        // The renderer inspector normally refreshes it; we do it by hand (private method, hence reflection).
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            typeof(ScriptableRendererData).GetMethod("UpdateMap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(data, null);
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[RealmOutline] Install finished.");
    }

    [MenuItem("Tools/Realm Outline/Add To Open Scene")]
    public static void AddToOpenScene()
    {
        // The feature of EVERY renderer in the project (each renderer asset has its own copy)
        var features = new System.Collections.Generic.List<ScriptableRendererFeature>();
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            var f = data == null ? null : data.rendererFeatures.FirstOrDefault(x => x != null && x.name == FeatureName);
            if (f != null) features.Add(f);
        }
        if (features.Count == 0) { Debug.LogError("[RealmOutline] Run Tools > Realm Outline > Install first."); return; }

        var rs = Object.FindAnyObjectByType<RealmStyle>();
        bool isNew = rs == null;
        if (isNew)
        {
            var created = new GameObject("RealmStyle");
            Undo.RegisterCreatedObjectUndo(created, "Add RealmStyle");
            rs = created.AddComponent<RealmStyle>();
        }
        var go = rs.gameObject;
        rs.outlineFeatures = features.ToArray();
        if (rs.endsWhenUsed == null)
        {
            GameObject portal = GameObject.Find("LabPortal");
            if (portal != null) rs.endsWhenUsed = portal.GetComponent<Portal>();
            else Debug.LogWarning("[RealmOutline] No object called LabPortal in this scene: set 'Ends When Used' yourself.");
        }
        Debug.Log("[RealmOutline] " + (isNew ? "Added" : "Updated") + " RealmStyle with " + features.Count + " renderer feature(s).", go);
        EditorUtility.SetDirty(go);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeObject = go;
    }
}
#endif
