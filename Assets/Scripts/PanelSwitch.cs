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

    public override bool CanInteract => panel != null && panel.Ready;
    public override void Interact(PlayerInteractor player) => panel.Press(this, player);

    // Light up the switch (and its number) in a colour
    public void SetGlow(Color c)
    {
        foreach (var m in mats) m.SetColor("_EmissionColor", c * 1.5f);
        if (label != null) label.color = c;
    }

    public void ClearGlow()
    {
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
        label.characterSize = 0.0018f;          // about 2 cm tall
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = labelColor;
        WorldTextUtil.Apply(label);                // hidden behind objects, not drawn through them
    }
}
