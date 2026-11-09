using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// VERSION 1
// The player's fighting (only used in the fight rooms: put it on the Player).
//
//   Left click        quick swing of the textbook
//   Hold left click   charge a HEAVY swing, let go to swing: slow, hits much harder, breaks his concentration
//   Hold right click  BLOCK: you take only a fifth of the damage
//   ...just before a hit lands (within "Parry Window"): PERFECT BLOCK = no damage, and it stuns him (chalk is thrown back)
//   Space             DODGE: a quick sidestep / backstep you cannot be hit during (Shift stays sprint)
//
// The textbook you see in your hand is built here from simple boxes (flat colours, so it also reads in the flat realm look).
[RequireComponent(typeof(Health))]
public class PlayerCombat : MonoBehaviour
{
    public enum HitResult { Hit, Blocked, Parried, Dodged }

    [Header("Attacks")]
    public float lightDamage = 8f;
    public float heavyDamage = 22f;
    [Tooltip("How far (metres) the book reaches")]
    public float reach = 2.4f;
    [Tooltip("Width of the swing in degrees")]
    public float arc = 100f;
    public float lightCooldown = 0.45f;
    public float heavyChargeTime = 0.55f;
    public float heavyCooldown = 0.9f;

    [Header("Defence")]
    [Tooltip("Press block this long (seconds) before a hit lands and it counts as a PERFECT block")]
    public float parryWindow = 0.3f;
    [Range(0f, 1f)] public float blockDamageFactor = 0.2f;
    public float dashDistance = 3.6f;
    public float dashTime = 0.16f;
    [Tooltip("How long (seconds) you cannot be hit after pressing dodge")]
    public float dashInvulnerable = 0.3f;
    public float dashCooldown = 0.8f;

    public Health Health { get; private set; }
    public bool Blocking { get; private set; }
    public bool Charging { get; private set; }
    public float Charge01 { get; private set; }
    public bool InputEnabled = true;
    public bool Invulnerable => Time.time < invulnUntil;
    public float LastDashAt { get; private set; } = -10f;      // when you last dodged (for "DODGED" when something flies past you)

    // Something that was heading for you flew past because you dodged: tell the player
    public void NoteDodged()
    {
        CombatHUD.Popup("DODGED", TutorialUI.Good);
        Defended?.Invoke(HitResult.Dodged);
    }

    // things the tutorial listens to
    public event Action<ProfessorAI, bool> HitLanded;      // who was hit, was it a heavy swing
    public event Action<HitResult> Defended;               // Blocked / Parried / Dodged
    public event Action Dashed;
    public event Action Swung;

    CharacterController cc;
    Camera cam;
    float blockStart, chargeStart, busyUntil, dashReadyAt, invulnUntil;
    bool cursorWasLocked;
    // input handling (see Update): held buttons and recent taps wait for the cooldown instead of being lost
    const float InputBuffer = 0.25f;                       // a tap this long before an action is allowed still counts
    bool leftArmed, rightArmed;
    float leftBufferUntil = -10f, dashBufferUntil = -10f;

    // ---- the book in your hand
    Transform vm;
    float swingStart = -10f, swingDuration = 0.28f;
    bool swingHeavy;
    float parryPop = -10f;
    static readonly Color Navy = new Color(0.12f, 0.18f, 0.42f);
    static readonly Color Paper = new Color(0.96f, 0.93f, 0.82f);

    void Awake()
    {
        Health = GetComponent<Health>();
        Health.Configure(75f, 74f);                          // attendance: 74% now, 75% is the goal (and the most you can have)
        cc = GetComponent<CharacterController>();
    }

    void Start()
    {
        Camera c = GetComponentInChildren<Camera>(true);
        cam = c;
        BuildBook();
    }

    void OnDestroy() { if (vm != null) Destroy(vm.gameObject); }

    // ================================================================ input
    void Update()
    {
        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;
        if (mouse == null || kb == null) return;

        if (!InputEnabled || Health.Dead)
        {
            Blocking = false; Charging = false; Charge01 = 0f;
            leftArmed = rightArmed = false; leftBufferUntil = dashBufferUntil = -10f;
            UpdateBook();
            return;
        }
        bool locked = cursorWasLocked;           // the click that grabs the mouse back must not also be an attack

        // ---- reading the buttons ----
        // A press is not thrown away just because the action is not allowed yet (still in its cooldown, busy charging, ...):
        //  - a button that is still HELD ("armed", from a press made while the mouse was captured) starts its action the moment it is allowed
        //  - a quick TAP made a moment before that (within InputBuffer seconds) also counts, for the attack and the dodge
        // So you never have to let go and press again: hold it, or press a little early, and it happens as soon as the cooldown ends.
        if (locked)
        {
            if (mouse.rightButton.wasPressedThisFrame) rightArmed = true;
            if (mouse.leftButton.wasPressedThisFrame) { leftArmed = true; leftBufferUntil = Time.time + InputBuffer; }
            if (kb.spaceKey.wasPressedThisFrame) dashBufferUntil = Time.time + InputBuffer;
        }
        if (!mouse.rightButton.isPressed) rightArmed = false;
        if (!mouse.leftButton.isPressed) leftArmed = false;
        bool canAct = Time.time >= busyUntil;

        // block (hold RIGHT button). The perfect-block window counts from the moment the block really starts.
        if (locked && !Blocking && !Charging && canAct && rightArmed)
        {
            Blocking = true;
            blockStart = Time.time;
        }
        if (Blocking && !mouse.rightButton.isPressed) Blocking = false;

        // attack: press to start charging, let go to swing (a quick tap = light swing, a long hold = heavy)
        if (!Blocking && locked)
        {
            if (!Charging && canAct && (leftArmed || Time.time <= leftBufferUntil))
            {
                Charging = true;
                chargeStart = Time.time;
                leftBufferUntil = -10f;                                   // (that tap is used up)
            }
        }
        if (Charging)
        {
            Charge01 = Mathf.Clamp01((Time.time - chargeStart) / heavyChargeTime);
            if (!mouse.leftButton.isPressed || Blocking)
            {
                Charging = false;
                if (!Blocking) Release(Charge01 >= 1f);
                Charge01 = 0f;
            }
        }

        // dodge
        if (locked && Time.time >= dashReadyAt && Time.time <= dashBufferUntil)
        {
            dashBufferUntil = -10f;
            StartCoroutine(Dash(kb));
        }

        UpdateBook();
    }

    void LateUpdate() { cursorWasLocked = Cursor.lockState == CursorLockMode.Locked; }

    // ================================================================ attacking
    void Release(bool heavy)
    {
        busyUntil = Time.time + (heavy ? heavyCooldown : lightCooldown);
        swingStart = Time.time;
        swingHeavy = heavy;
        swingDuration = heavy ? 0.4f : 0.26f;
        CombatSfx.Play(heavy ? CombatSfx.Clip.Heavy : CombatSfx.Clip.Swing, transform.position);
        Swung?.Invoke();
        StartCoroutine(SwingHit(heavy));
    }

    IEnumerator SwingHit(bool heavy)
    {
        yield return new WaitForSeconds(heavy ? 0.2f : 0.1f);      // the book has to get there first
        ProfessorAI target = FindTarget();
        if (target == null) yield break;
        target.TakeHit(heavy ? heavyDamage : lightDamage, heavy, transform.position);
        HitLanded?.Invoke(target, heavy);
    }

    ProfessorAI FindTarget()
    {
        if (cam == null) return null;
        ProfessorAI best = null;
        float bestDist = float.MaxValue;
        foreach (ProfessorAI p in ProfessorAI.All)
        {
            if (p == null || p.Health.Dead) continue;
            Vector3 to = p.Centre - cam.transform.position;
            float dist = to.magnitude;
            if (dist > reach + 0.4f) continue;                                    // (+0.4: he is a body, not a point)
            if (Vector3.Angle(cam.transform.forward, to) > arc * 0.5f) continue;   // not in front of you
            if (dist < bestDist) { bestDist = dist; best = p; }
        }
        return best;
    }

    // ================================================================ defending
    // Called by the professor's swing and his chalk. Returns what happened.
    public HitResult ReceiveHit(float damage, Vector3 from)
    {
        if (Health.Dead) return HitResult.Dodged;
        if (Invulnerable)
        {
            Defended?.Invoke(HitResult.Dodged);
            CombatHUD.Popup("DODGED", TutorialUI.Good);
            return HitResult.Dodged;
        }
        if (Blocking)
        {
            if (Time.time - blockStart <= parryWindow)
            {
                parryPop = Time.time;
                CombatSfx.Play(CombatSfx.Clip.Parry, transform.position);
                CombatHUD.Popup("PERFECT BLOCK!", TutorialUI.Accent);
                CombatHUD.Flash(new Color(1f, 1f, 1f), 0.35f);
                Defended?.Invoke(HitResult.Parried);
                return HitResult.Parried;
            }
            CombatSfx.Play(CombatSfx.Clip.Block, transform.position);
            CombatHUD.Popup("BLOCKED", Color.white);
            Health.Damage(damage * blockDamageFactor);
            Defended?.Invoke(HitResult.Blocked);
            return HitResult.Blocked;
        }
        Health.Damage(damage);
        CombatSfx.Play(CombatSfx.Clip.Hurt, transform.position);
        CombatHUD.Flash(new Color(1f, 0.1f, 0.1f), 0.55f);
        return HitResult.Hit;
    }

    IEnumerator Dash(Keyboard kb)
    {
        dashReadyAt = Time.time + dashCooldown;
        invulnUntil = Time.time + dashInvulnerable;
        LastDashAt = Time.time;
        float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        Vector3 dir = transform.right * x + transform.forward * z;
        if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;                  // no direction held: step back
        dir.y = 0f;
        dir.Normalize();
        CombatSfx.Play(CombatSfx.Clip.Dodge, transform.position);
        Dashed?.Invoke();
        for (float t = 0f; t < dashTime; t += Time.deltaTime)
        {
            if (cc != null && cc.enabled) cc.Move(dir * (dashDistance / dashTime) * Time.deltaTime);
            yield return null;
        }
    }

    // after a respawn
    public void ResetState()
    {
        Blocking = false; Charging = false; Charge01 = 0f;
        busyUntil = 0f; invulnUntil = 0f; dashReadyAt = 0f;
        leftArmed = rightArmed = false; leftBufferUntil = dashBufferUntil = -10f;
    }

    // ================================================================ the book in your hand
    void BuildBook()
    {
        if (cam == null) return;
        Shader shader = Shader.Find("Custom/ScreenGlow");       // flat colour, drawn after the flat realm pass so it stays visible
        if (shader == null) { Debug.LogWarning("[PlayerCombat] ScreenGlow shader not found: the textbook will be invisible.", this); return; }
        vm = new GameObject("Textbook").transform;
        vm.SetParent(cam.transform, false);
        Part(shader, "Pages", new Vector3(0f, 0f, 0f), new Vector3(0.19f, 0.26f, 0.06f), Paper);
        Part(shader, "FrontCover", new Vector3(0f, 0f, -0.04f), new Vector3(0.21f, 0.28f, 0.02f), Navy);
        Part(shader, "BackCover", new Vector3(0f, 0f, 0.04f), new Vector3(0.21f, 0.28f, 0.02f), Navy);
        Part(shader, "Spine", new Vector3(-0.1f, 0f, 0f), new Vector3(0.025f, 0.28f, 0.1f), Navy);
        vm.localPosition = IdlePos;
        vm.localRotation = Quaternion.Euler(IdleRot);
        vm.localScale = Vector3.one * 0.85f;
    }

    void Part(Shader shader, string name, Vector3 pos, Vector3 scale, Color colour)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(vm, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(shader) { color = colour };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static readonly Vector3 IdlePos = new Vector3(0.20f, -0.30f, 0.60f), IdleRot = new Vector3(15f, -25f, 8f);
    static readonly Vector3 ChargePos = new Vector3(0.26f, -0.14f, 0.38f), ChargeRot = new Vector3(-40f, -10f, 30f);
    static readonly Vector3 BlockPos = new Vector3(0.02f, -0.10f, 0.50f), BlockRot = new Vector3(-8f, 0f, 0f);

    void UpdateBook()
    {
        if (vm == null) return;
        Vector3 pos; Quaternion rot;
        float sinceSwing = Time.time - swingStart;

        if (sinceSwing < swingDuration)
        {
            // the swing: from the wound-up pose across the screen
            float k = Mathf.SmoothStep(0f, 1f, sinceSwing / swingDuration);
            if (swingHeavy)
            {
                pos = Vector3.Lerp(new Vector3(0.14f, 0.06f, 0.55f), new Vector3(0.02f, -0.34f, 0.78f), k);
                rot = Quaternion.Slerp(Quaternion.Euler(-70f, 0f, 0f), Quaternion.Euler(55f, 0f, 0f), k);
            }
            else
            {
                pos = Vector3.Lerp(new Vector3(0.40f, -0.14f, 0.38f), new Vector3(-0.18f, -0.30f, 0.66f), k);
                rot = Quaternion.Slerp(Quaternion.Euler(-30f, 15f, 40f), Quaternion.Euler(30f, -45f, -25f), k);
            }
            vm.localPosition = pos; vm.localRotation = rot;
            return;
        }

        if (Blocking)
        {
            pos = BlockPos; rot = Quaternion.Euler(BlockRot);
            float pop = Mathf.Clamp01(1f - (Time.time - parryPop) / 0.25f);              // a push forward on a perfect block
            pos += new Vector3(0f, 0f, 0.12f * pop);
        }
        else if (Charging)
        {
            pos = Vector3.Lerp(IdlePos, ChargePos, Charge01);
            rot = Quaternion.Slerp(Quaternion.Euler(IdleRot), Quaternion.Euler(ChargeRot), Charge01);
            if (Charge01 >= 1f) pos += new Vector3(Mathf.Sin(Time.time * 60f) * 0.004f, 0f, 0f);   // trembles when fully charged
        }
        else
        {
            pos = IdlePos + new Vector3(0f, Mathf.Sin(Time.time * 2f) * 0.006f, 0f);
            rot = Quaternion.Euler(IdleRot);
        }
        float follow = 20f * Time.deltaTime;
        vm.localPosition = Vector3.Lerp(vm.localPosition, pos, follow);
        vm.localRotation = Quaternion.Slerp(vm.localRotation, rot, follow);
    }
}
