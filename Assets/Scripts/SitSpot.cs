using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// VERSION 8
// Put this on an invisible trigger box placed ON a bench seat.
// Press E: the camera glides to a sitting position at this box, facing the chalkboard,
// tilted down to look under the desk. Press E again: glide back and walk again.
//
// Change the settings on the SitSpot PREFAB so all seats update together.
[RequireComponent(typeof(BoxCollider))]
public class SitSpot : Interactable
{
    [Header("Sitting position (relative to this box)")]
    [Tooltip("Where the eyes go, from the centre of this box. X = right, Y = up, Z = towards the board")]
    public Vector3 eyeOffset = new Vector3(0f, 0.05f, -0.15f);
    [Tooltip("How much to look down when sitting (degrees). 0 = straight ahead")]
    public float lookDownAngle = 20f;

    [Header("Facing")]
    [Tooltip("Seats face straight along the room (Z axis) towards this object. Empty = use the box's blue arrow.")]
    public string faceTowards = "Chalkboard";

    [Header("Feel")]
    public float moveTime = 0.6f;
    public float lookYawLimit = 70f;
    public float lookPitchLimit = 60f;
    public float mouseSensitivity = 0.1f;

    bool seated, moving;
    PlayerInteractor player;
    Transform camT;
    float yawOffset, pitchOffset;
    Transform faceTarget;

    void Reset() { promptText = "sit"; }

    void Awake()
    {
        // A seat box must never block the player: force it to be a trigger
        GetComponent<BoxCollider>().isTrigger = true;
        if (!string.IsNullOrEmpty(faceTowards))
        {
            GameObject go = GameObject.Find(faceTowards);
            if (go != null) faceTarget = go.transform;
            else Debug.LogWarning("SitSpot: no object called \"" + faceTowards + "\" found, using the blue arrow instead.", this);
        }
    }

    // ---- Where the eyes go when sitting ----
    Quaternion FacingRotation()
    {
        Vector3 fwd = transform.forward;
        Transform target = faceTarget;
        if (target == null && !Application.isPlaying && !string.IsNullOrEmpty(faceTowards))
        {
            GameObject go = GameObject.Find(faceTowards);   // editor preview only
            if (go != null) target = go.transform;
        }
        if (target != null)
        {
            // The rows of benches run along the Z axis and the board is at one end,
            // so every seat faces straight along Z, towards the board's side of the room.
            // (Only the board's Z matters: side seats near the front no longer turn sideways.)
            Vector3 c = target.TryGetComponent(out Renderer r) ? r.bounds.center : target.position;
            float dz = c.z - transform.position.z;
            fwd = new Vector3(0f, 0f, dz >= 0f ? 1f : -1f);
        }
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        return Quaternion.LookRotation(fwd.normalized, Vector3.up);
    }

    Vector3 SitPosition() => transform.position + FacingRotation() * eyeOffset;
    Quaternion SitRotation() => FacingRotation() * Quaternion.Euler(lookDownAngle, 0f, 0f);

    public override string GetPrompt() =>
        player != null && player.Locked == this ? "Press E to stand up" : "Press E to sit";
    public override bool CanInteract => !moving;

    public override void Interact(PlayerInteractor p)
    {
        if (moving) return;
        if (!seated) StartCoroutine(SitDown(p));
        else StartCoroutine(StandUp());
    }

    // Emergency: stop any glide and stand up instantly
    public override void ForceRelease(PlayerInteractor p)
    {
        StopAllCoroutines();
        seated = false;
        moving = false;
        p.Unlock();
    }

    // If this seat gets disabled mid-glide, don't leave the player stuck
    void OnDisable()
    {
        if (player != null && player.Locked == this) ForceRelease(player);
        seated = moving = false;
    }

    IEnumerator SitDown(PlayerInteractor p)
    {
        player = p;
        camT = p.cam.transform;
        yawOffset = pitchOffset = 0f;
        p.Lock(this);                              // walking OFF, E now means "stand up"
        moving = true;
        yield return MoveCamera(SitPosition(), SitRotation());
        moving = false;
        seated = true;
    }

    IEnumerator StandUp()
    {
        seated = false;
        moving = true;
        float eye = player.movement != null ? player.movement.eyeHeight : 1.6f;
        Vector3 headPos = player.transform.TransformPoint(new Vector3(0f, eye, 0f));
        yield return MoveCamera(headPos, player.transform.rotation);
        moving = false;
        player.Unlock();                           // walking ON (PlayerController re-syncs the camera)
    }

    // Smoothly glide the camera to a target position/rotation
    IEnumerator MoveCamera(Vector3 toPos, Quaternion toRot)
    {
        Vector3 fromPos = camT.position;
        Quaternion fromRot = camT.rotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / moveTime)
        {
            float s = Mathf.SmoothStep(0f, 1f, t);
            camT.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, s), Quaternion.Slerp(fromRot, toRot, s));
            yield return null;
        }
        camT.SetPositionAndRotation(toPos, toRot);
    }

    void Update()
    {
        if (!seated || moving) return;
        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 d = mouse.delta.ReadValue() * mouseSensitivity;
            yawOffset = Mathf.Clamp(yawOffset + d.x, -lookYawLimit, lookYawLimit);
            pitchOffset = Mathf.Clamp(pitchOffset - d.y, -lookPitchLimit, lookPitchLimit);
        }
        // Recalculated every frame, so you can tweak Eye Offset / Look Down Angle live in Play mode
        camT.SetPositionAndRotation(SitPosition(), SitRotation() * Quaternion.Euler(pitchOffset, yawOffset, 0f));
    }

#if UNITY_EDITOR
    // Scene view markers, drawn through everything:
    //   yellow dot + line = eyes and look direction when sitting
    //   cyan line         = which way the seat faces (should point at the board)
    void OnDrawGizmos()
    {
        Vector3 p = SitPosition();
        var old = UnityEditor.Handles.zTest;
        UnityEditor.Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        UnityEditor.Handles.color = Color.yellow;
        float size = UnityEditor.HandleUtility.GetHandleSize(p) * 0.08f;
        UnityEditor.Handles.SphereHandleCap(0, p, Quaternion.identity, size, EventType.Repaint);
        UnityEditor.Handles.DrawLine(p, p + SitRotation() * Vector3.forward * 0.6f, 3f);

        UnityEditor.Handles.color = Color.cyan;
        UnityEditor.Handles.DrawLine(transform.position, transform.position + FacingRotation() * Vector3.forward * 0.8f, 3f);

        UnityEditor.Handles.zTest = old;
    }
#endif
}
