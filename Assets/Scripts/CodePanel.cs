using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

// VERSION 9
// The switchboard puzzle (keypad style: 1 2 3 4 5 6 / 7 8 9 0). Put this on ONE empty object anywhere in the scene (e.g. "SwitchPuzzle").
//
// When the game starts it:
//  1. finds the wall switches in the classroom model (objects named "light1.001" ... "light1.012"),
//  2. sorts them by where they really are on the wall (rows top to bottom, left to right),
//  3. numbers them like a keypad: top row = 1 2 3 4 5 6, second row = 7 8 9 0 (last two unused),
//     and prints the digit on each switch,
//  4. lets the player press them with E and checks the order against the code.
// Right code -> switches flash green, portal appears. Wrong code -> flash red, reset.
public class CodePanel : MonoBehaviour
{
    [Header("The code")]
    [Tooltip("Digits to press, in order. Switches are numbered like a keypad: 1 2 3 4 5 6 / 7 8 9 0. Write it as 3019 or 3-0-1-9.")]
    public string code = "3019";

    [Header("Which switches")]
    [Tooltip("Objects whose name starts with this are the wall switches")]
    public string switchNamePrefix = "light1.";
    [Tooltip("Which switches to use, counted in reading order (top row left->right = 1-6, second row = 7-12). They become digits 1, 2, 3 ... 9, 0 in this order.")]
    public int[] useSwitches = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
    public bool showNumberLabels = true;

    [Header("What happens")]
    [Tooltip("Hidden at the start, appears when the code is right")]
    public GameObject portal;
    public UnityEvent onSolved;
    [Tooltip("Later: spawn a monster from here")]
    public UnityEvent onWrongCode;

    [Header("Look & feel")]
    public Color pressedColor = new Color(1f, 0.75f, 0.2f);
    public Color correctColor = new Color(0.2f, 1f, 0.3f);
    public Color wrongColor = new Color(1f, 0.15f, 0.1f);
    public float pushDepth = 0.004f;      // how far a switch moves in when pressed (metres)

    readonly List<PanelSwitch> switches = new List<PanelSwitch>();
    readonly List<int> entered = new List<int>();
    int[] codeNumbers = new int[0];
    bool busy, solved;
    Vector3 outward;                       // from the wall into the room
    AudioSource sfx;
    AudioClip clickClip, goodClip, badClip;
    PlayerInteractor lastPlayer;
    int lastPressFrame = -1;

    public bool Ready => !busy && !solved;
    public Vector3 Outward => outward;

    void Start()
    {
        codeNumbers = ParseCode(code);
        if (portal != null) portal.SetActive(false);

        List<(Renderer r, int number)> order = ComputeOrder(out outward, out Vector3 centre);
        if (order.Count == 0)
        {
            Debug.LogError("[CodePanel] No switches found whose name starts with \"" + switchNamePrefix + "\".", this);
            return;
        }

        if (FindObjectsByType<CodePanel>().Length > 1)
            Debug.LogWarning("[CodePanel] There is more than one Code Panel in the scene! Keep only one.", this);

        foreach (var (r, number) in order)
        {
            if (r.GetComponent<Collider>() == null) r.gameObject.AddComponent<MeshCollider>();
            if (r.GetComponent<PanelSwitch>() != null) continue;   // already set up by another panel
            PanelSwitch s = r.gameObject.AddComponent<PanelSwitch>();
            s.Setup(this, number);
            if (showNumberLabels) s.MakeLabel(outward);
            switches.Add(s);
        }

        // 3D sound coming from the panel (spatial audio)
        GameObject audioGo = new GameObject("SwitchPanelAudio");
        audioGo.transform.SetParent(transform, false);
        audioGo.transform.position = centre;
        sfx = audioGo.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 1f;
        sfx.rolloffMode = AudioRolloffMode.Linear;
        sfx.minDistance = 0.5f;
        sfx.maxDistance = 15f;
        MakeSounds();

        Debug.Log("[CodePanel] v9: using " + switches.Count + " switches. Code field = \"" + code + "\" -> press: " + string.Join(" ", codeNumbers), this);
    }

    // ---------- Called by a switch when the player presses E on it ----------
    public void Press(PanelSwitch s, PlayerInteractor player)
    {
        if (!Ready) return;
        if (Time.frameCount == lastPressFrame) return;   // never count one E press twice
        lastPressFrame = Time.frameCount;
        lastPlayer = player;
        sfx.PlayOneShot(clickClip);
        StartCoroutine(PushAnimation(s));
        s.SetGlow(pressedColor);
        entered.Add(s.Number);

        // Show progress like "Code: 3 7 _ _"
        string shown = string.Join(" ", codeNumbers.Select((_, i) => i < entered.Count ? entered[i].ToString() : "_"));
        player.ShowMessage("Code: " + shown, 2f);

        if (entered.Count >= codeNumbers.Length) StartCoroutine(CheckCode());
    }

    IEnumerator CheckCode()
    {
        busy = true;
        yield return new WaitForSeconds(0.35f);
        bool correct = entered.SequenceEqual(codeNumbers);
        Debug.Log("[CodePanel] You pressed: " + string.Join(" ", entered) +
                  "   |   Expected: " + string.Join(" ", codeNumbers) +
                  "   (Code field = \"" + code + "\")   -> " + (correct ? "CORRECT" : "WRONG"), this);

        if (correct)
        {
            solved = true;
            sfx.PlayOneShot(goodClip);
            if (lastPlayer != null) lastPlayer.ShowMessage("Correct! A portal has opened...", 4f);
            yield return Flash(correctColor, 3);
            foreach (var s in switches) s.SetGlow(correctColor);   // stay green
            if (portal != null) portal.SetActive(true);
            onSolved.Invoke();
        }
        else
        {
            sfx.PlayOneShot(badClip);
            if (lastPlayer != null) lastPlayer.ShowMessage("Wrong code! (you entered " + string.Join(" ", entered) + ")", 3f);
            yield return Flash(wrongColor, 3);
            onWrongCode.Invoke();
            entered.Clear();
            foreach (var s in switches) s.ClearGlow();
            busy = false;
        }
    }

    IEnumerator Flash(Color c, int times)
    {
        for (int i = 0; i < times; i++)
        {
            foreach (var s in switches) s.SetGlow(c);
            yield return new WaitForSeconds(0.15f);
            foreach (var s in switches) s.ClearGlow();
            yield return new WaitForSeconds(0.15f);
        }
    }

    IEnumerator PushAnimation(PanelSwitch s)
    {
        Vector3 inward = -outward * pushDepth;
        s.transform.position += inward;
        yield return new WaitForSeconds(0.12f);
        s.transform.position -= inward;
    }

    // ---------- Finding and numbering the switches ----------
    List<(Renderer r, int number)> ComputeOrder(out Vector3 outDir, out Vector3 centre)
    {
        var result = new List<(Renderer, int)>();
        outDir = Vector3.forward;
        centre = transform.position;

        List<Renderer> found = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude)
            .Where(r => r.name.StartsWith(switchNamePrefix))
            .Cast<Renderer>()
            .ToList();
        if (found.Count == 0) return result;

        Bounds all = found[0].bounds;
        foreach (var r in found) all.Encapsulate(r.bounds);
        centre = all.center;

        // The panel is flat on a wall: its thinnest direction is the wall's normal
        Vector3 e = all.extents;
        Vector3 axis = (e.x <= e.y && e.x <= e.z) ? Vector3.right : (e.z <= e.y ? Vector3.forward : Vector3.up);
        // Point it into the room (towards the player)
        Vector3 viewer = centre - axis;
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) viewer = pc.transform.position;
        else if (Camera.main != null) viewer = Camera.main.transform.position;
        if (Vector3.Dot(viewer - centre, axis) < 0f) axis = -axis;
        outDir = axis;

        // Left/right as seen by someone standing in front of the panel
        Vector3 lookDir = -axis;
        Vector3 right = Vector3.Cross(Vector3.up, lookDir).normalized;

        // Rows: top to bottom. Inside a row: left to right.
        var sorted = found.OrderByDescending(r => r.bounds.center.y).ToList();
        var rows = new List<List<Renderer>>();
        foreach (var r in sorted)
        {
            if (rows.Count == 0 || rows[rows.Count - 1][0].bounds.center.y - r.bounds.center.y > 0.03f)
                rows.Add(new List<Renderer>());
            rows[rows.Count - 1].Add(r);
        }
        var reading = rows.SelectMany(row => row.OrderBy(r => Vector3.Dot(r.bounds.center, right))).ToList();

        for (int i = 0; i < useSwitches.Length; i++)
        {
            int idx = useSwitches[i] - 1;
            if (idx >= 0 && idx < reading.Count) result.Add((reading[idx], (i + 1) % 10));   // 10th switch = 0
        }
        return result;
    }

    static int[] ParseCode(string s)
    {
        // Every digit is one switch press: "3019", "3-0-1-9" and "3 0 1 9" all mean 3, 0, 1, 9
        var nums = new List<int>();
        foreach (char c in s)
            if (char.IsDigit(c)) nums.Add(c - '0');
        return nums.ToArray();
    }

    // ---------- Sounds made in code (no audio files needed) ----------
    void MakeSounds()
    {
        clickClip = MakeClip("click", 0.05f, t => Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-t * 90f) * 0.6f);
        goodClip = MakeClip("correct", 0.45f, t =>
            Mathf.Sin(2f * Mathf.PI * (t < 0.15f ? 660f : t < 0.3f ? 880f : 1320f) * t) * 0.35f * (1f - t / 0.45f));
        badClip = MakeClip("wrong", 0.5f, t =>
            Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 130f * t)) * 0.2f * (1f - t / 0.5f));
    }

    static AudioClip MakeClip(string name, float seconds, System.Func<float, float> wave)
    {
        const int rate = 44100;
        int n = Mathf.CeilToInt(rate * seconds);
        float[] data = new float[n];
        for (int i = 0; i < n; i++) data[i] = wave((float)i / rate);
        AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

#if UNITY_EDITOR
    // Select this object in the editor to see the switch numbers on the wall (before pressing Play)
    void OnDrawGizmosSelected()
    {
        var order = ComputeOrder(out Vector3 o, out _);
        var style = new GUIStyle { fontSize = 16, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.yellow;
        foreach (var (r, number) in order)
            UnityEditor.Handles.Label(r.bounds.center + o * 0.03f, number.ToString(), style);
    }
#endif
}