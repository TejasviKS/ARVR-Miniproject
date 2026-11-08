using UnityEngine;
using UnityEngine.InputSystem;

// VERSION 2
// Runs the very first level (the tutorial cabin). Put it on ONE empty object and fill in the three slots.
// It teaches one thing at a time in a box at the bottom right of the screen (drawn with the built-in UI, no Canvas):
//
//   LOOK    mouse icon with 4 arrows, each lights up as you look that way
//   WALK    W A S D keys, they light up while you press them
//   SPRINT  the SHIFT key
//   RESET   the R key: "if you ever get stuck" (R puts you back at the start, see PlayerSpawn; nothing is lost)
//   GO TO THE CUPBOARD   a glowing ring on the floor + an on-screen marker pointing to it
//   INTERACT   the E key: look at the cupboard and press E -> it opens, a note appears
//   ... then: find the light switch -> lights out -> the portal opens -> step through it.
// Each finished step gets a green tick for a moment before the next one starts.
// The note can be read again any time: press E on the open cupboard.
public class TutorialDirector : MonoBehaviour
{
    [Header("The level")]
    public OpenableCupboard cupboard;
    public LightSwitch lightSwitch;
    [Tooltip("Hidden at the start; appears when the lights go out")]
    public GameObject portal;

    [Header("Words")]
    [TextArea(1, 3)] public string noteText = "Switch off the lights\nbefore leaving.";
    public string portalMessage = "A portal has opened!";

    [Header("The teaching box")]
    [Tooltip("Size of the box on screen (1 = the original size)")]
    [Range(0.5f, 1.2f)] public float boxScale = 0.75f;

    [Header("How much the player has to do")]
    [Tooltip("Degrees to look in EACH of the four directions")]
    public float lookDegrees = 14f;
    [Tooltip("Walking this far (metres) finishes the WASD step")]
    public float walkMetres = 1.0f;
    [Tooltip("Sprinting this far (metres) finishes the SHIFT step. From the start point a chair blocks the way only ~0.6 m straight ahead, so keep it below that: nobody should have to turn round.")]
    public float sprintMetres = 0.45f;
    [Tooltip("The glowing spot is this far (metres) in front of the cupboard")]
    public float spotDistance = 1.0f;
    [Tooltip("Standing this close (metres) to the centre of the spot counts as arriving")]
    public float spotRadius = 0.75f;
    [Tooltip("The note closes by itself after this many seconds the FIRST time (you can close it sooner with E)")]
    public float noteShowTime = 7f;

    enum Step { Look, Walk, Sprint, Reset, GoToCupboard, UseCupboard, FindSwitch, UsePortal }
    Step step = Step.Look;
    float stepStart;
    bool done;                        // this step is finished: green tick for a moment, then the next one
    float doneAt;

    PlayerController player;
    PlayerInteractor interactor;
    Camera cam;
    Vector3 lastPos;
    float lastYaw, lastPitch;
    readonly float[] look = new float[4];          // degrees looked: up, down, left, right
    float stepWalked, stepSprinted;

    GameObject ring;
    Material ringMat;

    float boxAlpha;
    bool noteVisible, noteAuto;
    float noteOpenedAt, noteClosedAt = -10f, noteAlpha;

    Texture2D triangle;
    GUIStyle titleStyle, bodyStyle, keyStyle, smallKeyStyle, objectiveStyle, noteStyle, noteTitleStyle, distStyle;

    static readonly Color Accent = new Color(1f, 0.78f, 0.3f);
    static readonly Color Dim = new Color(0.45f, 0.48f, 0.55f);
    static readonly Color Good = new Color(0.4f, 0.9f, 0.5f);

    // ================================================================ setup
    void Start()
    {
        Debug.Log("[Scripts] TutorialDirector v2 running", this);
        player = FindAnyObjectByType<PlayerController>();
        interactor = FindAnyObjectByType<PlayerInteractor>();
        if (player != null)
        {
            cam = player.GetComponentInChildren<Camera>(true);
            lastPos = player.transform.position;
            lastYaw = player.transform.eulerAngles.y;
            lastPitch = Pitch();
        }

        if (portal != null) portal.SetActive(false);
        else Debug.LogWarning("TutorialDirector: Portal is not set, so nothing opens when the lights go out.", this);
        if (cupboard != null)
        {
            cupboard.locked = true;
            cupboard.onOpened.AddListener(OnCupboardOpened);
            cupboard.onReadAgain.AddListener(() => ShowNote(false));
        }
        else Debug.LogWarning("TutorialDirector: Cupboard is not set.", this);
        if (lightSwitch != null) lightSwitch.onSwitchedOff.AddListener(OnLightsOff);
        else Debug.LogWarning("TutorialDirector: Light Switch is not set.", this);
        // R teleports the player: don't count that jump as walking
        if (PlayerSpawn.Current != null) PlayerSpawn.Current.onReset.AddListener(() => { if (player != null) lastPos = player.transform.position; });

        triangle = MakeTriangle();
        stepStart = Time.time;
        intro = FindAnyObjectByType<CabinIntroDialogue>();            // (the player's opening lines: the tutorial starts after them)
    }

    CabinIntroDialogue intro;

    void OnDestroy() { DestroyRing(); }

    float Pitch()
    {
        // looking down is positive
        return cam != null ? Mathf.DeltaAngle(0f, cam.transform.localEulerAngles.x) : 0f;
    }

    // ================================================================ step logic
    void Complete()
    {
        if (done) return;
        done = true;
        doneAt = Time.time;
    }

    void Advance()
    {
        done = false;
        step++;
        stepStart = Time.time;
        stepWalked = stepSprinted = 0f;
        if (step == Step.GoToCupboard) MakeRing();
        if (step > Step.GoToCupboard) DestroyRing();
    }

    void OnCupboardOpened()
    {
        if (step <= Step.UseCupboard)                  // (also fine if the player got here before the tutorial did)
        {
            step = Step.UseCupboard;
            DestroyRing();
            Complete();
        }
        ShowNote(true);
    }

    void OnLightsOff()
    {
        if (portal != null) portal.SetActive(true);
        if (interactor != null) interactor.ShowBanner(portalMessage, 4f);
        DestroyRing();
        done = false;
        step = Step.UsePortal;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        // the player's opening lines come first: no box and nothing counts yet (the first step starts fresh when they are over)
        if (intro != null && !intro.Finished)
        {
            boxAlpha = 0f;
            stepStart = Time.time;
            if (player != null) { lastPos = player.transform.position; lastYaw = player.transform.eulerAngles.y; lastPitch = Pitch(); }
            if (interactor != null) interactor.hidePrompt = true;
            return;
        }
        TrackPlayer(kb);

        if (!done)
        {
            switch (step)
            {
                case Step.Look:
                    if (look[0] >= lookDegrees && look[1] >= lookDegrees && look[2] >= lookDegrees && look[3] >= lookDegrees) Complete();
                    break;
                case Step.Walk:
                    if (stepWalked >= walkMetres) Complete();
                    break;
                case Step.Sprint:
                    if (stepSprinted >= sprintMetres) Complete();
                    break;
                case Step.Reset:
                    if (kb != null && kb.rKey.wasPressedThisFrame) Complete();      // PlayerInteractor does the actual reset
                    break;
                case Step.GoToCupboard:
                    if (SpotDistance() < spotRadius) Complete();
                    break;
            }
        }
        else if (Time.time - doneAt > 0.9f && step < Step.FindSwitch) Advance();

        // the note: closes with E / Space / click (after a moment), or by itself the first time
        if (noteVisible && Time.time - noteOpenedAt > 0.6f)
        {
            bool key = (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                       || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (key || (noteAuto && Time.time - noteOpenedAt > noteShowTime)) CloseNote();
        }

        // no "Press E to ..." text (or yellow crosshair) until the tutorial asks you to interact, and none on top of the note
        if (interactor != null) interactor.hidePrompt = noteVisible || step < Step.UseCupboard;
        if (cupboard != null) cupboard.locked = step < Step.UseCupboard;       // E does nothing on it until the tutorial asks

        bool boxWanted = step <= Step.UseCupboard;
        boxAlpha = Mathf.MoveTowards(boxAlpha, boxWanted ? 1f : 0f, dt * 4f);
        noteAlpha = Mathf.MoveTowards(noteAlpha, noteVisible ? 1f : 0f, dt * 5f);
        UpdateRing();
    }

    void TrackPlayer(Keyboard kb)
    {
        if (player == null) return;

        // walking
        Vector3 p = player.transform.position;
        Vector3 d = p - lastPos; d.y = 0f;
        if (d.magnitude < 6f)                                          // a huge jump is a teleport / hitch, not walking (even a slow frame at a sprint is well under this)
        {
            stepWalked += d.magnitude;
            if (kb != null && kb.leftShiftKey.isPressed) stepSprinted += d.magnitude;
        }
        lastPos = p;

        // looking: how far in each direction (only counted during the "look" step)
        float yaw = player.transform.eulerAngles.y;
        float dyaw = Mathf.DeltaAngle(lastYaw, yaw);
        lastYaw = yaw;
        float pitch = Pitch();
        float dpitch = pitch - lastPitch;
        lastPitch = pitch;
        if (step == Step.Look && !done && Mathf.Abs(dyaw) < 90f && Mathf.Abs(dpitch) < 60f)
        {
            if (dpitch < 0f) look[0] += -dpitch; else look[1] += dpitch;      // up / down
            if (dyaw < 0f) look[2] += -dyaw; else look[3] += dyaw;            // left / right
        }
    }

    // ================================================================ the walk-to spot (floor ring)
    Vector3 SpotPosition()
    {
        Renderer r = cupboard.GetComponent<Renderer>();
        Bounds b = r.bounds;
        Vector3 front = cupboard.frontDirection.normalized;
        float half = Vector3.Dot(b.extents, new Vector3(Mathf.Abs(front.x), 0f, Mathf.Abs(front.z)));
        Vector3 s = b.center + front * (half + spotDistance);
        s.y = 0.012f;
        return s;
    }

    float SpotDistance()
    {
        if (player == null || cupboard == null) return float.MaxValue;
        Vector3 d = SpotPosition() - player.transform.position; d.y = 0f;
        return d.magnitude;
    }

    void MakeRing()
    {
        if (cupboard == null || ring != null) return;
        Shader shader = Shader.Find("Custom/ScreenGlow");                 // a plain glowing colour; lives in Resources so it is always in builds
        if (shader == null) return;
        ringMat = new Material(shader) { color = Accent };
        ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "TutorialSpot";
        Destroy(ring.GetComponent<Collider>());
        ring.transform.position = SpotPosition();
        ring.transform.localScale = new Vector3(spotRadius * 1.3f, 0.004f, spotRadius * 1.3f);
        MeshRenderer mr = ring.GetComponent<MeshRenderer>();
        mr.sharedMaterial = ringMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void UpdateRing()
    {
        if (ring == null) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
        float s = spotRadius * (1.15f + 0.15f * pulse);
        ring.transform.localScale = new Vector3(s, 0.004f, s);
        ringMat.color = Color.Lerp(new Color(0.55f, 0.4f, 0.1f), Accent, 0.5f + 0.5f * pulse);
    }

    void DestroyRing()
    {
        if (ring != null) Destroy(ring);
        ring = null;
    }

    // ================================================================ the note
    void ShowNote(bool auto)
    {
        if (noteVisible) return;
        if (Time.time - noteClosedAt < 0.3f) return;      // the E that closed it must not open it again straight away
        noteVisible = true;
        noteAuto = auto;
        noteOpenedAt = Time.time;
    }

    void CloseNote()
    {
        noteVisible = false;
        noteClosedAt = Time.time;
        if (step == Step.UseCupboard && !done) Complete();
    }

    // ================================================================ drawing
    static Texture2D MakeTriangle()
    {
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float halfWidth = (float)y / (n - 1) * 0.5f * (n - 1);          // wide at the bottom, a point at the top (y grows upwards in a texture)
                halfWidth = (1f - (float)y / (n - 1)) * 0.5f * (n - 1);
                px[y * n + x] = Mathf.Abs(x - (n - 1) / 2f) <= halfWidth ? Color.white : new Color(1f, 1f, 1f, 0f);
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    void MakeStyles()
    {
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, alignment = TextAnchor.UpperLeft, wordWrap = true };
        keyStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        smallKeyStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.UpperLeft, wordWrap = true };
        objectiveStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        distStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        noteTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter };
        noteStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter, wordWrap = true };
    }

    static void Round(Rect r, Color c, float radius)
    {
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, Vector4.zero, new Vector4(radius, radius, radius, radius));
    }

    static void Text(Rect r, string text, GUIStyle style, Color colour)
    {
        GUI.color = Color.white;                          // the colour is on the style; don't let an earlier tint change it
        style.normal.textColor = colour;
        GUI.Label(r, text, style);
    }

    // a keyboard key: lit = amber face with dark letter
    void Key(Rect r, string label, bool lit, float a, GUIStyle style = null, bool grow = true)
    {
        style = style ?? keyStyle;
        int oldSize = style.fontSize;
        if (grow)                                              // bigger key around its centre, with bigger letters
        {
            r = new Rect(r.center.x - r.width * TutorialUI.KeyBoost / 2f, r.center.y - r.height * TutorialUI.KeyBoost / 2f, r.width * TutorialUI.KeyBoost, r.height * TutorialUI.KeyBoost);
            style.fontSize = Mathf.RoundToInt(oldSize * TutorialUI.KeyBoost);
        }
        Round(r, new Color(0f, 0f, 0f, 0.5f * a), 9f);
        Rect face = new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 7f);
        Round(face, lit ? new Color(Accent.r, Accent.g, Accent.b, a) : new Color(0.20f, 0.23f, 0.29f, a), 8f);
        Text(face, label, style, lit ? new Color(0.1f, 0.08f, 0.02f, a) : new Color(0.85f, 0.88f, 0.95f, a));
        style.fontSize = oldSize;
    }

    // Rotate whatever is drawn next around a point given in the CURRENT drawing coordinates (GUIUtility.RotateAroundPivot
    // reads the pivot in screen coordinates, which is wrong inside the scaled teaching box)
    static void RotateAround(float angle, Vector2 pivot)
    {
        GUI.matrix = GUI.matrix * Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, angle), Vector3.one) * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
    }

    // a triangle pointing in a direction: 0 = up, 90 = right, 180 = down, 270 = left
    void Arrow(Vector2 centre, float size, float angle, Color c)
    {
        Matrix4x4 saved = GUI.matrix;
        RotateAround(angle, centre);
        GUI.color = c;
        GUI.DrawTexture(new Rect(centre.x - size / 2f, centre.y - size / 2f, size, size), triangle);
        GUI.matrix = saved;
        GUI.color = Color.white;
    }

    static void Stroke(Vector2 a, Vector2 b, float w, Color c)
    {
        Vector2 d = b - a;
        Matrix4x4 saved = GUI.matrix;
        RotateAround(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
        GUI.color = c;
        GUI.DrawTexture(new Rect(a.x, a.y - w / 2f, d.magnitude, w), Texture2D.whiteTexture);
        GUI.matrix = saved;
        GUI.color = Color.white;
    }

    static void Tick(Vector2 c, float s, Color col)
    {
        Stroke(c + new Vector2(-0.45f, 0.0f) * s, c + new Vector2(-0.12f, 0.32f) * s, s * 0.14f, col);
        Stroke(c + new Vector2(-0.12f, 0.32f) * s, c + new Vector2(0.48f, -0.38f) * s, s * 0.14f, col);
    }

    void OnGUI()
    {
        // (if Unity's GUI skin was not ready the first time, the styles come back with font size 0: build them again)
        if (titleStyle == null || titleStyle.fontSize == 0) MakeStyles();
        if (boxAlpha > 0.01f) DrawBox();
        DrawWaypoint();
        DrawObjective();
        if (noteAlpha > 0.01f) DrawNote();
        GUI.color = Color.white;
    }

    // ---------------------------------------------------------------- the teaching box (bottom left)
    void DrawBox()
    {
        float a = boxAlpha;
        const float w = 440f, h = 200f;                          // the box is laid out in these units, then scaled as a whole (Box Scale)
        Matrix4x4 savedMatrix = GUI.matrix;
        float sc = boxScale * TutorialUI.SizeFactor;             // grows with the screen, so full screen is not tiny
        Vector3 origin = new Vector3(Screen.width - w * sc - 24f, Screen.height - h * sc - 24f, 0f);   // bottom right: the spot you walk to is usually low on the left
        GUI.matrix = savedMatrix * Matrix4x4.TRS(origin, Quaternion.identity, new Vector3(sc, sc, 1f));
        Rect box = new Rect(0f, 0f, w, h);
        Round(box, new Color(0.03f, 0.05f, 0.08f, 0.9f * a), 12f);
        GUI.color = new Color(Accent.r, Accent.g, Accent.b, a);
        GUI.DrawTexture(new Rect(box.x + 14f, box.y, box.width - 28f, 3f), Texture2D.whiteTexture);

        string title = "", body = "", small = "";
        float progress = -1f;
        Vector2 icon = new Vector2(box.x + 120f, box.y + 112f);          // centre of the icon area
        float textX = box.x + 238f, textW = w - 238f - 18f;

        switch (step)
        {
            case Step.Look:
            {
                title = "LOOK AROUND"; body = "Move the mouse to look around.";
                small = "Look up, down, left and right.";
                float sum = 0f; for (int i = 0; i < 4; i++) sum += Mathf.Clamp01(look[i] / lookDegrees);
                progress = sum / 4f;
                // mouse
                Rect body_ = new Rect(icon.x - 26f, icon.y - 40f, 52f, 80f);
                Round(body_, new Color(0.75f, 0.78f, 0.85f, a), 26f);
                Round(new Rect(body_.x + 3f, body_.y + 3f, body_.width - 6f, body_.height - 6f), new Color(0.14f, 0.16f, 0.2f, a), 23f);
                GUI.color = new Color(0.75f, 0.78f, 0.85f, a);
                GUI.DrawTexture(new Rect(icon.x - 1f, body_.y + 4f, 2f, 28f), Texture2D.whiteTexture);                // button split
                GUI.DrawTexture(new Rect(body_.x + 4f, body_.y + 32f, body_.width - 8f, 2f), Texture2D.whiteTexture);
                Round(new Rect(icon.x - 4f, body_.y + 9f, 8f, 16f), new Color(Accent.r, Accent.g, Accent.b, a), 4f);      // wheel
                // the four arrows: dim until you have looked that way, amber once you have
                Vector2[] at = { icon + new Vector2(0f, -58f), icon + new Vector2(0f, 58f), icon + new Vector2(-62f, 0f), icon + new Vector2(62f, 0f) };
                float[] ang = { 0f, 180f, 270f, 90f };
                for (int i = 0; i < 4; i++)
                {
                    float f = Mathf.Clamp01(look[i] / lookDegrees);
                    Color c = Color.Lerp(Dim, Accent, f >= 1f ? 1f : f * 0.5f);
                    Arrow(at[i], 26f, ang[i], new Color(c.r, c.g, c.b, a));
                }
                break;
            }
            case Step.Walk:
            {
                title = "WALK"; body = "Use W A S D to walk around.";
                small = "Walk a few steps.";
                progress = Mathf.Clamp01(stepWalked / walkMetres);
                Keyboard kb = Keyboard.current;
                float k = 62f, g = 6f;                                   // (the WASD keys are laid out big already: no extra growing)
                Vector2 o = new Vector2(icon.x - (3f * k + 2f * g) / 2f, icon.y - k - g / 2f - 4f);
                Key(new Rect(o.x + k + g, o.y, k, k), "W", kb != null && kb.wKey.isPressed, a, null, false);
                Key(new Rect(o.x, o.y + k + g, k, k), "A", kb != null && kb.aKey.isPressed, a, null, false);
                Key(new Rect(o.x + k + g, o.y + k + g, k, k), "S", kb != null && kb.sKey.isPressed, a, null, false);
                Key(new Rect(o.x + 2f * (k + g), o.y + k + g, k, k), "D", kb != null && kb.dKey.isPressed, a, null, false);
                break;
            }
            case Step.Sprint:
            {
                title = "SPRINT"; body = "Hold SHIFT while you walk to sprint.";
                small = "Keep W held and add SHIFT.";
                progress = Mathf.Clamp01(stepSprinted / sprintMetres);
                Keyboard kb = Keyboard.current;
                Key(new Rect(icon.x - 78f, icon.y - 30f, 156f, 60f), "SHIFT", kb != null && kb.leftShiftKey.isPressed, a);
                break;
            }
            case Step.Reset:
            {
                title = "GOT STUCK?"; body = "Press R if you ever get stuck.";
                small = "It puts you back at the start. Nothing you did is lost. (Try it now.)";
                Keyboard kb = Keyboard.current;
                Key(new Rect(icon.x - 36f, icon.y - 36f, 72f, 72f), "R", kb != null && kb.rKey.isPressed, a);
                break;
            }
            case Step.GoToCupboard:
            {
                title = "GO TO THE CUPBOARD"; body = "Walk to the glowing spot in front of the cupboard.";
                small = "Follow the marker.";
                float bob = Mathf.Sin(Time.time * 4f) * 5f;
                Arrow(icon + new Vector2(0f, -6f + bob), 64f, 180f, new Color(Accent.r, Accent.g, Accent.b, a));
                break;
            }
            case Step.UseCupboard:
            {
                title = "INTERACT"; body = "Look at the cupboard and press [E] to open it.";
                small = "E works on most things you can look at.";
                Keyboard kb = Keyboard.current;
                Key(new Rect(icon.x - 36f, icon.y - 36f, 72f, 72f), "E", kb != null && kb.eKey.isPressed, a);
                break;
            }
        }

        Text(new Rect(box.x + 20f, box.y + 12f, 300f, 26f), title, titleStyle, new Color(Accent.r, Accent.g, Accent.b, a));
        Text(new Rect(textX, box.y + 46f, textW, 90f), body, bodyStyle, new Color(1f, 1f, 1f, a));
        Text(new Rect(textX, box.y + 120f, textW, 60f), small, smallKeyStyle, new Color(0.62f, 0.66f, 0.74f, a));

        if (progress >= 0f)                                    // a thin progress bar along the bottom
        {
            Rect bar = new Rect(box.x + 20f, box.y + h - 18f, w - 40f, 5f);
            Round(bar, new Color(1f, 1f, 1f, 0.12f * a), 3f);
            if (progress > 0.01f) Round(new Rect(bar.x, bar.y, bar.width * progress, bar.height), new Color(Accent.r, Accent.g, Accent.b, a), 3f);
        }

        if (done)                                              // finished: a green tick
        {
            Round(new Rect(box.xMax - 56f, box.y + 8f, 44f, 36f), new Color(0.08f, 0.2f, 0.1f, 0.95f * a), 10f);
            Tick(new Vector2(box.xMax - 34f, box.y + 27f), 22f, new Color(Good.r, Good.g, Good.b, a));
        }
        GUI.matrix = savedMatrix;
        GUI.color = Color.white;
    }

    // ---------------------------------------------------------------- the pointer to the walk-to spot
    void DrawWaypoint()
    {
        if (step != Step.GoToCupboard || done || cam == null || cupboard == null) return;

        Vector3 spot = SpotPosition();
        Vector3 world = spot + Vector3.up * 1.6f;                         // the marker floats at head height above the spot
        Vector3 sp = cam.WorldToScreenPoint(world);
        float dist = Mathf.Max(0f, SpotDistance());
        Vector2 centre = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Vector2 pos = new Vector2(sp.x, Screen.height - sp.y);
        bool behind = sp.z < 0f;
        const float margin = 70f;
        bool inside = !behind && pos.x > margin && pos.x < Screen.width - margin && pos.y > margin && pos.y < Screen.height - margin;
        float bob = Mathf.Sin(Time.time * 4f) * 6f;

        if (inside)
        {
            Arrow(pos + new Vector2(0f, bob), 38f, 180f, Accent);                     // a marker pointing down at the spot
            Text(new Rect(pos.x - 60f, pos.y - 44f + bob, 120f, 24f), dist.ToString("0.0") + " m", distStyle, Accent);
        }
        else
        {
            // off screen: an arrow on the edge of the screen, pointing the way (turn that way)
            Vector2 dir = behind ? -(pos - centre) : (pos - centre);
            if (dir.sqrMagnitude < 1f) dir = Vector2.down;
            dir.Normalize();
            float scale = Mathf.Min((Screen.width / 2f - margin) / Mathf.Max(0.01f, Mathf.Abs(dir.x)),
                                    (Screen.height / 2f - margin) / Mathf.Max(0.01f, Mathf.Abs(dir.y)));
            Vector2 edge = centre + dir * scale;
            Arrow(edge, 46f, Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg, Accent);
            Text(new Rect(edge.x - 60f, edge.y + 22f, 120f, 24f), dist.ToString("0.0") + " m", distStyle, Accent);
        }
        GUI.color = Color.white;
    }

    // ---------------------------------------------------------------- objective line (top left), for the later steps
    void DrawObjective()
    {
        string objective = step == Step.FindSwitch ? "Find the light switch" : step == Step.UsePortal ? "Step through the portal" : "";
        if (objective == "") return;
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(new Rect(18f, 18f, 420f, 40f), Texture2D.whiteTexture);
        Text(new Rect(30f, 18f, 400f, 40f), "▶  " + objective, objectiveStyle, Accent);
    }

    // ---------------------------------------------------------------- the note, as a sheet of paper (pops in)
    void DrawNote()
    {
        float a = noteAlpha;
        float pop = Mathf.SmoothStep(0.85f, 1f, Mathf.Clamp01((Time.time - noteOpenedAt) / 0.25f));
        if (!noteVisible) pop = 1f;
        float w = 520f * pop, h = 250f * pop;
        Rect r = new Rect(Screen.width / 2f - w / 2f, Screen.height / 2f - h / 2f - 30f, w, h);
        GUI.color = new Color(0f, 0f, 0f, 0.35f * a);
        GUI.DrawTexture(new Rect(r.x + 6f, r.y + 8f, w, h), Texture2D.whiteTexture);          // soft shadow
        GUI.color = new Color(0.96f, 0.92f, 0.78f, a);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        Color faint = new Color(0.35f, 0.3f, 0.2f, a);
        Text(new Rect(r.x, r.y + 14f * pop, w, 28f), "a note, stuck to the back of the cupboard", noteTitleStyle, faint);
        Text(new Rect(r.x + 24f, r.y + 50f * pop, w - 48f, h - 100f * pop), noteText, noteStyle, new Color(0.1f, 0.1f, 0.25f, a));
        Text(new Rect(r.x, r.y + h - 40f * pop, w, 28f), noteAuto ? "[E] Continue" : "[E] Close", noteTitleStyle, faint);
    }
}
