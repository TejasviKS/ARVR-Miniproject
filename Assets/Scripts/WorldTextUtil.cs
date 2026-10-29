using UnityEngine;

// VERSION 9
// Helper used by PanelSwitch and CodeNote: makes a TextMesh use the "Custom/WorldText" shader
// (Assets/Resources/WorldText.shader) so the text is hidden behind objects instead of showing through them.
public static class WorldTextUtil
{
    static Shader shader;

    public static void Apply(TextMesh tm)
    {
        MeshRenderer mr = tm.GetComponent<MeshRenderer>();
        if (shader == null) shader = Shader.Find("Custom/WorldText");
        if (shader == null)
        {
            Debug.LogWarning("WorldText shader not found: put WorldText.shader in Assets/Resources. Using Unity's default text (shows through walls).");
            mr.material = tm.font.material;
            return;
        }
        Material m = new Material(shader);
        m.mainTexture = tm.font.material.mainTexture;
        mr.material = m;
    }
}
