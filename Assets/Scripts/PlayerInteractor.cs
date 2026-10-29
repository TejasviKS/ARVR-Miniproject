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

    string message;
    float messageUntil;
    GUIStyle promptStyle, messageStyle;

    void Awake()
    {
        cam = GetComponentInChildren<Camera>(true);
        movement = GetComponent<PlayerController>();
        Debug.Log("[Scripts] PlayerInteractor v8 running");
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || cam == null) return;

        // EMERGENCY RESET: press R if anything gets stuck (instantly stand up, walking back on)
        if (kb.rKey.wasPressedThisFrame)
        {
            if (Locked != null) Locked.ForceRelease(this);
            Locked = null;
            if (movement != null) { movement.enabled = false; movement.enabled = true; }  // re-sync camera
            ShowMessage("View reset", 1f);
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

        if (Focused != null && kb.eKey.wasPressedThisFrame)
            Focused.Interact(this);
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

    // Show a message at the top of the screen, e.g. "Wrong code!"
    public void ShowMessage(string text, float seconds = 3f)
    {
        message = text;
        messageUntil = Time.time + seconds;
    }

    // Simple built-in UI (no Canvas setup needed)
    void OnGUI()
    {
        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
            promptStyle.normal.textColor = Color.white;
            messageStyle = new GUIStyle(promptStyle) { fontSize = 28, fontStyle = FontStyle.Bold };
        }

        float cx = Screen.width / 2f, cy = Screen.height / 2f;

        // Crosshair dot (bigger + yellow when looking at something usable)
        bool active = Focused != null && Locked == null;
        float size = active ? 10f : 5f;
        GUI.color = active ? Color.yellow : Color.white;
        GUI.DrawTexture(new Rect(cx - size / 2, cy - size / 2, size, size), Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (Focused != null)
            GUI.Label(new Rect(0, cy + 30, Screen.width, 40), Focused.GetPrompt(), promptStyle);

        if (!string.IsNullOrEmpty(message) && Time.time < messageUntil)
            GUI.Label(new Rect(0, 60, Screen.width, 50), message, messageStyle);
    }
}
