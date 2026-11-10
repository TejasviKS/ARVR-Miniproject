using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// VERSION 3
// The director of the final boss fight: Prof. Claude in the Classroom_boss room.
//
// Prof. Claude IS a ProfessorAI (the same hologram body, pointer swing, chalk throw, perfect-block stun and health bar as the
// tutorial's Prof. GPT). This script sits next to him and adds what GPT never had:
//   - PHASES, chosen by his health: Haiku (100-66%), Sonnet (66-33%), Opus (33-0%), and the secret Fable.
//   - extra attacks that unlock phase by phase,
//   - the intro, the "you were beaten, try again" flow and the "Mission Passed" ending,
//   - difficulty (BossDifficulty.cs): every time and every damage here is multiplied/divided by it.
//
// THE LETTER (a trick): when he drops to 10% in Opus, a player who picked up the Letter of Recommendation in the classroom gets the prompt
// "[E] Present Letter of Recommendation". Using it is a FAKE-OUT: he slouches, his bar drains to 0 and the fight music fades out into
// silence... then he straightens up (the intense Fable music snaps on), transforms into his full form and heals to 20%: the secret fourth phase, FABLE.
// Ignoring the prompt just continues the fight; at 0% he is really beaten.
//
// BUILT SO FAR (Phases Built = 4):
//   HAIKU   pointer swing, chalk throw, HAIKU LUNGE (a dash along three floor marks)
//   SONNET  + CHALK VOLLEY (three chalks in a fan) and FOOTNOTE BLINK (he vanishes and strikes from behind you)
//   OPUS    + RED PEN MARKS (red X's on the floor that burst after a moment) and a faster tempo: shorter telegraphs, attacks chained close together
//   FABLE   + ZERO ATTENDANCE (a long wind-up, then a room-wide shockwave; parry or dash it and he is stunned) and the fastest tempo. Only reached through the Letter.
// "Phases Built" says how many phases exist. When his health reaches the start of a phase that is not built yet, the fight simply
// ends with Mission Passed, so the game can be played from start to finish while the next phases are added one at a time.
[DefaultExecutionOrder(-10)]            // runs before the boss's own decisions each frame, so a ready special attack wins over his ordinary swing
public class BossFight : MonoBehaviour
{
    public ProfessorAI boss;
    public PlayerCombat player;

    [Tooltip("Health on the Easy difficulty. The difficulty multiplies it (BossDifficulty.cs).")]
    public float baseHealth = 600f;
    [Tooltip("How many phases exist so far: 1 = Haiku only, 2 = + Sonnet. Raise it as Opus / Fable are added.")]
    public int phasesBuilt = 4;

    // ---------------------------------------------------------------- the phases
    // (name, the health fraction where it STARTS, how much faster than normal he is in it)
    struct PhaseDef { public string name; public float startsAt; public float tempo; public string line; }
    static readonly PhaseDef[] Phases =
    {
        new PhaseDef { name = "Haiku",  startsAt = 1.00f, tempo = 1.0f, line = "" },                  // (the intro speech has its own four lines: see Intro())
        new PhaseDef { name = "Sonnet", startsAt = 0.66f, tempo = 1.0f, line = "You should obey your professors. Come back next class!" },
        new PhaseDef { name = "Opus",   startsAt = 0.33f, tempo = 1.2f, line = "Alright! Now, you've done it." },
        new PhaseDef { name = "Fable",  startsAt = 0.00f, tempo = 1.3f, line = "And every fable has a twist." },
    };
    const int Haiku = 0, Sonnet = 1, Opus = 2, Fable = 3;
    // Each phase is redder than the one before: the small tag under his health bar, and the big word that rises up the screen when the phase starts.
    // (Haiku keeps the original look; Fable is a deep red.)
    static readonly Color[] TagColours =
    {
        new Color(1.00f, 0.775f, 0.65f),      // Haiku: as it always was
        new Color(1.00f, 0.60f, 0.48f),       // Sonnet
        new Color(1.00f, 0.40f, 0.33f),       // Opus
        new Color(0.85f, 0.10f, 0.10f),       // Fable
    };
    static readonly Color[] PopupColours =
    {
        new Color(1.00f, 0.70f, 0.40f),       // Haiku (no popup of its own)
        new Color(1.00f, 0.60f, 0.38f),       // Sonnet
        new Color(1.00f, 0.38f, 0.28f),       // Opus
        new Color(0.88f, 0.08f, 0.08f),       // Fable
    };
    const float LetterAt = 0.10f, LetterOfferTime = 7f, FableHealth = 0.20f;
    const string LetterItem = "letter_of_recommendation";
    const float ZeroWindup = 2.5f, ZeroDamage = 35f, ZeroCooldown = 24f, ZeroStun = 5f;

    // ---------------------------------------------------------------- base numbers (Easy, normal speed)
    const float MeleeDamage = 12f, MeleeTelegraph = 0.8f, MeleeRecover = 1.0f, MeleeCooldown = 1.3f;
    const float ChalkDamage = 7f, ChalkTelegraph = 0.95f, ChalkRecover = 1.0f, ChalkCooldown = 3.0f, ChalkSpeed = 7f;
    const float LungeDamage = 14f, LungeMarkTime = 0.38f, LungeSpeed = 17f, LungeRecover = 1.25f, LungeCooldown = 7f;
    const float LungeMinDist = 3.6f, LungeMaxDist = 10f;
    const float VolleyDamage = 6f, VolleyTelegraph = 1.05f, VolleyRecover = 1.2f, VolleyCooldown = 8f, VolleyFan = 15f;
    const float BlinkVanish = 0.35f, BlinkRing = 0.6f, BlinkSwingTelegraph = 0.55f, BlinkCooldown = 10f, BlinkBehind = 2.0f;
    const float GapBetweenSpecials = 1.4f, OpusGapBetweenSpecials = 0.5f;       // Opus chains his attacks almost back to back
    const float PenCastTime = 0.6f, PenFuse = 1.2f, PenRadius = 1.5f, PenDamage = 14f, PenRecover = 1.0f, PenCooldown = 11f;

    [Tooltip("How far his CENTRE stays from every wall, so his arms and pauldrons can never reach into one when he turns")]
    public float wallClearance = 1.35f;

    // the room as he can use it: measured once from the walls around where he starts
    float minX = -1e6f, maxX = 1e6f, minZ = -1e6f, maxZ = 1e6f;
    float roomMinX = -1e6f, roomMaxX = 1e6f, roomMinZ = -1e6f, roomMaxZ = 1e6f;       // the walls themselves (where the red pen marks may go)

    int phase;
    bool fightOn, busy, beaten, finished;
    float nextLungeAt, nextVolleyAt, nextBlinkAt, nextPenAt, nextZeroAt, nextSpecialAt;
    bool offered, offerActive, letterPressed, playerLocked;
    float offerEndsAt;
    Coroutine cutscene;
    RealmStyle realm;
    Color realmFill, realmLine, realmVoid;
    bool savedPlayerInvulnerable;
    Coroutine penRoutine;
    GameObject penMarks;
    Coroutine attack;
    GameObject marks;
    float Tempo => Phases[Mathf.Clamp(phase, 0, Phases.Length - 1)].tempo * BossDifficulty.Tempo;

    void Start()
    {
        if (boss == null) boss = FindAnyObjectByType<ProfessorAI>();
        if (player == null) player = FindAnyObjectByType<PlayerCombat>();
        if (boss == null || player == null) { Debug.LogError("[BossFight] Needs a ProfessorAI (the boss) and a PlayerCombat (the player) in the scene.", this); enabled = false; return; }

        boss.displayName = "Prof. Claude";
        boss.hideWhenDead = false;                                    // (he stays lying there while the room's colours come back)
        boss.barScreenFraction = 0.7f;                                // a very wide boss bar: 70% of the screen, at any resolution
        boss.barNotches = NotchesBuilt();
        boss.Health.Configure(baseHealth * BossDifficulty.HpMultiplier, 0f);
        boss.Health.Damaged += OnBossDamaged;
        boss.Died += OnBossDied;
        boss.brainPaused = false;
        boss.aggressive = false;
        ApplyTuning();
        player.Health.Died += OnPlayerBeaten;
        MeasureArena();

        // for the Letter fake-out: the room's colours (they drain to grey and come back)
        realm = FindAnyObjectByType<RealmStyle>();
        if (realm != null) { realmFill = realm.fillColor; realmLine = realm.lineColor; realmVoid = realm.voidColor; }

        StartCoroutine(Intro());
    }

    // Looks at the four walls around him and remembers how far he may go (wall minus the clearance)
    void MeasureArena()
    {
        Vector3 from = boss.transform.position + Vector3.up * 1.5f;
        float? Wall(Vector3 dir)
        {
            float best = float.MaxValue;
            foreach (RaycastHit h in Physics.RaycastAll(from, dir, 40f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.GetComponentInParent<PlayerCombat>() != null || h.collider.GetComponentInParent<ProfessorAI>() != null) continue;
                if (h.distance < best) best = h.distance;
            }
            return best < float.MaxValue ? best : (float?)null;
        }
        float? l = Wall(Vector3.left), r = Wall(Vector3.right), b = Wall(Vector3.back), f = Wall(Vector3.forward);
        if (l.HasValue) { roomMinX = from.x - l.Value; minX = roomMinX + wallClearance; }
        if (r.HasValue) { roomMaxX = from.x + r.Value; maxX = roomMaxX - wallClearance; }
        if (b.HasValue) { roomMinZ = from.z - b.Value; minZ = roomMinZ + wallClearance; }
        if (f.HasValue) { roomMaxZ = from.z + f.Value; maxZ = roomMaxZ - wallClearance; }
        Debug.Log("[BossFight] Arena for the boss: x " + minX.ToString("0.0") + ".." + maxX.ToString("0.0") + ", z " + minZ.ToString("0.0") + ".." + maxZ.ToString("0.0"));
    }

    bool InsideArena(Vector3 p) => p.x >= minX && p.x <= maxX && p.z >= minZ && p.z <= maxZ;

    // after everything else has moved him this frame: never closer to a wall than the clearance
    void LateUpdate()
    {
        if (boss == null || boss.Current == ProfessorAI.State.Dead) return;
        Vector3 p = boss.transform.position;
        if (p.y < -5f) return;                                             // (mid-blink, waiting under the floor)
        float x = Mathf.Clamp(p.x, minX, maxX), z = Mathf.Clamp(p.z, minZ, maxZ);
        if (!Mathf.Approximately(x, p.x) || !Mathf.Approximately(z, p.z)) boss.MoveBy(new Vector3(x - p.x, 0f, z - p.z));
    }

    void OnDestroy()
    {
        if (boss != null) { boss.Health.Damaged -= OnBossDamaged; boss.Died -= OnBossDied; }
        if (player != null && player.Health != null) player.Health.Died -= OnPlayerBeaten;
        if (marks != null) Destroy(marks);
    }

    float[] NotchesBuilt()
    {
        // only the phase boundaries are marked. The Letter moment at 10% is a SURPRISE: nothing on the bar hints at it.
        return new[] { 0.66f, 0.33f };
    }

    // ---------------------------------------------------------------- numbers for the current phase and difficulty
    void ApplyTuning()
    {
        float t = Tempo, d = BossDifficulty.DamageFactor;
        boss.meleeDamage = MeleeDamage * d;
        boss.meleeTelegraph = Mathf.Max(0.45f, MeleeTelegraph / t);
        boss.meleeRecover = MeleeRecover / t;
        boss.meleeCooldown = MeleeCooldown / t;
        boss.chalkDamage = ChalkDamage * d;
        boss.chalkTelegraph = Mathf.Max(0.55f, ChalkTelegraph / t);
        boss.chalkRecover = ChalkRecover / t;
        boss.chalkCooldown = ChalkCooldown / t;
        boss.chalkSpeed = ChalkSpeed;
        boss.moveSpeed = 2.0f * Mathf.Min(t, 1.3f);
        boss.stunTime = 3f;
        boss.uninterruptible = false;
        boss.barTag = Phases[phase].name;
        boss.barTagColour = TagColours[Mathf.Clamp(phase, 0, TagColours.Length - 1)];
    }

    // ---------------------------------------------------------------- intro
    IEnumerator Intro()
    {
        // You arrive with no HUD (your attendance bar is hidden) and cannot walk or attack until he has finished speaking.
        boss.Health.invulnerable = true;
        LockForIntro(true);
        yield return new WaitForSeconds(1.2f);
        // each line waits for its own length (typing + reading time, see ProfessorAI.LineSeconds) plus a short beat before the next
        string[] speech =
        {
            "You're late to the class.",
            "You know who's a traitor to the country?",
            "You!",
            "You all are actual traitors to the country if you come late.",
        };
        for (int i = 0; i < speech.Length; i++)
        {
            boss.Say(speech[i]);
            yield return new WaitForSeconds(ProfessorAI.LineSeconds(speech[i]) + (i < speech.Length - 1 ? 0.2f : 0.15f));
        }
        // the fight starts: your attendance bar fades in quickly and you can move
        boss.Health.invulnerable = false;
        LockForIntro(false);
        BeginFighting(0.5f);    }

    void LockForIntro(bool locked)
    {
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null) { pc.movementLocked = locked; pc.lookLocked = locked; }                  // no walking and no looking around while he speaks
        player.InputEnabled = !locked;                                   // (no swinging at him while he is talking)
        CombatHUD.SetPlayerBarHidden(locked, instant: locked);           // hidden at once; shown again it fades in
    }

    void BeginFighting(float grace)
    {
        boss.aggressive = true;
        fightOn = true;
        busy = false;
        nextLungeAt = Time.time + grace + 3.5f;
        nextVolleyAt = Time.time + grace + 5f;
        nextBlinkAt = Time.time + grace + 8f;
        nextPenAt = Time.time + grace + 4f;
        nextZeroAt = Time.time + grace + 2.5f;                            // (only used in Fable: his big opening move)
        nextSpecialAt = Time.time + grace;
        boss.ResetTimers(grace + 1f, grace + 2f);
    }

    // ---------------------------------------------------------------- every frame: when he is free, pick one of the attacks that is ready
    readonly List<int> ready = new List<int>();

    void Update()
    {
        if (PauseMenu.IsPaused) return;
        // the Letter prompt: E while it is on screen
        if (offerActive && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) letterPressed = true;

        if (!fightOn || finished || busy || boss.Current == ProfessorAI.State.Dead || player.Health.Dead) return;
        if (boss.Current != ProfessorAI.State.Idle && boss.Current != ProfessorAI.State.Chase) return;
        if (Time.time < nextSpecialAt) return;

        Vector3 to = player.transform.position - boss.transform.position; to.y = 0f;
        float d = to.magnitude;

        // his signature move, in Fable only: it is always the first thing he does when it is ready
        if (phase >= Fable && Time.time >= nextZeroAt) { attack = StartCoroutine(ZeroAttendance()); return; }

        ready.Clear();
        if (Time.time >= nextLungeAt && d >= LungeMinDist && d <= LungeMaxDist) ready.Add(0);
        if (phase >= Sonnet && Time.time >= nextVolleyAt && d >= 2.5f && d <= 11f) ready.Add(1);
        if (phase >= Sonnet && Time.time >= nextBlinkAt && d >= 1.2f && d <= 9f) ready.Add(2);
        if (phase >= Opus && Time.time >= nextPenAt) ready.Add(3);
        if (ready.Count == 0) return;

        switch (ready[Random.Range(0, ready.Count)])
        {
            case 0: attack = StartCoroutine(HaikuLunge()); break;
            case 1: attack = StartCoroutine(ChalkVolley()); break;
            case 2: attack = StartCoroutine(FootnoteBlink()); break;
            case 3: attack = StartCoroutine(RedPens()); break;
        }
    }

    // ---- shared by every attack: start (take over from his own brain) and finish (give it back)
    void StartAttack() { busy = true; boss.brainPaused = true; }

    void EndAttack()
    {
        if (marks != null) { Destroy(marks); marks = null; }
        boss.SetBodyVisible(true);
        boss.uninterruptible = false;
        boss.brainPaused = false;
        busy = false;
        attack = null;
        nextSpecialAt = Time.time + (phase >= Opus ? OpusGapBetweenSpecials : GapBetweenSpecials) / Tempo;
        if (boss.Current == ProfessorAI.State.TelegraphLunge || boss.Current == ProfessorAI.State.Lunge || boss.Current == ProfessorAI.State.TelegraphVolley)
            boss.SetState(ProfessorAI.State.Idle);
    }

    // everything an attack in progress leaves behind (a phase change, the player being beaten, the boss dying)
    void Interrupt(bool keepCutscene = false)
    {
        if (attack != null) { StopCoroutine(attack); attack = null; }
        ClearPens();
        if (!keepCutscene)
        {
            // the Letter offer or the fake-out was going on: put everything back
            if (cutscene != null) { StopCoroutine(cutscene); cutscene = null; }
            offerActive = false;
            RestoreRealm();
            UnlockPlayer();
            boss.SetSlouch(false);
            boss.stunTime = 3f;
            if (zeroFloor != null) { Destroy(zeroFloor); zeroFloor = null; }
        }
        if (boss.Current == ProfessorAI.State.Dead) { if (marks != null) { Destroy(marks); marks = null; } boss.SetBodyVisible(true); return; }
        if (boss.transform.position.y < -5f)                                // he was mid-blink, waiting under the floor: bring him back
        {
            boss.Teleport(new Vector3(boss.transform.position.x, 0.06f, boss.transform.position.z));
            boss.SetState(ProfessorAI.State.Idle);
        }
        EndAttack();
    }

    // ================================================================ HAIKU LUNGE
    // Three marks light up along the floor in a 5-7-5 rhythm (long, longer, long), then he dashes along that line.
    // The line is fixed when it appears: step to the side to dodge it, dash through it, or parry him as he arrives.
    IEnumerator HaikuLunge()
    {
        StartAttack();
        float t = Tempo;

        Vector3 start = boss.transform.position;
        Vector3 dir = player.transform.position - start; dir.y = 0f;
        float dist = dir.magnitude;
        dir.Normalize();
        float length = Mathf.Clamp(dist + 2.5f, 6f, 11f);

        boss.transform.rotation = Quaternion.LookRotation(dir);
        boss.SetState(ProfessorAI.State.TelegraphLunge);
        BuildMarks(start, dir, length);

        // 5 - 7 - 5: the middle beat is a little longer
        float[] beats = { LungeMarkTime, LungeMarkTime * 1.4f, LungeMarkTime };
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForSeconds(beats[i] / t);
            if (boss.Current == ProfessorAI.State.Dead || boss.Current == ProfessorAI.State.Stunned) { nextLungeAt = Time.time + LungeCooldown / t; EndAttack(); yield break; }
            LightMark(i);
            CombatSfx.Play(CombatSfx.Clip.Telegraph, boss.transform.position + dir * length * (0.2f + 0.3f * i), 0.9f);
        }
        yield return new WaitForSeconds(0.18f / t);

        // the dash
        boss.SetState(ProfessorAI.State.Lunge);
        CombatSfx.Play(CombatSfx.Clip.Swing, boss.transform.position, 1f);
        float travelled = 0f; int stuck = 0; bool hit = false; bool parried = false;
        while (travelled < length)
        {
            if (boss.Current == ProfessorAI.State.Dead) { EndAttack(); yield break; }
            float step = LungeSpeed * Mathf.Min(1.2f, t) * Time.deltaTime;
            Vector3 moved = boss.MoveBy(dir * step + Vector3.down * 0.05f);
            moved.y = 0f;
            travelled += moved.magnitude;
            stuck = moved.magnitude < step * 0.3f ? stuck + 1 : 0;
            if (stuck >= 3) break;                                  // ran into a wall

            if (!hit && !player.Health.Dead)
            {
                Vector3 toP = player.transform.position - boss.transform.position; toP.y = 0f;
                float along = Vector3.Dot(toP, dir);
                float across = Mathf.Abs(Vector3.Cross(dir, toP).y);
                if (along > -0.4f && along < 1.3f && across < 0.85f)
                {
                    PlayerCombat.HitResult r = player.ReceiveHit(LungeDamage * BossDifficulty.DamageFactor, boss.transform.position);
                    if (r == PlayerCombat.HitResult.Parried) { parried = true; break; }
                    if (r != PlayerCombat.HitResult.Dodged) hit = true;         // (a dodge lets him fly through you; if the dodge ends while he is still on you, it can still hit)
                }
            }
            yield return null;
        }

        nextLungeAt = Time.time + LungeCooldown / t;
        if (parried) { boss.StunNow(); EndAttack(); yield break; }
        boss.SetState(ProfessorAI.State.Recover, LungeRecover / t);
        EndAttack();
    }

    // ================================================================ CHALK VOLLEY (Sonnet)
    // He raises his chalk hand (a little longer than the single throw), then throws THREE pieces in a fan. Dash through the gap
    // between two of them, block (the damage is small) or parry one of them back at him. A heavy hit during the wind-up breaks it.
    IEnumerator ChalkVolley()
    {
        StartAttack();
        float t = Tempo;
        boss.SetState(ProfessorAI.State.TelegraphVolley);
        yield return new WaitForSeconds(Mathf.Max(0.6f, VolleyTelegraph / t));

        if (boss.Current != ProfessorAI.State.TelegraphVolley)               // a heavy hit broke it, or he was stunned or beaten
        {
            nextVolleyAt = Time.time + VolleyCooldown / t * 0.5f;
            EndAttack();
            yield break;
        }

        Vector3 hand = boss.transform.position + Vector3.up * 1.3f + boss.transform.forward * 0.4f;
        Vector3 aim = (player.transform.position + Vector3.up * 1.2f) - hand;
        foreach (float angle in new[] { -VolleyFan, 0f, VolleyFan })
        {
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * aim;
            Chalk.Spawn(hand, dir, ChalkSpeed, VolleyDamage * BossDifficulty.DamageFactor, boss, player);
        }
        CombatSfx.Play(CombatSfx.Clip.Throw, boss.transform.position, 1f);
        Debug.Log("[BossFight] Chalk volley thrown (" + Chalk.InFlight + " pieces in the air)");

        nextVolleyAt = Time.time + VolleyCooldown / t;
        boss.SetState(ProfessorAI.State.Recover, VolleyRecover / t);
        EndAttack();
    }

    // ================================================================ FOOTNOTE BLINK (Sonnet)
    // He vanishes with a chime. A glowing triangle on the floor behind you shows where he will appear (0.6 s warning). He appears
    // there facing you and swings (a short wind-up). Move away, or turn and parry. A heavy hit does NOT break this wind-up.
    IEnumerator FootnoteBlink()
    {
        float t = Tempo;
        Vector3 dest;
        if (!FindBlinkSpot(out dest)) { nextBlinkAt = Time.time + 2f; yield break; }          // no room behind you right now: try again soon

        StartAttack();
        boss.uninterruptible = true;
        // gone: he waits far below the floor so nothing bumps into him
        CombatSfx.Play(CombatSfx.Clip.Telegraph, boss.transform.position, 1f);
        boss.SetState(ProfessorAI.State.Recover, 99f);
        boss.SetBodyVisible(false);
        boss.Teleport(new Vector3(dest.x, -20f, dest.z));
        yield return new WaitForSeconds(BlinkVanish / t);

        // the warning on the floor, behind you
        Vector3 faceDir = player.transform.position - dest; faceDir.y = 0f;
        BuildBlinkRing(dest, faceDir);
        float ringTime = BlinkRing / t, started = Time.time;
        Renderer ring = marks != null ? marks.GetComponentInChildren<Renderer>() : null;
        while (Time.time - started < ringTime)
        {
            if (ring != null) ring.material.color = Color.Lerp(Dim, Lit, ((Time.time - started) / ringTime));
            yield return null;
        }

        // he arrives and strikes
        if (marks != null) { Destroy(marks); marks = null; }
        boss.Teleport(dest);
        boss.transform.rotation = Quaternion.LookRotation(faceDir.sqrMagnitude > 0.01f ? faceDir : Vector3.forward);
        boss.SetBodyVisible(true);
        CombatSfx.Play(CombatSfx.Clip.Stun, dest, 0.8f);
        float savedTelegraph = boss.meleeTelegraph;
        boss.meleeTelegraph = Mathf.Max(0.4f, BlinkSwingTelegraph / t);
        boss.SetState(ProfessorAI.State.TelegraphMelee);
        Debug.Log("[BossFight] Footnote blink: he appeared at " + dest.ToString("0.0") + ", " + Vector3.Distance(dest, player.transform.position).ToString("0.0") + " m from you");
        yield return null;
        // wait until the swing is over (his own brain does the swing, and stuns him if you parry it)
        float guard = Time.time + 3f;
        while ((boss.Current == ProfessorAI.State.TelegraphMelee || boss.Current == ProfessorAI.State.Swing) && Time.time < guard) yield return null;
        boss.meleeTelegraph = savedTelegraph;
        Debug.Log("[BossFight] Footnote blink finished, state " + boss.Current);

        nextBlinkAt = Time.time + BlinkCooldown / t;
        EndAttack();
    }

    // ================================================================ RED PEN MARKS (Opus)
    // He raises his hand and marks the floor around you with red X's. Each one shows a circle and glows brighter for 1.2 s, then BURSTS
    // (14 damage inside the circle). The first mark lands where you are heading, so standing still or running straight gets you hit:
    // keep moving between them, or dash through a burst. He is open to attack while the marks fuse (they do not need him).
    IEnumerator RedPens()
    {
        StartAttack();
        float t = Tempo;
        boss.SetState(ProfessorAI.State.TelegraphVolley);                 // the arm goes up, like a teacher about to write on the board
        yield return new WaitForSeconds(Mathf.Max(0.4f, PenCastTime / t));

        if (boss.Current != ProfessorAI.State.TelegraphVolley)           // a heavy hit broke it, or he was stunned
        {
            nextPenAt = Time.time + 3f;
            EndAttack();
            yield break;
        }

        List<Vector3> spots = PickPenSpots(BossDifficulty.PenMarks);
        penRoutine = StartCoroutine(PenBurst(spots, Mathf.Max(0.8f, PenFuse / t)));
        Debug.Log("[BossFight] Red pen marks: " + spots.Count);

        nextPenAt = Time.time + PenCooldown / t;
        boss.SetState(ProfessorAI.State.Recover, PenRecover / t);
        EndAttack();
    }

    // The first mark is where the player is heading in a moment; the rest are spread around, never on top of each other
    List<Vector3> PickPenSpots(int count)
    {
        var spots = new List<Vector3>();
        CharacterController pcc = player.GetComponent<CharacterController>();
        Vector3 vel = pcc != null ? pcc.velocity : Vector3.zero; vel.y = 0f;
        Vector3 first = player.transform.position + vel * 0.7f;
        spots.Add(InRoom(first));
        for (int tries = 0; spots.Count < count && tries < 40; tries++)
        {
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(1.5f, 4.5f);
            Vector3 p = InRoom(player.transform.position + new Vector3(r.x, 0f, r.y));
            bool apart = true;
            foreach (Vector3 s in spots) if ((s - p).magnitude < PenRadius * 1.5f) apart = false;
            if (apart) spots.Add(p);
        }
        return spots;
    }

    Vector3 InRoom(Vector3 p)
    {
        p.x = Mathf.Clamp(p.x, roomMinX + 0.8f, roomMaxX - 0.8f);
        p.z = Mathf.Clamp(p.z, roomMinZ + 0.8f, roomMaxZ - 0.8f);
        p.y = 0.07f;
        return p;
    }

    IEnumerator PenBurst(List<Vector3> spots, float fuse)
    {
        if (penMarks != null) Destroy(penMarks);
        penMarks = new GameObject("RedPens");
        Shader sh = Shader.Find("Custom/ScreenGlow");
        var marks = new List<Renderer[]>();
        foreach (Vector3 s in spots)
        {
            var rs = new List<Renderer>();
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(penMarks.transform, false);
            disc.transform.position = s;
            disc.transform.localScale = new Vector3(PenRadius * 2f, 0.004f, PenRadius * 2f);
            rs.Add(Tint(disc, sh, PenDim));
            for (int k = 0; k < 2; k++)                                            // the X
            {
                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(bar.GetComponent<Collider>());
                bar.transform.SetParent(penMarks.transform, false);
                bar.transform.SetPositionAndRotation(s + Vector3.up * 0.012f, Quaternion.Euler(0f, 45f + 90f * k, 0f));
                bar.transform.localScale = new Vector3(PenRadius * 1.5f, 0.004f, 0.16f);
                rs.Add(Tint(bar, sh, PenHot));
            }
            marks.Add(rs.ToArray());
            CombatSfx.Play(CombatSfx.Clip.Telegraph, s, 0.7f);
        }

        float start = Time.time;
        while (Time.time - start < fuse)
        {
            float k = (Time.time - start) / fuse;
            float pulse = k > 0.55f ? 0.5f + 0.5f * Mathf.Sin(Time.time * 30f) : 0f;           // flickers faster as it is about to go
            foreach (Renderer[] rs in marks)
            {
                rs[0].material.color = Color.Lerp(PenDim, new Color(0.75f, 0.1f, 0.12f), k);
                for (int i = 1; i < rs.Length; i++) rs[i].material.color = Color.Lerp(PenHot, Color.white, pulse * 0.8f);
            }
            yield return null;
        }

        // burst: a white flash, damage once to anyone inside a circle
        foreach (Renderer[] rs in marks) foreach (Renderer r in rs) r.material.color = Color.white;
        bool hit = false;
        foreach (Vector3 s in spots)
        {
            CombatSfx.Play(CombatSfx.Clip.Heavy, s, 0.9f);
            if (hit || player.Health.Dead) continue;
            Vector3 to = player.transform.position - s; to.y = 0f;
            if (to.magnitude <= PenRadius)
            {
                player.ReceiveHit(PenDamage * BossDifficulty.DamageFactor, s);
                hit = true;
            }
        }
        yield return new WaitForSeconds(0.18f);
        ClearPens();
    }

    static readonly Color PenDim = new Color(0.35f, 0.02f, 0.04f), PenHot = new Color(1f, 0.25f, 0.18f);

    void ClearPens()
    {
        if (penRoutine != null) { StopCoroutine(penRoutine); penRoutine = null; }
        if (penMarks != null) { Destroy(penMarks); penMarks = null; }
    }

    // somewhere behind the player (or beside them if a wall is there) where he can stand
    bool FindBlinkSpot(out Vector3 spot)
    {
        Vector3 fwd = player.transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 chest = player.transform.position + Vector3.up * 1.0f;
        foreach (Vector3 side in new[] { -fwd, right, -right })
        {
            Vector3 p = player.transform.position + side * BlinkBehind;
            p.y = boss.transform.position.y > -5f ? boss.transform.position.y : 0.06f;
            bool wallBetween = Physics.Raycast(chest, side, BlinkBehind + 0.5f, ~0, QueryTriggerInteraction.Ignore) && !OnlyUs(chest, side);
            bool occupied = false;
            foreach (Collider c in Physics.OverlapSphere(p + Vector3.up * 1f, 0.45f, ~0, QueryTriggerInteraction.Ignore))
                if (c.GetComponentInParent<PlayerCombat>() == null && c.GetComponentInParent<ProfessorAI>() == null) occupied = true;
            if (!wallBetween && !occupied && InsideArena(p)) { spot = p; return true; }
        }
        spot = Vector3.zero;
        return false;
    }

    // true if the only thing the ray hits is the player or the boss (they do not count as a wall)
    bool OnlyUs(Vector3 from, Vector3 dir)
    {
        foreach (RaycastHit h in Physics.RaycastAll(from, dir, BlinkBehind + 0.5f, ~0, QueryTriggerInteraction.Ignore))
            if (h.collider.GetComponentInParent<PlayerCombat>() == null && h.collider.GetComponentInParent<ProfessorAI>() == null) return false;
        return true;
    }

    void BuildBlinkRing(Vector3 at, Vector3 faceDir)
    {
        marks = new GameObject("BlinkRing");
        GameObject tri = new GameObject("Sigil");
        tri.transform.SetParent(marks.transform, false);
        tri.transform.SetPositionAndRotation(new Vector3(at.x, 0.07f, at.z), Quaternion.LookRotation(faceDir.sqrMagnitude > 0.01f ? faceDir : Vector3.forward));
        tri.transform.localScale = new Vector3(1f, 0.01f, 1f);
        tri.AddComponent<MeshFilter>().sharedMesh = ProfessorAI.SigilMesh(1.2f);
        MeshRenderer mr = tri.AddComponent<MeshRenderer>();
        Shader sh = Shader.Find("Custom/ScreenGlow");
        if (sh != null) mr.sharedMaterial = new Material(sh) { color = Dim };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    // ---- the floor marks of the lunge
    Renderer[] markDots;
    static readonly Color Dim = new Color(0.30f, 0.02f, 0.05f), Lit = new Color(1f, 0.95f, 0.55f);

    void BuildMarks(Vector3 start, Vector3 dir, float length)
    {
        marks = new GameObject("LungeMarks");
        Shader sh = Shader.Find("Custom/ScreenGlow");
        float y = 0.06f;
        Quaternion rot = Quaternion.LookRotation(dir);

        GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(strip.GetComponent<Collider>());
        strip.transform.SetParent(marks.transform, false);
        strip.transform.SetPositionAndRotation(new Vector3(start.x, y, start.z) + dir * length * 0.5f, rot);
        strip.transform.localScale = new Vector3(1.0f, 0.01f, length);
        Tint(strip, sh, new Color(0.40f, 0.03f, 0.06f));

        markDots = new Renderer[3];
        float[] at = { 0.2f, 0.5f, 0.8f };
        for (int i = 0; i < 3; i++)
        {
            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(dot.GetComponent<Collider>());
            dot.transform.SetParent(marks.transform, false);
            dot.transform.SetPositionAndRotation(new Vector3(start.x, y + 0.01f, start.z) + dir * length * at[i], rot);
            dot.transform.localScale = new Vector3(1.0f, 0.01f, 0.7f);
            markDots[i] = Tint(dot, sh, Dim);
        }
    }

    void LightMark(int i)
    {
        if (markDots != null && i < markDots.Length && markDots[i] != null) markDots[i].material.color = Lit;
    }

    static Renderer Tint(GameObject go, Shader sh, Color c)
    {
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (sh != null) mr.sharedMaterial = new Material(sh) { color = c };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return mr;
    }

    // ---------------------------------------------------------------- phases
    void OnBossDamaged(float dealt)
    {
        if (finished || beaten) return;
        float frac = boss.Health.Fraction;

        // the Letter: the first time he drops to 10% in Opus, a player who has it is offered the choice
        if (phase == Opus && !offered && frac <= LetterAt && frac > 0f && phasesBuilt > Fable && GameProgress.HasItem(LetterItem))
        {
            offered = true;
            cutscene = StartCoroutine(LetterOffer());
            return;
        }

        int next = phase + 1;
        if (next >= Phases.Length || Phases[next].name == "Fable" || frac > Phases[next].startsAt) return;      // (Fable is never reached by damage: only through the Letter)

        if (next < phasesBuilt) StartCoroutine(ChangePhase(next));
        else if (next == phasesBuilt) EndFightForNow();               // the next phase is not built yet: the fight ends here
    }

    IEnumerator ChangePhase(int next)
    {
        phase = next;
        Interrupt();
        busy = true;
        fightOn = false;
        boss.Health.invulnerable = true;
        boss.brainPaused = true;
        boss.SetState(ProfessorAI.State.Recover, 2.4f);
        if (Phases[phase].name == "Fable") boss.SetForm(true, true);       // his true shape: the crown, the pauldrons, the cloak, the shards
        ApplyTuning();
        CombatHUD.Popup(Phases[phase].name.ToUpper(), PopupColours[Mathf.Clamp(phase, 0, PopupColours.Length - 1)], outline: true);
        CombatSfx.Play(CombatSfx.Clip.Stun, boss.transform.position, 1f);
        boss.Say(Phases[phase].line);
        yield return new WaitForSeconds(2.4f);
        boss.Health.invulnerable = false;
        boss.brainPaused = false;
        BeginFighting(0.4f);
    }

    // ================================================================ THE LETTER OFFER (at 10% in Opus)
    // He stops, keeps his guard down and says a line. For 7 seconds the prompt "[E] Present Letter of Recommendation" is on screen.
    // Nothing else hints at it: not the health bar, not the wording. Ignore it and the fight carries on from exactly where it was.
    IEnumerator LetterOffer()
    {
        Interrupt(true);
        busy = true;
        fightOn = false;
        boss.Health.invulnerable = true;
        boss.brainPaused = true;
        boss.SetState(ProfessorAI.State.Recover, 60f);
        boss.Say("Impressive. Have you anything to present in your defence?");

        letterPressed = false;
        offerActive = true;
        offerEndsAt = Time.time + LetterOfferTime;
        while (Time.time < offerEndsAt && !letterPressed && !player.Health.Dead) yield return null;
        offerActive = false;

        if (letterPressed && !player.Health.Dead)
        {
            yield return StartCoroutine(FakeOut());
            cutscene = null;
            yield break;
        }

        // no letter: back to the fight
        boss.Say("Nothing? Then get out of my class.");
        boss.Health.invulnerable = false;
        boss.brainPaused = false;
        boss.SetState(ProfessorAI.State.Idle);
        cutscene = null;
        BeginFighting(0.8f);
    }

    // ================================================================ THE FAKE-OUT
    // 1. you hold out the letter, he slouches (the fight music fades out as he says "A letter of recommendation.") and his bar slowly drains to 0
    // 2. a beat of "you won"... and dead silence
    // 3. he says he wrote the letter, the room flashes, he straightens and TRANSFORMS into his full form
    // 4. his bar refills to 20%: the fourth phase, Fable
    IEnumerator FakeOut()
    {
        LockPlayer();
        float maxHp = boss.Health.max;
        float startHp = boss.Health.Current;

        CombatSfx.Play(CombatSfx.Clip.Telegraph, boss.transform.position, 0.6f);
        string line = "What is that?";
        boss.Say(line);
        // the fight music starts fading out as "What is that?" begins and is fully gone 2 s after the slouch finishes (the slouch takes 1.1 s, see
        // ProfessorAI), and stays silent. Only the fade is long: the dialogue and the slouch below keep their own timing.
        MusicDirector.Silence(ProfessorAI.LineSeconds(line) + 0.15f + 1.3f + 1.1f + 2f);
        yield return new WaitForSeconds(ProfessorAI.LineSeconds(line) + 0.15f);
        boss.Say("A letter of recommendation.");
        yield return new WaitForSeconds(1.3f);

        // he slouches, the bar drains, the world loses its colour
        boss.SetSlouch(true);
        float drain = 2.6f, t0 = Time.time;
        while (Time.time - t0 < drain)
        {
            float k = Mathf.SmoothStep(0f, 1f, (Time.time - t0) / drain);
            boss.Health.SetCurrent(Mathf.Lerp(startHp, 0f, k));
            MixRealm(k);
            yield return null;
        }
        boss.Health.SetCurrent(0f);
        MixRealm(1f);
        yield return new WaitForSeconds(1.7f);                              // a long beat: it looks like you won

        yield return new WaitForSeconds(1.1f);                              // dead silence (the music has already faded out)

        line = "Did you read the signature?";
        boss.Say(line);
        yield return new WaitForSeconds(ProfessorAI.LineSeconds(line) + 0.15f);
        boss.Say("I wrote that letter.");
        yield return new WaitForSeconds(2.0f);

        // three pulses building up, then the change
        for (int i = 0; i < 3; i++)
        {
            CombatHUD.Flash(new Color(1f, 0.55f, 0.25f), 0.5f + 0.25f * i);
            CombatSfx.Play(CombatSfx.Clip.Heavy, boss.transform.position, 0.8f + 0.1f * i);
            MixRealm(1f - 0.3f * (i + 1));
            yield return new WaitForSeconds(0.45f);
        }
        boss.SetSlouch(false);
        boss.SetForm(true, true);                                           // the transformation: flash, swell, the crown and the pauldrons
        CombatHUD.Flash(Color.white, 1.4f);
        CombatSfx.Play(CombatSfx.Clip.Stun, boss.transform.position, 1f);
        CombatSfx.Play(CombatSfx.Clip.Heavy, boss.transform.position, 1f);
        CombatHUD.Popup("FABLE", PopupColours[Fable], outline: true);
        MusicDirector.Play(MusicTrack.Fable, 0.05f);                        // the intense music snaps on, as if someone just turned it up
        boss.Say("Recommendation denied.");

        // his health comes back, slowly, and the room's colours return
        phase = Fable;
        ApplyTuning();
        float heal = 2.6f; t0 = Time.time;
        while (Time.time - t0 < heal)
        {
            float k = Mathf.SmoothStep(0f, 1f, (Time.time - t0) / heal);
            boss.Health.SetCurrent(Mathf.Lerp(0f, maxHp * FableHealth, k));
            MixRealm(Mathf.Clamp01(1f - (Time.time - t0) / 1.2f) * 0.7f);
            yield return null;
        }
        boss.Health.SetCurrent(maxHp * FableHealth);
        RestoreRealm();
        yield return new WaitForSeconds(0.4f);

        UnlockPlayer();
        boss.Health.invulnerable = false;
        boss.brainPaused = false;
        boss.SetState(ProfessorAI.State.Idle);
        BeginFighting(0.4f);
    }

    // ---- the cutscene's helpers
    void LockPlayer()
    {
        if (playerLocked) return;
        playerLocked = true;
        savedPlayerInvulnerable = player.Health.invulnerable;
        player.Health.invulnerable = true;                                  // nothing can hurt you while you watch
        player.InputEnabled = false;
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = false;
    }

    void UnlockPlayer()
    {
        if (!playerLocked) return;
        playerLocked = false;
        player.Health.invulnerable = savedPlayerInvulnerable;
        player.InputEnabled = true;
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null && !beaten) pc.enabled = true;
    }

    // 0 = the room's own colours, 1 = washed out and dark (the "he is beaten" look)
    void MixRealm(float k)
    {
        if (realm == null) return;
        realm.fillColor = Color.Lerp(realmFill, new Color(0.42f, 0.34f, 0.35f), k);
        realm.lineColor = Color.Lerp(realmLine, new Color(0.08f, 0.06f, 0.07f), k);
        realm.voidColor = Color.Lerp(realmVoid, new Color(0.05f, 0.04f, 0.05f), k);
    }

    void RestoreRealm()
    {
        if (realm == null) return;
        realm.fillColor = realmFill; realm.lineColor = realmLine; realm.voidColor = realmVoid;
    }

    // the prompt: big, gold, under the crosshair, with a thin bar that runs down
    GUIStyle promptStyle;
    void OnGUI()
    {
        if (!offerActive) return;
        float S = TutorialUI.SizeFactor;
        if (promptStyle == null) promptStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        promptStyle.fontSize = Mathf.RoundToInt(28f * S);
        string text = Interactable.Prompt("Present Letter of Recommendation");
        Rect r = new Rect(0f, Screen.height * 0.58f, Screen.width, 50f * S);
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 6f);
        GUI.color = Color.white;
        promptStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, promptStyle);
        promptStyle.normal.textColor = new Color(1f, 0.85f * pulse, 0.3f * pulse, 1f);
        GUI.Label(r, text, promptStyle);
        float left = Mathf.Clamp01((offerEndsAt - Time.time) / LetterOfferTime);
        float w = 260f * S;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(Screen.width / 2f - w / 2f, r.yMax + 4f * S, w, 4f * S), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.85f, 0.3f, 0.9f);
        GUI.DrawTexture(new Rect(Screen.width / 2f - w / 2f, r.yMax + 4f * S, w * left, 4f * S), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    // ================================================================ ZERO ATTENDANCE (Fable)
    // His signature move. He lifts off, the whole floor flashes faster and faster for 2.5 seconds, then a shockwave crosses the room for 35.
    // The only ways through it: a PERFECT BLOCK timed to the end, or a DASH timed to the end. Do either and he is stunned for 5 seconds.
    // A heavy hit does not interrupt it.
    GameObject zeroFloor;

    IEnumerator ZeroAttendance()
    {
        StartAttack();
        boss.uninterruptible = true;
        float t = Tempo;
        boss.SetState(ProfessorAI.State.TelegraphVolley);
        boss.Say("Attendance... zero.");
        CombatHUD.Popup("ZERO ATTENDANCE", new Color(1f, 0.3f, 0.2f));

        // the floor of the whole room
        zeroFloor = new GameObject("ZeroAttendance");
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(zeroFloor.transform, false);
        float sx = Mathf.Min(roomMaxX - roomMinX, 40f), sz = Mathf.Min(roomMaxZ - roomMinZ, 40f);
        quad.transform.position = new Vector3((roomMinX + roomMaxX) * 0.5f, 0.09f, (roomMinZ + roomMaxZ) * 0.5f);
        quad.transform.localScale = new Vector3(sx, 0.005f, sz);
        Renderer rend = Tint(quad, Shader.Find("Custom/ScreenGlow"), PenDim);

        float windup = ZeroWindup / (1f + (t - 1f) * 0.5f);
        float start = Time.time, nextBeep = 0f, phaseOsc = 0f;
        while (Time.time - start < windup)
        {
            if (boss.Current == ProfessorAI.State.Dead) { Destroy(zeroFloor); zeroFloor = null; EndAttack(); yield break; }
            float k = (Time.time - start) / windup;
            phaseOsc += Time.deltaTime * Mathf.Lerp(3f, 24f, k * k);                 // the flashing speeds up
            rend.material.color = Color.Lerp(PenDim, new Color(1f, 0.35f, 0.25f), 0.5f + 0.5f * Mathf.Sin(phaseOsc)) * Mathf.Lerp(0.5f, 1f, k);
            if (Time.time >= nextBeep) { CombatSfx.Play(CombatSfx.Clip.Telegraph, boss.transform.position, 0.9f); nextBeep = Time.time + Mathf.Lerp(0.5f, 0.12f, k); }
            yield return null;
        }

        // the shockwave
        rend.material.color = Color.white;
        CombatHUD.Flash(Color.white, 1f);
        CombatSfx.Play(CombatSfx.Clip.Heavy, boss.transform.position, 1f);
        PlayerCombat.HitResult r = player.Health.Dead ? PlayerCombat.HitResult.Hit : player.ReceiveHit(ZeroDamage * BossDifficulty.DamageFactor, boss.transform.position);
        Debug.Log("[BossFight] Zero Attendance hit the player: " + r);
        yield return new WaitForSeconds(0.2f);
        Destroy(zeroFloor); zeroFloor = null;

        nextZeroAt = Time.time + ZeroCooldown / t;
        boss.uninterruptible = false;
        if (r == PlayerCombat.HitResult.Parried || r == PlayerCombat.HitResult.Dodged)
        {
            boss.stunTime = ZeroStun;
            boss.StunNow();
            CombatHUD.Popup("STUNNED", new Color(1f, 0.9f, 0.3f));
            StartCoroutine(RestoreStunTime(ZeroStun + 0.3f));
        }
        else boss.SetState(ProfessorAI.State.Recover, 1.2f / t);
        EndAttack();
    }

    IEnumerator RestoreStunTime(float after)
    {
        yield return new WaitForSeconds(after);
        if (boss != null) boss.stunTime = 3f;
    }

    // Until all phases exist: reaching the start of the first missing phase counts as beating him
    void EndFightForNow()
    {
        Debug.Log("[BossFight] Phase " + Phases[phase + 1].name + " is not built yet: ending the fight with Mission Passed.");
        boss.Health.invulnerable = false;
        boss.Health.Damage(boss.Health.max * 2f);
    }

    // ---------------------------------------------------------------- winning
    void OnBossDied()
    {
        if (finished) return;
        finished = true;
        fightOn = false;
        Interrupt();
        player.Health.invulnerable = true;                              // nothing can beat you now (a chalk still in the air)
        StartCoroutine(Victory());
    }

    // The world stays monochrome while the music fades out (a slow 4.6 s fade, 2 s longer than before), and the colour wave starts once it is nearly
    // gone: the music is fully silent 1 s into the wave. The wave itself and the "Mission Passed" screen keep their own timing after that.
    const float WaitBeforeWave = 3.6f, WaveSeconds = 4.8f;

    IEnumerator Victory()
    {
        MusicDirector.Silence(WaitBeforeWave + 1f);                  // the fade lasts 1 s into the colour wave (final)
        boss.Say("Well... here's your attendance.");
        yield return new WaitForSeconds(WaitBeforeWave);             // let the line be read while he falls

        // the room's real colours fan out from where he fell (the flat red look melts away in a growing circle); when the whole room has its
        // colours back, the "Mission Passed" screen comes up
        RealmStyle rs = realm != null ? realm : FindAnyObjectByType<RealmStyle>();
        if (rs != null)
        {
            Vector3 from = boss.transform.position + Vector3.up * 1f;
            // The wave covers the WHOLE room, including the part behind the player (turn around and it is coloured too). It is sized to reach the
            // farthest corner of the room and no further, and it speeds up as it goes: slow around him, fast across the room. The moment it
            // reaches that corner the whole room is coloured and the screen comes up, with nothing left to wait for.
            float far = FarthestCornerFrom(from);
            rs.Reveal(from, WaveSeconds, far, 2f);
            CombatSfx.Play(CombatSfx.Clip.Stun, boss.transform.position, 0.9f);
            while (!rs.RevealDone) yield return null;                 // and the screen comes up the moment the room is fully coloured
        }
        else yield return new WaitForSeconds(WaveSeconds - 3f);
        MissionPassedScreen.Show();
    }

    // How far from a point is the farthest corner of the room (floor or ceiling)? The wave has to reach exactly that far.
    float FarthestCornerFrom(Vector3 from)
    {
        if (Mathf.Abs(roomMinX) > 1e5f || Mathf.Abs(roomMaxX) > 1e5f || Mathf.Abs(roomMinZ) > 1e5f || Mathf.Abs(roomMaxZ) > 1e5f) return 25f;   // (walls were not found)
        float ceiling = 2.6f;                                           // a little above the ceiling (2.48)
        if (Physics.Raycast(from, Vector3.up, out RaycastHit up, 10f, ~0, QueryTriggerInteraction.Ignore)) ceiling = up.point.y;
        float best = 0f;
        foreach (float x in new[] { roomMinX, roomMaxX })
            foreach (float z in new[] { roomMinZ, roomMaxZ })
                foreach (float y in new[] { 0f, ceiling })
                    best = Mathf.Max(best, Vector3.Distance(from, new Vector3(x, y, z)));
        return best + 0.3f;                                             // + the soft edge of the wave, so the last pixel of the corner is fully coloured
    }

    // ---------------------------------------------------------------- losing: "ATTENDANCE 0%" and TRY AGAIN, like the tutorial fight
    void OnPlayerBeaten()
    {
        if (beaten || finished) return;
        StartCoroutine(Beaten());
    }

    IEnumerator Beaten()
    {
        beaten = true;
        fightOn = false;
        Interrupt();
        boss.brainPaused = true;
        player.InputEnabled = false;
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = false;
        boss.Say("Class dismissed.");
        bool tryAgain = false;
        WastedScreen.Show(() => tryAgain = true);
        while (!tryAgain) yield return null;

        player.Health.Restore();
        player.ResetState();
        if (PlayerSpawn.Current != null) PlayerSpawn.Current.ResetPlayer();
        else if (pc != null) pc.enabled = true;
        boss.ResetFight();
        boss.SetForm(false, false);                                      // back to his base form
        phase = 0;
        offered = false; letterPressed = false;                          // (the Letter is still in your pocket: the offer can come again)
        boss.Health.Configure(baseHealth * BossDifficulty.HpMultiplier, 0f);
        ApplyTuning();
        boss.brainPaused = false;
        boss.Health.invulnerable = false;
        player.InputEnabled = true;
        beaten = false;
        BeginFighting(1.5f);
        MusicDirector.Play(MusicTrack.CombatClaude, 1.5f);                     // (always the normal fight music again, even if you fell during Fable)
    }
}
