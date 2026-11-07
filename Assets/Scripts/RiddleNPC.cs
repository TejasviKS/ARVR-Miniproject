using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// VERSION 1
// The lab assistant (Lab Asst. Gemini) in the CAD lab bench room: he quizzes you before you may go on.
// (The class keeps its old name, RiddleNPC, so the scene keeps working; "riddles" are now course questions.)
// Put this on an EMPTY object standing on the floor.
//
// When the game starts it:
//  1. builds a glowing hologram body from simple shapes (or uses your own Model if you set one),
//  2. adds a flickering cyan light and a low 3D hum you can hear from ~10 m,
//  3. hides the Portal (the way into Lab 1) until the riddles are done.
// Press E on him -> dialogue box at the bottom of the screen:
//   intro lines -> 3 questions (ONE try each, options shuffled) -> score -> portal appears.
// The score decides how many faulty computers the player must fix next (3 if all are right, up to 6 if all are wrong;
// saved in GameProgress).
// R (emergency reset) closes the dialogue at any moment; talking again continues where you left off.

[System.Serializable]
public class Riddle
{
    [TextArea(2, 4)] public string question;
    [Tooltip("The answers. They are shuffled every time, so the order here doesn't matter.")]
    public string[] options = new string[3];
    [Tooltip("Which option above is right: 0 = first, 1 = second, 2 = third")]
    public int correctIndex;

    public Riddle() { }
    public Riddle(string q, string[] o, int correct) { question = q; options = o; correctIndex = correct; }
}

[RequireComponent(typeof(CapsuleCollider))]
public class RiddleNPC : Interactable
{
    [Header("Who")]
    public string npcName = "Lab Asst. Gemini";
    [Tooltip("Optional: your own 3D model (a child of this object). Empty = a hologram is built from simple shapes.")]
    public GameObject model;
    [Tooltip("ON = your model also gets the see-through hologram look")]
    public bool hologramLookOnModel = true;
    public Color hologramColor = new Color(0.2f, 0.85f, 1f);

    [Header("What he says")]
    [TextArea(1, 3)]
    public string[] introLines =
    {
        "Hi, I'm Lab Asst. Gemini. Welcome to the CAD lab.",
        "Some of the computers in here are faulty, and they need fixing before class.",
        "Answer three questions to proceed. Every wrong answer means more computers to fix.",
    };
    public Riddle[] riddles =
    {
        new Riddle("Which type of testing is performed after coding every module?",
                   new[] { "Unit testing", "Integration testing", "Acceptance testing" }, 0),
        new Riddle("Which software development life cycle model uses pair programming?",
                   new[] { "Extreme Programming", "Lean", "Waterfall" }, 0),
        new Riddle("Which testing technique involves examining the internal code structure without executing the program?",
                   new[] { "Static testing", "Dynamic testing", "Performance testing" }, 0),
    };
    public string correctLine = "Correct.";
    [Tooltip("{0} = the right answer")]
    public string wrongLine = "Not quite. The answer is: {0}.";
    [Tooltip("{0} = how many right, {1} = how many questions, {2} = e.g. \"3 computers\"")]
    [TextArea(1, 3)] public string scoreLine = "{0} out of {1} correct. You'll need to fix {2}.";
    [TextArea(1, 3)] public string[] outroLines = { "Head through the portal by the door when you're ready. The faulty computers are in Lab 1." };
    [Tooltip("Said when you talk to him again after finishing")]
    public string afterLine = "The faulty computers are in Lab 1, through the portal by the door.";

    [Header("Computers to fix for each score")]
    [Tooltip("Element 0 = 0 right, Element 1 = 1 right, Element 2 = 2 right, Element 3 = 3 right. At least 3, at most 6.")]
    public int[] computersForScore = { 6, 5, 4, 3 };

    [Header("When the riddles are done")]
    [Tooltip("Hidden at the start, appears when the riddles are finished (the portal by the door)")]
    public GameObject portal;
    public UnityEvent onFinished;

    [Header("Look & sound")]
    public float lettersPerSecond = 40f;
    public float bobHeight = 0.06f;
    [Tooltip("How fast he turns to face you (degrees per second)")]
    public float turnSpeed = 60f;
    public float lightRange = 4f;
    public float lightIntensity = 1.5f;
    [Range(0f, 1f)] public float humVolume = 0.35f;
    [Tooltip("How far away (metres) the hum and voice can be heard")]
    public float hearingDistance = 10f;

    // ---- Conversation state ----
    enum Mode { Closed, Typing, WaitingForContinue, Choosing }
    Mode mode = Mode.Closed;
    PlayerInteractor player;
    Coroutine talk;
    string shownText = "";
    bool continuePressed;
    float ignoreInputUntil;
    Riddle currentRiddle;
    int[] order;              // shuffled option order: button i shows option order[i]
    int chosen = -1;

    // ---- Progress (kept when the dialogue is closed with R) ----
    bool introDone, finished;
    int riddleIndex, correctCount;

    // ---- Looks & sound ----
    Transform holo;           // the part that floats up and down
    Vector3 holoBase;
    Material holoMat;
    Light glowLight;
    Transform playerT;
    Color flashColor;
    float flashUntil;
    AudioSource hum, voice;
    AudioClip humClip, blipClip, rightClip, wrongClip, openClip;
    GUIStyle nameStyle, textStyle, hintStyle, buttonStyle;

    void Start()
    {
        Debug.Log("[Scripts] RiddleNPC v1 running on \"" + name + "\"", this);

        if (portal != null) portal.SetActive(false);
        else Debug.LogWarning("RiddleNPC: Portal is not set, so nothing will open after the questions. Drag the portal into the Portal slot.", this);

        for (int i = 0; i < riddles.Length; i++)
        {
            Riddle r = riddles[i];
            if (r.options == null || r.options.Length < 2 || r.correctIndex < 0 || r.correctIndex >= r.options.Length)
                Debug.LogWarning("RiddleNPC: riddle " + (i + 1) + " needs at least 2 options and a Correct Index that points at one of them.", this);
        }

        // Invisible "press E" capsule around him (a trigger, so it never blocks the player)
        CapsuleCollider col = GetComponent<CapsuleCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0f, 0.9f, 0f);
        col.height = 1.8f;
        col.radius = 0.45f;

        // Body
        Shader shader = Shader.Find("Custom/Hologram");
        if (shader == null)
        {
            Debug.LogWarning("RiddleNPC: Hologram shader not found: put Hologram.shader in Assets/Resources. Using a plain unlit colour.", this);
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        holoMat = new Material(shader) { color = hologramColor };
        if (model != null) UseOwnModel();
        else BuildHologram();
        holoBase = holo.localPosition;

        // Cyan glow light
        GameObject lightGo = new GameObject("HologramLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        glowLight = lightGo.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.range = lightRange;
        glowLight.intensity = lightIntensity;
        glowLight.color = hologramColor;
        glowLight.shadows = LightShadows.None;    // cheap on low-end laptops

        // 3D sound: a looping hum, plus a second source for the voice blips and effects
        MakeSounds();
        hum = Make3DSource(humVolume, hearingDistance);
        hum.clip = humClip;
        hum.loop = true;
        hum.Play();
        voice = Make3DSource(0.7f, hearingDistance + 5f);

        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) playerT = pc.transform;
    }

    // ================= Interactable =================
    public override string GetPrompt() => mode != Mode.Closed ? "" : "Press E to talk to " + npcName;

    public override void Interact(PlayerInteractor p)
    {
        if (mode == Mode.Closed)
        {
            player = p;
            p.Lock(this);                         // stop walking, E now comes to us
            talk = StartCoroutine(Conversation());
            return;
        }
        if (Time.time >= ignoreInputUntil) continuePressed = true;   // E while talking = continue
    }

    // R key: close the dialogue right away. Progress (intro seen, riddles answered) is kept.
    public override void ForceRelease(PlayerInteractor p)
    {
        if (talk != null) StopCoroutine(talk);
        if (riddleIndex >= riddles.Length && !finished) Finish();   // all answered? still open the way
        Close();
        base.ForceRelease(p);
    }

    // ================= The conversation =================
    IEnumerator Conversation()
    {
        if (finished)
        {
            yield return Say(afterLine);
            Close();
            yield break;
        }

        if (!introDone)
        {
            foreach (string line in introLines) yield return Say(line);
            introDone = true;
        }

        while (riddleIndex < riddles.Length)
        {
            Riddle r = riddles[riddleIndex];
            yield return Say("Question " + (riddleIndex + 1) + " of " + riddles.Length + ":\n" + r.question, false);
            yield return Choose(r);

            bool right = order[chosen] == r.correctIndex;
            if (right) correctCount++;
            riddleIndex++;                        // one attempt only: right or wrong, move on
            React(right);
            yield return Say(right ? correctLine : string.Format(wrongLine, r.options[r.correctIndex]));
        }

        yield return Say(ScoreText());
        Finish();
        foreach (string line in outroLines) yield return Say(line);
        Close();
    }

    // Type a line out letter by letter, then (usually) wait for E / Space / click
    IEnumerator Say(string text, bool waitForContinue = true)
    {
        mode = Mode.Typing;
        continuePressed = false;
        float letters = 0f;
        int shown = 0;
        while (shown < text.Length)
        {
            if (continuePressed) { continuePressed = false; break; }   // pressed early: show the whole line
            letters += Time.deltaTime * lettersPerSecond;
            while (shown < text.Length && shown < (int)letters)
            {
                shown++;
                if (shown % 3 == 0 && !char.IsWhiteSpace(text[shown - 1])) Blip();
            }
            shownText = text.Substring(0, shown);
            yield return null;
        }
        shownText = text;
        if (!waitForContinue) yield break;

        mode = Mode.WaitingForContinue;
        continuePressed = false;
        while (!continuePressed) yield return null;
        continuePressed = false;
    }

    // Show the answer buttons (in a new random order) and wait until one is picked
    IEnumerator Choose(Riddle r)
    {
        currentRiddle = r;
        order = new int[r.options.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        chosen = -1;
        mode = Mode.Choosing;
        SetCursorFree(true);                      // so the buttons can be clicked
        while (chosen < 0) yield return null;
        SetCursorFree(false);
        ignoreInputUntil = Time.time + 0.25f;     // the click on the button shouldn't also skip the next line
    }

    void Pick(int button)
    {
        if (mode == Mode.Choosing && button < order.Length) chosen = button;
    }

    // All riddles answered: save the score and open the way
    void Finish()
    {
        finished = true;
        GameProgress.riddlesDone = true;
        GameProgress.riddlesCorrect = correctCount;
        GameProgress.riddlesTotal = riddles.Length;
        GameProgress.computersToSwitchOff = ComputersForScore();

        if (portal != null)
        {
            portal.SetActive(true);
            AudioSource.PlayClipAtPoint(openClip, portal.transform.position, 0.9f);
        }
        if (player != null) player.ShowBanner("A portal has opened by the door!", 3f);
        onFinished.Invoke();
        Debug.Log("[RiddleNPC] Finished: " + correctCount + "/" + riddles.Length + " right -> " + GameProgress.computersToSwitchOff + " computers to fix.", this);
    }

    void Close()
    {
        mode = Mode.Closed;
        talk = null;
        SetCursorFree(false);
        if (player != null && player.Locked == this) player.Unlock();
    }

    int ComputersForScore()
    {
        if (computersForScore == null || computersForScore.Length == 0) return 3;
        return Mathf.Clamp(computersForScore[Mathf.Clamp(correctCount, 0, computersForScore.Length - 1)], 3, 6);
    }

    string ScoreText()
    {
        int n = ComputersForScore();
        string line = string.Format(scoreLine, correctCount, riddles.Length, n + (n == 1 ? " computer" : " computers"));
        return char.ToUpper(line[0]) + line.Substring(1);
    }

    static string NumberWord(int n)
    {
        string[] words = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };
        return n >= 0 && n < words.Length ? words[n] : n.ToString();
    }

    static void SetCursorFree(bool free)
    {
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = free;
    }

    // ================= Every frame =================
    void Update()
    {
        // ---- Keys while talking ----
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (mode == Mode.Choosing && kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Pick(0);
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Pick(1);
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Pick(2);
        }
        else if (mode != Mode.Closed && Time.time >= ignoreInputUntil)
        {
            bool space = kb != null && kb.spaceKey.wasPressedThisFrame;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (space || click) continuePressed = true;
        }

        // ---- Float up and down ----
        if (holo != null)
            holo.localPosition = holoBase + Vector3.up * (bobHeight + Mathf.Sin(Time.time * 1.5f) * bobHeight);

        // ---- Slowly turn to face the player (only left/right) ----
        if (playerT != null)
        {
            Vector3 dir = playerT.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
        }

        // ---- Light flicker, and a red/green flash after an answer ----
        bool flashing = Time.time < flashUntil;
        Color c = flashing ? flashColor : hologramColor;
        if (glowLight != null)
        {
            glowLight.color = c;
            glowLight.intensity = lightIntensity * (flashing ? 2f : 1f) * (0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 6f, 0f));
        }
        if (holoMat != null) holoMat.color = c;
    }

    void React(bool right)
    {
        flashColor = right ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.15f, 0.1f);
        flashUntil = Time.time + 0.8f;
        voice.pitch = 1f;
        voice.PlayOneShot(right ? rightClip : wrongClip);
    }

    void Blip()
    {
        voice.pitch = Random.Range(0.85f, 1.3f);  // random pitch = sounds like alien speech
        voice.PlayOneShot(blipClip, 0.8f);
    }

    // ================= Dialogue box (built-in UI, no Canvas needed) =================
    void OnGUI()
    {
        if (mode == Mode.Closed) return;
        if (nameStyle == null)
        {
            nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            nameStyle.normal.textColor = hologramColor;
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true };
            textStyle.normal.textColor = Color.white;
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleRight };
            hintStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, wordWrap = true };
        }

        // laid out in a 'virtual' screen that is scaled up with the real one, so it is not tiny in full screen
        float S = TutorialUI.SizeFactor * 0.9f;
        float sw = Screen.width / S, sh = Screen.height / S;
        Matrix4x4 savedMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(S, S, 1f));
        float w = Mathf.Min(900f, sw - 40f), h = 240f;
        float x = (sw - w) / 2f, y = sh - h - 20f;

        // Dark panel with a coloured line on top
        GUI.color = new Color(0.02f, 0.05f, 0.1f, 0.85f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = hologramColor;
        GUI.DrawTexture(new Rect(x, y, w, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(x + 20f, y + 10f, w - 40f, 30f), npcName, nameStyle);
        GUI.Label(new Rect(x + 20f, y + 44f, w - 40f, 120f), shownText, textStyle);

        if (mode == Mode.Choosing)
        {
            int n = order.Length;
            float gap = 20f, bw = (w - 40f - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                string label = (i < 3 ? (i + 1) + ".  " : "") + currentRiddle.options[order[i]];
                if (GUI.Button(new Rect(x + 20f + i * (bw + gap), y + h - 70f, bw, 52f), label, buttonStyle)) Pick(i);
            }
        }
        else if (mode == Mode.WaitingForContinue)
        {
            GUI.Label(new Rect(x + 20f, y + h - 34f, w - 40f, 24f), "Press Space to continue", hintStyle);
        }
        GUI.matrix = savedMatrix;
    }

    // ================= Building the hologram =================
    void BuildHologram()
    {
        holo = new GameObject("Hologram").transform;
        holo.SetParent(transform, false);
        Part(PrimitiveType.Capsule, holo, new Vector3(0f, 0.7f, 0f), new Vector3(0.5f, 0.7f, 0.35f));     // body, 1.4 m
        Part(PrimitiveType.Sphere, holo, new Vector3(0f, 1.58f, 0f), new Vector3(0.3f, 0.32f, 0.3f));     // head
        Part(PrimitiveType.Cube, holo, new Vector3(0f, 1.6f, 0.13f), new Vector3(0.2f, 0.05f, 0.06f));    // glowing visor (shows where he looks)
        Part(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.01f, 0f), new Vector3(0.9f, 0.005f, 0.9f)); // projector disc on the floor (doesn't float)
    }

    void Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());         // looks only: the capsule trigger on this object is for E
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = holoMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    void UseOwnModel()
    {
        holo = model.transform;
        if (!hologramLookOnModel) return;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = holoMat;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    // ================= Sounds made in code (no audio files needed) =================
    AudioSource Make3DSource(float volume, float maxDistance)
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.volume = volume;
        s.spatialBlend = 1f;                          // fully 3D: direction + distance
        s.rolloffMode = AudioRolloffMode.Linear;      // silent beyond maxDistance
        s.minDistance = 1f;
        s.maxDistance = maxDistance;
        s.dopplerLevel = 0f;
        s.playOnAwake = false;
        return s;
    }

    void MakeSounds()
    {
        // 2-second loop: low electric hum that slowly swells (all frequencies fit the loop exactly)
        humClip = MakeClip("hum", 2f, t =>
        {
            float swell = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
            float s = 0.55f * Mathf.Sin(2f * Mathf.PI * 55f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * 110f * t)
                    + 0.12f * Mathf.Sin(2f * Mathf.PI * 165f * t);
            return s * swell * 0.4f;
        });
        // Tiny "voice" blip for each few letters
        blipClip = MakeBlip();
        // Right answer: two rising notes
        rightClip = MakeClip("right", 0.6f, t =>
        {
            float f = t < 0.2f ? 880f : 1318.5f;
            float local = t < 0.2f ? t : t - 0.2f;
            return Mathf.Sin(2f * Mathf.PI * f * t) * 0.4f * Mathf.Exp(-local * 6f);
        });
        // Wrong answer: low buzz
        wrongClip = MakeClip("wrong", 0.5f, t =>
            ((110f * t) % 1f * 2f - 1f) * 0.35f * (1f - t / 0.5f));
        // Portal opens: rising sweep that fades in and out
        openClip = MakeClip("open", 1.2f, t =>
        {
            float phase = 2f * Mathf.PI * (200f * t + (900f - 200f) * t * t / (2f * 1.2f));
            return Mathf.Sin(phase) * 0.35f * Mathf.Sin(Mathf.PI * t / 1.2f);
        });
    }

    // The little "voice" blip. Shared: the professors (ProfessorAI) talk with exactly the same sound.
    public static AudioClip MakeBlip() => MakeClip("blip", 0.07f, t =>
        (Mathf.Sin(2f * Mathf.PI * 520f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * 1040f * t)) * 0.3f * Mathf.Sin(Mathf.PI * t / 0.07f));

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
    // Cyan outline in the Scene view so you can see where he will stand
    void OnDrawGizmos()
    {
        Gizmos.color = hologramColor;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f));
        Gizmos.DrawRay(transform.position + Vector3.up * 1.6f, transform.forward * 0.5f);
    }
#endif
}
