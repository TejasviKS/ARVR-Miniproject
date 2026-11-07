using UnityEngine;

// VERSION 9
// One wall switch. You DON'T add this yourself: CodePanel adds it to each switch when the game starts.
public class PanelSwitch : Interactable
{
    public int Number { get; private set; }
    CodePanel panel;
    Material[] mats;
    TextMesh label;
    Color labelColor = Color.white;
    bool flat;                 // flat "realm" look: the switch is a plain glowing plate (see UseFlatLook)
    Color flatIdle, flatInk;

    public void Setup(CodePanel p, int number)
    {
        panel = p;
        Number = number;
        promptText = "press switch " + number;

        // Own copy of the materials so this switch can glow on its own
        Renderer r = GetComponent<Renderer>();
        mats = r.materials;
        foreach (var m in mats)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
        }
    }

    // The flat realm look (RealmStyle) paints every normal material one colour, which would hide the glow and the number.
    // So in a realm the switch becomes a plain unlit plate: a light colour with a dark number, lit green/red/amber when used.
    // Call this right after Setup and before MakeLabel.
    public void UseFlatLook(Color idle, Color ink)
    {
        Shader shader = Shader.Find("Custom/ScreenGlow");
        if (shader == null) return;
        flat = true; flatIdle = idle; flatInk = ink; labelColor = ink;
        Renderer r = GetComponent<Renderer>();
        var fresh = new Material[r.sharedMaterials.Length];
        for (int i = 0; i < fresh.Length; i++) fresh[i] = new Material(shader) { color = idle };
        r.materials = fresh;
        mats = r.materials;
    }

    public override string GetPrompt() => "Press E for Switch " + Number;

    public override bool AcceptsClick => true;          // E or left click both press it (the prompt only mentions E: shorter on screen)
    public override bool CanInteract => panel != null && panel.Ready;
    public override void Interact(PlayerInteractor player) => panel.Press(this, player);

    // Light up the switch (and its number) in a colour
    public void SetGlow(Color c)
    {
        if (flat) { foreach (var m in mats) m.color = c; if (label != null) label.color = new Color(0.08f, 0.05f, 0.03f); return; }   // dark number on the bright glow
        foreach (var m in mats) m.SetColor("_EmissionColor", c * 1.5f);
        if (label != null) label.color = c;
    }

    public void ClearGlow()
    {
        if (flat) { foreach (var m in mats) m.color = flatIdle; if (label != null) label.color = labelColor; return; }
        foreach (var m in mats) m.SetColor("_EmissionColor", Color.black);
        if (label != null) label.color = labelColor;
    }

    // Small white number printed on the front of the switch
    public void MakeLabel(Vector3 outward)
    {
        Bounds b = GetComponent<Renderer>().bounds;
        float halfThickness = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(outward.x), Mathf.Abs(outward.y), Mathf.Abs(outward.z))));

        GameObject go = new GameObject("Label_" + Number);
        go.transform.SetPositionAndRotation(
            b.center + outward * (halfThickness + 0.002f),
            Quaternion.LookRotation(-outward, Vector3.up));     // readable from the room
        go.transform.SetParent(transform, true);

        label = go.AddComponent<TextMesh>();
        label.text = Number.ToString();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 100;
        label.characterSize = flat ? 0.0024f : 0.0018f;          // about 2 cm tall (a bit bigger and bold in the flat look)
        if (flat) label.fontStyle = FontStyle.Bold;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = labelColor;
        WorldTextUtil.Apply(label);                // hidden behind objects, not drawn through them
    }
}
