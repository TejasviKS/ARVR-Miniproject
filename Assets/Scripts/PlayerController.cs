using UnityEngine;
using UnityEngine.InputSystem;

// VERSION 8
// First-person player: WASD to walk, mouse to look, Shift to run, Space to jump.
// Put this on a "Player" object that has a CharacterController.
// The Main Camera must be a child of the Player.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3f;          // metres per second
    public float runSpeed = 6f;
    public float jumpHeight = 0f;         // 0 = no jumping (stops climbing onto benches)
    public float gravity = -9.81f;

    [Header("Looking")]
    public float mouseSensitivity = 0.1f;
    [Tooltip("Height of the eyes above the player's feet (metres)")]
    public float eyeHeight = 1.6f;

    CharacterController controller;
    Transform cam;                        // the camera inside the Player (found automatically)
    float verticalVelocity;               // up/down speed (gravity + jumping)
    float pitch;                          // looking up/down
    float yaw;                            // facing left/right

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        Camera c = GetComponentInChildren<Camera>(true);
        if (c != null) cam = c.transform;
        else Debug.LogError("PlayerController: put the Main Camera INSIDE the Player object!");
        Debug.Log("[Scripts] PlayerController v8 running");
    }

    void Start() => LockCursor(true);

    // Runs at the start AND every time walking is switched back on (e.g. after standing up from a seat).
    // Re-syncs body + camera so the view and WASD always line up.
    void OnEnable()
    {
        yaw = transform.eulerAngles.y;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);   // body perfectly upright
        pitch = 0f;                                           // look straight ahead
        SnapCamera();
    }

    void SnapCamera()
    {
        if (cam == null) return;
        cam.localPosition = new Vector3(0f, eyeHeight, 0f);   // camera exactly at the head
        cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);  // only up/down tilt, never sideways
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        // Esc frees the mouse, clicking the game window grabs it again
        if (kb.escapeKey.wasPressedThisFrame) LockCursor(false);
        if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);

        // ---- Look around ----
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -85f, 85f);
        }
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);   // turn body (WASD follows this)
        SnapCamera();                                         // camera always follows the body

        // ---- Walk ----
        float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        Vector3 move = transform.right * x + transform.forward * z;
        if (move.sqrMagnitude > 1f) move.Normalize();         // no faster diagonals
        float speed = kb.leftShiftKey.isPressed ? runSpeed : walkSpeed;

        // ---- Gravity & jump ----
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;  // stick to floor
        if (jumpHeight > 0f && controller.isGrounded && kb.spaceKey.wasPressedThisFrame)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
