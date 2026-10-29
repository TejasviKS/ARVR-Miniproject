using UnityEngine;

// VERSION 9
// A sticky note that shows the switch code. Put it on a thin Cube placed under a desk.
// It reads the code from CodePanel, so if you change the code there, the note updates by itself.
// The code is printed on ONE big face of the cube (the side the blue arrow points to).
// If it ends up on the wrong side, tick "Flip Side".
public class CodeNote : MonoBehaviour
{
    public CodePanel panel;                              // found automatically if empty
    public Color inkColor = new Color(0.1f, 0.1f, 0.2f);
    [Tooltip("Height of the writing in metres")]
    public float textHeight = 0.022f;
    [Tooltip("Tick if the writing is on the wrong side of the note")]
    public bool flipSide = false;

    void Start()
    {
        if (panel == null) panel = FindAnyObjectByType<CodePanel>();
        string text = panel != null ? panel.code : "????";
        Write(text, flipSide ? -1 : +1);
    }

    void Write(string text, int side)
    {
        // The cube's two big faces are its local front and back (blue arrow and opposite)
        Vector3 faceDir = transform.forward * side;
        float halfThickness = transform.lossyScale.z * 0.5f;

        GameObject go = new GameObject("NoteText");
        go.transform.SetPositionAndRotation(
            transform.position + faceDir * (halfThickness + 0.001f),
            Quaternion.LookRotation(-faceDir, transform.up));
        go.transform.SetParent(transform, true);

        TextMesh tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.fontSize = 100;
        tm.characterSize = textHeight / 10f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = inkColor;
        WorldTextUtil.Apply(tm);                   // hidden behind objects, not drawn through them
    }
}