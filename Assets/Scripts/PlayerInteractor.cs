using UnityEngine;
using UnityEngine.InputSystem;

// VERSION 8
// Put this on the Player (next to PlayerController).
// Every frame it looks for an Interactable in front of the camera.
// If it finds one, it shows "Press E to ..." and pressing E calls that object's Interact().
// It also draws the crosshair dot and on-screen messages.
public class PlayerInteractor : MonoBehaviour
{
    [Tooltip("How far away (metres) you can interact from")]
    public float reach = 2.5f;

    [HideInInspector] public Camera cam;
    [HideInInspector] public PlayerController movement;

    public Interactable Focused { get; private set; }  // what we're looking at
    public Interactable Locked { get; private set; }   // e.g. the seat we're sitting in
    [HideInInspector] public bool hidePrompt;          // another script (e.g. the tutorial's note) is showing something over the screen

    bool cursorWasLocked;
    string message;                    // small info box at the bottom ("Picked up: ...", "Code: 3 7 _ _")
    float messageUntil;
    string banner;                     // BIG text at the top: only for a portal opening / a level being cleared
    float bannerUntil;
    GUIStyle promptStyle, messageStyle;

    void Awake()
    {
        cam = GetComponentInChildren<Camera>(true);
        movement = GetComponent<PlayerController>();
        Debug.Log("[Scripts] PlayerInteractor v8 running");
    }

    void Start() => PlayerSpawn.EnsureExists();       // R = back to the start, in every scene

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || cam == null) return;
        if (WastedScreen.IsShowing) { Focused = null; return; }       // the defeat screen is up: nothing else may react (or re-enable walking)

        // EMERGENCY RESET: press R if anything gets stuck (instantly stand up, walking back on).
        // In scenes that have a PlayerSpawn (the cabins) it also puts you back where you started.
        if (kb.rKey.wasPressedThisFrame && !PortalTransition.Busy)      // (not in the middle of a portal transition)
        {
            if (Locked != null) Locked.ForceRelease(this);
            Locked = null;
            if (movement != null) { movement.enabled = false; movement.enabled = true; }  // re-sync camera
            if (PlayerSpawn.Current != null)
            {
                PlayerSpawn.Current.ResetPlayer();
                ShowMessage("Back at the start", 1.5f);
            }
            else ShowMessage("View reset", 1f);          // (only if the scene has no spawn at all)
        }

        // Safety net: if walking got switched off but nothing is holding us, switch it back on
        if (Locked == null && movement != null && !movement.enabled) movement.enabled = true;

        if (Locked != null)
        {
            Focused = Locked;          // while sitting, E always means "stand up"
        }
        else
        {
            Focused = FindTarget();
        }

        // E always works. A left click works too on things that accept it (wall switches), but only when the
        // mouse was already captured: the click that grabs the mouse back must not also press something.
        var mouse = Mouse.current;
        bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame
                       && cursorWasLocked && Locked == null && Focused != null && Focused.AcceptsClick;

        if (Focused != null && (kb.eKey.wasPressedThisFrame || clicked))
            Focused.Interact(this);
    }

    void LateUpdate()
    {
        cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;   // as it was at the end of this frame
    }

    Interactable FindTarget()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        // 1) Exact thin line first: picks precisely the small thing under the crosshair (e.g. one switch)
        Interactable exact = Nearest(Physics.RaycastAll(ray, reach, ~0, QueryTriggerInteraction.Collide), ray);
        if (exact != null) return exact;

        // 2) Otherwise a forgiving thick ray (15 cm) for big invisible boxes like seats,
        //    which goes THROUGH everything so seats hidden inside bench colliders are still found.
        return Nearest(Physics.SphereCastAll(ray, 0.15f, reach, ~0, QueryTriggerInteraction.Collide), ray);
    }

    Interactable Nearest(RaycastHit[] hits, Ray ray)
    {
        Interactable best = null;
        float bestDist = float.MaxValue;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;   // ignore our own body
            Interactable it = hit.collider.GetComponentInParent<Interactable>();
            if (it == null || !it.enabled || !it.CanInteract) continue;
            if (it.transform.IsChildOf(transform)) continue;              // never anything on the Player

            float d = hit.distance > 0f ? hit.distance : Vector3.Distance(ray.origin, hit.collider.bounds.center);
            if (d < bestDist) { bestDist = d; best = it; }
        }
        return best;
    }

    // Called by things like SitSpot: freeze walking and send E presses to them
    public void Lock(Interactable it)
    {
        Locked = it;
        if (movement != null) movement.enabled = false;
    }

    // Walking switched back on -> PlayerController.OnEnable re-syncs the camera and WASD
    public void Unlock()
    {
        Locked = null;
        if (movement != null) movement.enabled = true;
    }

    // A short message in a small box at the bottom of the screen (same look as the tutorial box), e.g. "Wrong code!"
    public void ShowMessage(string text, float seconds = 3f)
    {
        message = text;
        messageUntil = Time.time + seconds;
    }

    // BIG white text at the top of the screen. Only for the big moments: a portal opening / a level being cleared.
    public void ShowBanner(string text, float seconds = 4f)
    {
        banner = text;
        bannerUntil = Time.time + seconds;
    }

    static void ShadowLabel(Rect r, string text, GUIStyle style)
    {
        Color keep = style.normal.textColor;
        style.normal.textColor = new Color(0.04f, 0.02f, 0.06f, 0.95f);
        foreach (Vector2 o in new[] { new Vector2(-2f, 0f), new Vector2(2f, 0f), new Vector2(0f, -2f), new Vector2(0f, 2f), new Vector2(2f, 2f) })
            GUI.Label(new Rect(r.x + o.x, r.y + o.y, r.width, r.height), text, style);
        style.normal.textColor = keep;
        GUI.Label(r, text, style);
    }

    // the small box at the bottom: the tutorial box's rounded dark panel with the gold line on top
    void DrawInfoBox()
    {
        TutorialUI.Ensure();
        float left = messageUntil - Time.time;
        float a = Mathf.Clamp01(left / 0.35f);
        float scale = Mathf.Clamp(Screen.height / 1080f, 0.6f, 1.3f) * 0.9f;
        const float w = 440f, h = 64f;
        Matrix4x4 saved = GUI.matrix;
        GUI.matrix = saved * Matrix4x4.TRS(new Vector3(Screen.width / 2f - w * scale / 2f, Screen.height - h * scale - 40f, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        Rect box = new Rect(0f, 0f, w, h);
        TutorialUI.Panel(box, a);
        GUIStyle st = TutorialUI.BodyStyle;
        TextAnchor oldAnchor = st.alignment; st.alignment = TextAnchor.MiddleCenter;
        TutorialUI.Text(new Rect(box.x + 12f, box.y + 6f, box.width - 24f, box.height - 8f), message, st, new Color(1f, 1f, 1f, a));
        st.alignment = oldAnchor;
        GUI.matrix = saved;
        GUI.color = Color.white;
    }

    // Simple built-in UI (no Canvas setup needed)
    void OnGUI()
    {
        if (promptStyle == null || promptStyle.fontSize == 0)     // (font size 0 = Unity's GUI skin was not ready yet: build again)
        {
            promptStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold };
            promptStyle.normal.textColor = Color.white;
            messageStyle = new GUIStyle(promptStyle) { fontSize = 32 };
        }

        if (WastedScreen.IsShowing) return;
        float cx = Screen.width / 2f, cy = Screen.height / 2f;

        // Crosshair dot (bigger + yellow when looking at something usable)
        bool active = Focused != null && Locked == null && !hidePrompt;
        float size = active ? 10f : 5f;
        GUI.color = new Color(0.08f, 0.04f, 0.1f, 0.85f);                // dark rim, so the dot reads on light or flat-colour rooms too
        GUI.DrawTexture(new Rect(cx - size / 2 - 2f, cy - size / 2 - 2f, size + 4f, size + 4f), Texture2D.whiteTexture);
        GUI.color = active ? Color.yellow : Color.white;
        GUI.DrawTexture(new Rect(cx - size / 2, cy - size / 2, size, size), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // prompts are bold with a dark outline, so they stay readable in front of busy backgrounds
        if (Focused != null && !hidePrompt)
            ShadowLabel(new Rect(0, cy + 30, Screen.width, 40), Focused.GetPrompt(), promptStyle);

        if (!string.IsNullOrEmpty(banner) && Time.time < bannerUntil)
            ShadowLabel(new Rect(0, 60, Screen.width, 50), banner, messageStyle);

        if (!string.IsNullOrEmpty(message) && Time.time < messageUntil) DrawInfoBox();
    }
}
