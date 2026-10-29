using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// VERSION 22
// "Find the beeping computer" puzzle for the CAD lab.
// Put this on ONE empty object anywhere. Type the beeping monitor's name in "Beeping Computer Name"
// (or leave it empty and place this object next to the computer you want).
//
// When the game starts it:
//  1. finds every monitor in the lab (recognised by shape + name/material),
//  2. gives each screen a glow (dim blue, like an idle PC),
//  3. makes ONE computer beep with 3D sound. Its screen looks almost like the others:
//     it only glitches/flickers now and then, and shows a small error message you can read up close,
//  4. when the player presses E on it: beeping stops, power-down sound, screen goes black,
//  5. after a short pause the NEXT computer from the list starts beeping (Computers To Switch Off),
//  6. when all of them are off, "On Solved" runs and the Portal (if set) appears.
public class BeepingComputerPuzzle : MonoBehaviour
{
    [Header("Which computer beeps")]
    [Tooltip("How many computers the player has to switch off, one after another, before the door opens")]
    [Min(1)] public int computersToSwitchOff = 2;
    [Tooltip("Seconds of silence before the next computer starts beeping")]
    public float delayBeforeNext = 2f;
    [Tooltip("ON = pick the computers at random from the Random Options below (never the same one twice).")]
    public bool pickRandomly = true;
    [Tooltip("The computers that can be picked when Pick Randomly is ON")]
    public string[] randomOptions =
    {
        "defaultMaterial.044",   // Lab 1, row 1
        "defaultMaterial.005",   // Lab 1, row 1
        "defaultMaterial.385",   // Lab 1, row 2
        "defaultMaterial.075",   // Lab 1, row 3
        "defaultMaterial.325",   // Lab 1, row 4
    };
    [Tooltip("Used when Pick Randomly is OFF: name of the monitor that should beep. Empty = the computer closest to this object.")]
    public string beepingComputerName = "defaultMaterial.385";
    [Tooltip("Only used when Beeping Computer Name is empty. 0 = the computer closest to this object. More than 0 = a random computer within this many metres of this object (different every play).")]
    public float randomWithinRadius = 0f;
    [Tooltip("Objects whose name starts with this are the monitors")]
    public string monitorNamePrefix = "Computer_Low";
    [Tooltip("Objects whose name starts with this are the keyboards (used to know which way each screen faces)")]
    public string keyboardNamePrefix = "Key_Board_Low";

    [Header("Screens")]
    public bool lightUpOtherScreens = true;
    [Tooltip("OFF = glow only on the front of each screen (normal). ON = glow on front and back.")]
    public bool bothSides = false;
    public Color idleScreenColor = new Color(0.05f, 0.15f, 0.4f);
    [Tooltip("OFF (default) = subtle: same colour as the other screens, flickers now and then, small error text.\nON = obvious: flashing red screen with big text.")]
    public bool obviousAlarm = false;
    public Color alarmScreenColor = new Color(0.9f, 0.08f, 0.05f);
    [Tooltip("Subtle mode: seconds between glitches (random between these two)")]
    public Vector2 glitchEvery = new Vector2(2.5f, 6f);
    public Color offScreenColor = new Color(0.01f, 0.01f, 0.01f);
    public string alarmText = "! WARNING !\nSWITCH ME OFF";
    [Tooltip("Subtle mode: the small text on the beeping screen (only readable up close)")]
    public string subtleText = "ERR 0x7F\nsystem unstable";

    [Header("Sound")]
    [Range(0f, 1f)] public float beepVolume = 0.8f;
    [Tooltip("How far away (metres) the beeping can still be heard")]
    public float hearingDistance = 25f;

    [Header("What happens when it's switched off")]
    [Tooltip("Hidden at the start, appears when the computer is switched off (e.g. the portal to the next room)")]
    public GameObject portal;
    public string solvedMessage = "All computers shut down. Something opened...";
    [Tooltip("Shown after switching one off when more are left. {0} = how many are off, {1} = total")]
    public string progressMessage = "Computer {0}/{1} shut down... wait, another one is beeping!";
    public UnityEvent onSolved;

    public bool IsBeeping { get; private set; }

    // One entry per computer the player must switch off, in order
    class Alarm { public Monitor m; public Material mat; public TwoSidedText label; }
    readonly List<Alarm> alarms = new List<Alarm>();
    int current = -1;

    Material alarmMat;            // the CURRENT beeping computer's screen
    TwoSidedText alarmLabel;      // the CURRENT beeping computer's text
    float nextGlitch, glitchUntil;
    AudioSource beepSource;
    AudioClip beepClip, powerDownClip;
    GameObject hitbox;
    BoxCollider hitboxCollider;

    struct Monitor { public Transform t; public Bounds b; public Vector3 facing; }

    // Which way each CAD lab screen faces, measured from the model (towards its keyboard and chair).
    // Every screen faces along Z: these face +Z, all the others in the lab face -Z.
    static readonly HashSet<string> ScreensFacingPlusZ = new HashSet<string>
    {
        "defaultMaterial.204", "defaultMaterial.205", "defaultMaterial.214", "defaultMaterial.215",
        "defaultMaterial.224", "defaultMaterial.225", "defaultMaterial.234", "defaultMaterial.235",
        "defaultMaterial.244", "defaultMaterial.245", "defaultMaterial.254", "defaultMaterial.255",
        "defaultMaterial.264", "defaultMaterial.265", "defaultMaterial.274", "defaultMaterial.275",
        "defaultMaterial.284", "defaultMaterial.285", "defaultMaterial.294", "defaultMaterial.295",
        "defaultMaterial.304", "defaultMaterial.305", "defaultMaterial.314", "defaultMaterial.315",
        "defaultMaterial.324", "defaultMaterial.325", "defaultMaterial.334", "defaultMaterial.335",
        "defaultMaterial.344", "defaultMaterial.345", "defaultMaterial.354", "defaultMaterial.355",
        "defaultMaterial.364", "defaultMaterial.365", "defaultMaterial.374", "defaultMaterial.375",
        "defaultMaterial.384", "defaultMaterial.385", "defaultMaterial.394", "defaultMaterial.395",
    };
    static readonly HashSet<string> ScreensFacingMinusZ = new HashSet<string>
    {
        "defaultMaterial.005", "defaultMaterial.009", "defaultMaterial.014", "defaultMaterial.015",
        "defaultMaterial.024", "defaultMaterial.025", "defaultMaterial.034", "defaultMaterial.035",
        "defaultMaterial.044", "defaultMaterial.045", "defaultMaterial.054", "defaultMaterial.055",
        "defaultMaterial.064", "defaultMaterial.065", "defaultMaterial.074", "defaultMaterial.075",
        "defaultMaterial.084", "defaultMaterial.085", "defaultMaterial.094", "defaultMaterial.095",
        "defaultMaterial.104", "defaultMaterial.105", "defaultMaterial.114", "defaultMaterial.115",
        "defaultMaterial.124", "defaultMaterial.125", "defaultMaterial.134", "defaultMaterial.135",
        "defaultMaterial.144", "defaultMaterial.145", "defaultMaterial.154", "defaultMaterial.155",
        "defaultMaterial.164", "defaultMaterial.165", "defaultMaterial.174", "defaultMaterial.175",
        "defaultMaterial.184", "defaultMaterial.185",
    };

    static BeepingComputerPuzzle running;   // only ONE of these may run, or you get double beeps/text

    void Awake()
    {
        if (running != null && running != this)
        {
            Debug.LogWarning("[BeepingComputer] There is more than one Beeping Computer Puzzle in the scene (\"" + running.name +
                             "\" and \"" + name + "\"). Only the first one runs - delete the extra one.", this);
            enabled = false;
            return;
        }
        running = this;
    }

    void OnDestroy() { if (running == this) running = null; }

    void Start()
    {
        if (portal != null) portal.SetActive(false);

        List<Monitor> monitors = FindMonitors();
        if (monitors.Count == 0)
        {
            Debug.LogError("[BeepingComputer] No monitors found. Looking for ~40x25 cm objects named \"" + monitorNamePrefix + "...\" or using a 'computer/monitor' material.", this);
            return;
        }

        // Pick the computers that will beep, in order
        List<Monitor> sequence = PickSequence(monitors);

        // Screens
        Shader glow = Shader.Find("Custom/ScreenGlow");
        if (glow == null) Debug.LogWarning("[BeepingComputer] ScreenGlow shader not found: put ScreenGlow.shader in Assets/Resources.");
        Material idleMat = null;
        if (glow != null && lightUpOtherScreens)
        {
            idleMat = new Material(glow);
            idleMat.color = idleScreenColor;
        }
        foreach (Monitor m in monitors)
        {
            if (sequence.Exists(x => x.t == m.t))
            {
                // A computer that will beep later: its own screen + hidden text, looks idle for now
                var al = new Alarm { m = m };
                if (glow != null)
                {
                    al.mat = new Material(glow);
                    al.mat.color = idleScreenColor;
                    MakeScreen(m, al.mat);
                    al.label = MakeScreenText(m, obviousAlarm ? alarmText : subtleText);
                    if (!obviousAlarm)
                    {
                        al.label.characterSize = 0.0012f;               // ~1 cm letters: readable only up close
                        al.label.fontStyle = FontStyle.Normal;
                        al.label.color = new Color(0.55f, 0.65f, 0.8f);  // same tone as the idle screens
                    }
                    al.label.gameObject.SetActive(false);
                }
                alarms.Add(al);
            }
            else if (idleMat != null)
            {
                MakeScreen(m, idleMat);
            }
        }
        // keep the alarms in the picked order
        alarms.Sort((x, y) => sequence.FindIndex(q => q.t == x.m.t).CompareTo(sequence.FindIndex(q => q.t == y.m.t)));

        // The "press E" box (moves to whichever computer is beeping)
        hitbox = new GameObject("BeepingComputer_Hitbox");
        hitbox.transform.SetParent(transform, true);
        hitboxCollider = hitbox.AddComponent<BoxCollider>();
        hitboxCollider.isTrigger = true;
        hitbox.AddComponent<ComputerSwitch>().puzzle = this;

        // 3D beeping sound (moves to whichever computer is beeping)
        MakeSounds();
        GameObject snd = new GameObject("BeepSound");
        snd.transform.SetParent(transform, true);
        beepSource = snd.AddComponent<AudioSource>();
        beepSource.clip = beepClip;
        beepSource.volume = beepVolume;
        beepSource.spatialBlend = 1f;                       // fully 3D: direction + distance
        beepSource.rolloffMode = AudioRolloffMode.Logarithmic;
        beepSource.minDistance = 1f;
        beepSource.maxDistance = hearingDistance;
        beepSource.dopplerLevel = 0f;

        int matched = 0;
        var missing = new List<string>();
        if (randomOptions != null)
            foreach (string opt in randomOptions)
            {
                if (monitors.Exists(mm => mm.t.name == opt)) matched++; else missing.Add(opt);
            }
        Debug.Log("[BeepingComputer] v22: " + monitors.Count + " monitors found. Pick Randomly = " + pickRandomly +
                  ", random options found: " + matched + "/" + (randomOptions == null ? 0 : randomOptions.Length) +
                  (missing.Count > 0 ? " (NOT found: " + string.Join(", ", missing) + ")" : "") +
                  ". Order: " + string.Join(" -> ", alarms.ConvertAll(x => x.m.t.name)), this);

        StartBeeping(0);
    }

    // Make computer number i (in the order) start beeping
    void StartBeeping(int i)
    {
        current = i;
        Alarm al = alarms[i];
        alarmMat = al.mat;
        alarmLabel = al.label;
        if (alarmLabel != null) alarmLabel.gameObject.SetActive(true);
        if (alarmMat != null && obviousAlarm) alarmMat.color = alarmScreenColor;

        hitbox.transform.position = al.m.b.center;
        hitboxCollider.size = al.m.b.size + Vector3.one * 0.15f;
        beepSource.transform.position = al.m.b.center;
        beepSource.loop = true;
        beepSource.Play();
        nextGlitch = Time.time + Random.Range(glitchEvery.x, glitchEvery.y);
        IsBeeping = true;
    }

    void Update()
    {
        if (!IsBeeping || alarmMat == null || beepSource == null) return;

        if (obviousAlarm)
        {
            // Flash red in time with the beeps
            float t = beepSource.time % 1f;
            bool on = t < 0.12f || (t > 0.25f && t < 0.37f);
            alarmMat.color = on ? alarmScreenColor : alarmScreenColor * 0.35f;
            if (alarmLabel != null) alarmLabel.color = on ? Color.white : new Color(1f, 1f, 1f, 0.5f);
            return;
        }

        // Subtle: looks like every other screen, but every few seconds it glitches for a moment
        if (Time.time >= nextGlitch)
        {
            glitchUntil = Time.time + Random.Range(0.12f, 0.35f);
            nextGlitch = Time.time + Random.Range(glitchEvery.x, glitchEvery.y);
        }
        if (Time.time < glitchUntil)
        {
            // quick random flicker: dims, brightens, slight colour shift
            float r = Random.value;
            alarmMat.color = r < 0.4f ? idleScreenColor * 0.2f
                           : r < 0.7f ? idleScreenColor * 1.8f
                           : new Color(idleScreenColor.r + 0.12f, idleScreenColor.g, idleScreenColor.b * 0.8f);
            if (alarmLabel != null) alarmLabel.gameObject.SetActive(r > 0.3f);
        }
        else
        {
            alarmMat.color = idleScreenColor;
            if (alarmLabel != null) alarmLabel.gameObject.SetActive(true);
        }
    }

    // ---------- Called by the hitbox when the player presses E ----------
    public void SwitchOff(PlayerInteractor player)
    {
        if (!IsBeeping) return;
        IsBeeping = false;
        StartCoroutine(PowerDown(player));
    }

    IEnumerator PowerDown(PlayerInteractor player)
    {
        beepSource.Stop();
        beepSource.loop = false;
        beepSource.PlayOneShot(powerDownClip);
        if (alarmLabel != null)
        {
            alarmLabel.gameObject.SetActive(true);
            alarmLabel.text = "SHUTTING DOWN...";
            alarmLabel.characterSize = 0.0022f;     // clear, readable size
            alarmLabel.color = Color.white;
        }
        if (alarmMat != null) alarmMat.color = new Color(0.1f, 0.1f, 0.3f);

        yield return new WaitForSeconds(0.8f);

        if (alarmMat != null) alarmMat.color = offScreenColor;
        if (alarmLabel != null) alarmLabel.gameObject.SetActive(false);

        int done = current + 1;
        if (done < alarms.Count)
        {
            // More to go: short silence, then the next one starts beeping somewhere else
            if (player != null) player.ShowMessage(string.Format(progressMessage, done, alarms.Count), 3f);
            yield return new WaitForSeconds(delayBeforeNext);
            StartBeeping(done);
            yield break;
        }

        // All switched off: open the way
        if (player != null) player.ShowMessage(solvedMessage, 4f);
        if (portal != null) portal.SetActive(true);
        onSolved.Invoke();
    }

    // ---------- Finding the monitors ----------
    List<Monitor> FindMonitors()
    {
        // Monitors are recognised by SHAPE (about 40 x 25 cm, a few cm thick) plus either their
        // group name ("Computer_Low...") or their material ("computer_moniter"). This works even if
        // the model was exported without its group objects.
        var result = new List<Monitor>();
        var keyboards = new List<Bounds>();

        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (!(r is MeshRenderer) || !r.enabled) continue;
            Vector3 sz = r.bounds.size;
            float wide = Mathf.Max(sz.x, sz.z), narrow = Mathf.Min(sz.x, sz.z);
            bool named = HasAncestorStarting(r.transform, monitorNamePrefix) || r.name == beepingComputerName
                         || (randomOptions != null && System.Array.IndexOf(randomOptions, r.name) >= 0);
            bool computerMat = MaterialLooksLikeComputer(r);

            bool monitorShape = sz.y > 0.15f && sz.y < 0.4f && wide > 0.28f && wide < 0.65f && narrow < 0.12f;
            bool keyboardShape = sz.y < 0.07f && wide > 0.28f && wide < 0.6f && narrow > 0.08f && narrow < 0.3f;

            if (monitorShape && (named || computerMat))
                result.Add(new Monitor { t = r.transform, b = r.bounds });
            else if (keyboardShape && (computerMat || HasAncestorStarting(r.transform, keyboardNamePrefix)))
                keyboards.Add(r.bounds);
        }

        // Which way each screen faces: towards its keyboard (or the chair side), snapped to straight out
        for (int i = 0; i < result.Count; i++)
        {
            Monitor m = result[i];
            Vector3 facing = Vector3.forward;
            float best = float.MaxValue;
            foreach (Bounds kb in keyboards)
            {
                float d = (kb.center - m.b.center).sqrMagnitude;
                if (d < best) { best = d; facing = kb.center - m.b.center; }
            }
            Vector3 e = m.b.extents;
            m.facing = e.x < e.z ? new Vector3(Mathf.Sign(facing.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(facing.z));

            // Known CAD lab screens: use the measured direction (turned with the model if it was rotated)
            Quaternion modelRot = m.t.root.rotation;
            if (ScreensFacingPlusZ.Contains(m.t.name)) m.facing = modelRot * Vector3.forward;
            else if (ScreensFacingMinusZ.Contains(m.t.name)) m.facing = modelRot * Vector3.back;
            result[i] = m;
        }

        if (Application.isPlaying)
            Debug.Log("[BeepingComputer] Found " + result.Count + " monitors and " + keyboards.Count + " keyboards.", this);
        return result;
    }

    static bool HasAncestorStarting(Transform t, string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return false;
        for (Transform p = t; p != null; p = p.parent)
            if (p.name.StartsWith(prefix)) return true;
        return false;
    }

    static bool MaterialLooksLikeComputer(Renderer r)
    {
        foreach (Material m in r.sharedMaterials)
        {
            if (m == null) continue;
            string n = m.name.ToLower();
            if (n.Contains("moniter") || n.Contains("monitor") || n.Contains("screen") || n.Contains("computer")) return true;
        }
        return false;
    }

    static bool TryBounds(Transform t, out Bounds b)
    {
        b = new Bounds();
        bool any = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any;
    }

    // The computers that will beep, in order (never the same one twice)
    List<Monitor> PickSequence(List<Monitor> monitors)
    {
        var seq = new List<Monitor>();
        int count = Mathf.Max(1, computersToSwitchOff);

        if (pickRandomly && randomOptions != null && randomOptions.Length > 0)
        {
            var options = monitors.FindAll(m => System.Array.IndexOf(randomOptions, m.t.name) >= 0);
            // shuffle
            for (int i = options.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (options[i], options[j]) = (options[j], options[i]);
            }
            for (int i = 0; i < options.Count && seq.Count < count; i++) seq.Add(options[i]);
            if (seq.Count < count && Application.isPlaying)
                Debug.LogWarning("[BeepingComputer] Only " + seq.Count + " usable names in Random Options, but Computers To Switch Off is " + count + ".", this);
        }

        if (seq.Count == 0) seq.Add(PickMonitor(monitors));

        // Not enough from the list (e.g. random off)? Add other random monitors
        var rest = monitors.FindAll(m => !seq.Exists(x => x.t == m.t));
        while (seq.Count < count && rest.Count > 0)
        {
            int j = Random.Range(0, rest.Count);
            seq.Add(rest[j]);
            rest.RemoveAt(j);
        }
        return seq;
    }

    Monitor PickMonitor(List<Monitor> monitors)
    {
        // Random from the list (only names that really exist count)
        if (pickRandomly && randomOptions != null && randomOptions.Length > 0)
        {
            var options = monitors.FindAll(m => System.Array.IndexOf(randomOptions, m.t.name) >= 0);
            if (options.Count > 0)
            {
                // In the editor preview don't flicker between choices: just show the first
                return Application.isPlaying ? options[Random.Range(0, options.Count)] : options[0];
            }
            if (Application.isPlaying) Debug.LogWarning("[BeepingComputer] None of the names in Random Options were found as monitors.", this);
        }

        if (!pickRandomly && !string.IsNullOrEmpty(beepingComputerName))
        {
            foreach (Monitor m in monitors)
                if (m.t.name == beepingComputerName) return m;
            // Not a monitor itself (maybe a keyboard or mouse)? Use the monitor nearest to it.
            GameObject named = GameObject.Find(beepingComputerName);
            if (named != null)
            {
                Vector3 p = named.TryGetComponent(out Renderer nr) ? nr.bounds.center : named.transform.position;
                Monitor near = monitors[0];
                float nd = float.MaxValue;
                foreach (Monitor m in monitors)
                {
                    float d = Vector3.Distance(m.b.center, p);
                    if (d < nd) { nd = d; near = m; }
                }
                return near;
            }
            if (Application.isPlaying) Debug.LogWarning("[BeepingComputer] No object called \"" + beepingComputerName + "\" found, using the computer closest to this object instead.", this);
        }
        if (randomWithinRadius > 0f)
        {
            var near = monitors.FindAll(m => Vector3.Distance(m.b.center, transform.position) <= randomWithinRadius);
            if (near.Count > 0) return near[Random.Range(0, near.Count)];
        }
        Monitor best = monitors[0];
        float bestD = float.MaxValue;
        foreach (Monitor m in monitors)
        {
            float d = Vector3.Distance(m.b.center, transform.position);
            if (d < bestD) { bestD = d; best = m; }
        }
        return best;
    }

    // ---------- Building the glowing screen ----------
    // The glow + text go on the FRONT of the monitor (the side facing its keyboard/chair).
    void MakeScreen(Monitor m, Material mat)
    {
        Vector3 e = m.b.extents;
        bool facesX = Mathf.Abs(m.facing.x) > 0.5f;
        float width = (facesX ? e.z : e.x) * 2f * 0.88f;
        float height = e.y * 2f * 0.84f;          // leave a thin frame around the screen
        float thin = facesX ? e.x : e.z;

        foreach (float side in bothSides ? new[] { 1f, -1f } : new[] { 1f })
        {
            Vector3 n = side > 0 ? m.facing : -m.facing;
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "Screen_" + m.t.name;
            q.transform.SetPositionAndRotation(m.b.center + n * (thin + 0.004f), Quaternion.LookRotation(-n, Vector3.up));
            q.transform.localScale = new Vector3(width, height, 1f);
            q.transform.SetParent(transform, true);      // parent AFTER sizing so this object's scale can't shrink it
            MeshRenderer mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    // Text shown on both faces of the beeping monitor
    class TwoSidedText
    {
        public GameObject gameObject;
        public TextMesh a, b;
        public Color color { set { a.color = value; b.color = value; } }
        public string text { set { a.text = value; b.text = value; } }
        public float characterSize { set { a.characterSize = value; b.characterSize = value; } }
        public FontStyle fontStyle { set { a.fontStyle = value; b.fontStyle = value; } }
    }

    TwoSidedText MakeScreenText(Monitor m, string text)
    {
        Vector3 e = m.b.extents;
        bool facesX = Mathf.Abs(m.facing.x) > 0.5f;
        float thin = facesX ? e.x : e.z;

        var result = new TwoSidedText { gameObject = new GameObject("ScreenText_" + m.t.name) };
        result.gameObject.transform.SetParent(transform, false);
        result.gameObject.transform.position = m.b.center;

        TextMesh Make(Vector3 n)
        {
            GameObject go = new GameObject("Text");
            go.transform.SetPositionAndRotation(m.b.center + n * (thin + 0.008f), Quaternion.LookRotation(-n, Vector3.up));
            go.transform.SetParent(result.gameObject.transform, true);
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.fontSize = 100;
            tm.characterSize = 0.0028f;               // about 3 cm letters
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = Color.white;
            WorldTextUtil.Apply(tm);
            return tm;
        }
        result.a = Make(m.facing);
        result.b = Make(bothSides ? -m.facing : m.facing);   // single-sided: both copies sit on the front
        return result;
    }

    // ---------- Sounds made in code (no audio files needed) ----------
    void MakeSounds()
    {
        // 1-second loop: two sharp beeps, then silence
        beepClip = MakeClip("beep", 1f, t =>
        {
            float local = t < 0.25f ? t : t - 0.25f;
            bool on = t < 0.12f || (t > 0.25f && t < 0.37f);
            if (!on) return 0f;
            float env = Mathf.Clamp01(local * 200f) * Mathf.Clamp01((0.12f - local) * 200f);
            return Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 1250f * t)) * 0.35f * env;
        });
        // Falling "power down" whine
        powerDownClip = MakeClip("powerdown", 0.9f, t =>
        {
            float f = Mathf.Lerp(700f, 60f, t / 0.9f);
            return Mathf.Sin(2f * Mathf.PI * f * t) * 0.45f * (1f - t / 0.9f);
        });
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
    // Select this object to see a yellow line to the computer that will beep (closest mode)
    void OnDrawGizmosSelected()
    {
        if (Application.isPlaying) return;
        var ms = FindMonitors();
        if (ms.Count == 0) return;
        Gizmos.color = Color.yellow;
        if (!pickRandomly && string.IsNullOrEmpty(beepingComputerName) && randomWithinRadius > 0f) { Gizmos.DrawWireSphere(transform.position, randomWithinRadius); return; }
        Monitor m = PickMonitor(ms);
        Gizmos.DrawLine(transform.position, m.b.center);
        Gizmos.DrawWireCube(m.b.center, m.b.size + Vector3.one * 0.05f);
    }
#endif
}