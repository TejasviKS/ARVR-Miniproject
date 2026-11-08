using System.Collections;
using UnityEngine;

// VERSION 1
// Runs the combat tutorial in the second cabin. Put it on ONE empty object and fill in the slots.
// The same kind of teaching box as the first tutorial (bottom right), one lesson at a time:
//
//   COMBAT       he introduces himself (he cannot hurt you yet)
//   SWING        left click: land 3 hits
//   HEAVY SWING  hold left click until the bar fills, let go: land 1 heavy hit
//   BLOCK        hold right click: block one of his (slow) swings
//   PERFECT BLOCK  press right click just as the swing lands: it stuns him
//   COUNTER      hit him while he is stunned (double damage)
//   DODGE        press Space as his chalk comes
//   DEFEAT HIM   the real fight, with everything you learned
//
// He CANNOT be beaten before the last lesson (his health has a floor), so every lesson always gets shown.
// He only has two attacks: a pointer swing and one slow chalk throw. The boss will have many more.
// If YOU are beaten you simply start the fight again (the lesson you were on is kept).
public class CombatTutorial : MonoBehaviour
{
    [Header("The fight")]
    public ProfessorAI professor;
    public PlayerCombat player;
    [Tooltip("Hidden at the start; appears when he is beaten")]
    public GameObject portal;

    [Header("The teaching box")]
    [Range(0.5f, 1.2f)] public float boxScale = 0.75f;
    [Tooltip("Hits needed to finish the SWING lesson")]
    public int lightHitsNeeded = 3;

    enum Step { Intro, Swing, Heavy, Block, Parry, Counter, Dodge, Finish, Done }
    Step step = Step.Intro;
    float stepStart;
    bool done;
    float doneAt;
    int hits;
    bool parriedEarly;
    float boxAlpha;
    PlayerInteractor interactor;
    bool beatenRoutineRunning;

    // ================================================================ setup
    void Start()
    {
        Debug.Log("[Scripts] CombatTutorial v1 running", this);
        if (professor == null) professor = FindAnyObjectByType<ProfessorAI>();
        if (player == null) player = FindAnyObjectByType<PlayerCombat>();
        interactor = FindAnyObjectByType<PlayerInteractor>();
        if (professor == null || player == null) { Debug.LogError("CombatTutorial: needs a Professor and a Player with PlayerCombat.", this); enabled = false; return; }

        if (portal != null) portal.SetActive(false);

        player.HitLanded += OnPlayerHit;
        player.Defended += OnDefended;
        player.Dashed += OnDashed;
        professor.HitReceived += OnProfessorHit;
        professor.ImpactSoon += OnImpactSoon;
        professor.Telegraphing += OnTelegraphing;
        professor.Died += OnProfessorDied;
        player.Health.Died += OnPlayerBeaten;

        professor.Health.minFraction = 0.3f;               // he cannot be beaten until the last lesson
        EnterStep(Step.Intro);
    }

    void OnDestroy()
    {
        if (player != null) { player.HitLanded -= OnPlayerHit; player.Defended -= OnDefended; player.Dashed -= OnDashed; if (player.Health != null) player.Health.Died -= OnPlayerBeaten; }
        if (professor != null) { professor.HitReceived -= OnProfessorHit; professor.ImpactSoon -= OnImpactSoon; professor.Telegraphing -= OnTelegraphing; professor.Died -= OnProfessorDied; }
    }

    // ================================================================ steps
    void EnterStep(Step s)
    {
        step = s;
        stepStart = Time.time;
        done = false;
        hits = 0;
        switch (s)
        {
            case Step.Intro:
                SetProfessor(aggressive: false, melee: false, chalk: false);
                StartCoroutine(IntroLines());
                break;
            case Step.Swing:
            case Step.Heavy:
                SetProfessor(false, false, false);
                break;
            case Step.Block:
                SetProfessor(true, true, false);
                professor.meleeTelegraph = 1.3f; professor.meleeDamage = 6f; professor.meleeCooldown = 1.6f;
                professor.Say("Let me remind you who is teaching here.");
                break;
            case Step.Parry:
                SetProfessor(true, true, false);
                professor.meleeTelegraph = 1.15f; professor.meleeDamage = 6f; professor.meleeCooldown = 1.6f;
                break;
            case Step.Counter:
                SetProfessor(true, true, false);
                break;
            case Step.Dodge:
                SetProfessor(true, false, true);
                professor.chalkTelegraph = 1.0f; professor.chalkCooldown = 2.2f; professor.chalkDamage = 6f;
                break;
            case Step.Finish:
                SetProfessor(true, true, true);
                professor.meleeTelegraph = 0.9f; professor.meleeDamage = 10f; professor.meleeCooldown = 1.2f;
                professor.chalkTelegraph = 0.95f; professor.chalkDamage = 6f; professor.chalkCooldown = 3.2f;
                professor.Health.minFraction = 0f;         // now he can fall
                professor.Say("This lecture ends with YOU leaving.");
                break;
            case Step.Done:
                break;
        }
    }

    void SetProfessor(bool aggressive, bool melee, bool chalk)
    {
        professor.aggressive = aggressive;
        professor.useMelee = melee;
        professor.useChalk = chalk;
    }

    IEnumerator IntroLines()
    {
        // from the moment you arrive until he has finished speaking you can neither walk nor look around (the next lesson starts then)
        PlayerController pc = player != null ? player.GetComponent<PlayerController>() : null;
        if (pc != null) { pc.movementLocked = true; pc.lookLocked = true; }
        yield return new WaitForSeconds(3.5f);                    // let the player read the tutorial box first
        const float pause = 0.4f;                                  // a short beat between two lines
        string line = "Ah. There you are.";
        professor.Say(line);
        yield return new WaitForSeconds(ProfessorAI.LineSeconds(line) + pause);
        line = "Attendance: 74%.";
        professor.Say(line);
        yield return new WaitForSeconds(ProfessorAI.LineSeconds(line) + pause);
        line = "The minimum is 75%.";
        professor.Say(line);
        yield return new WaitForSeconds(ProfessorAI.LineSeconds(line));
        if (pc != null) { pc.movementLocked = false; pc.lookLocked = false; }
        introLinesDone = true;
    }
    bool introLinesDone;

    void Complete()
    {
        if (done) return;
        done = true;
        doneAt = Time.time;
    }

    void Advance()
    {
        if (step == Step.Block && parriedEarly) { parriedEarly = false; EnterStep(Step.Counter); return; }   // already did the perfect block
        EnterStep(step + 1);
    }

    void Update()
    {
        if (!done)
        {
            switch (step)
            {
                case Step.Intro:
                    if (introLinesDone) Complete();                    // (the first lesson starts once he has finished speaking)
                    break;
                case Step.Counter:
                    // the stun wore off before you hit him: teach the perfect block again
                    if (!professor.IsStunned && Time.time - stepStart > 0.6f) EnterStep(Step.Parry);
                    break;
            }
        }
        else if (Time.time - doneAt > 0.9f && step != Step.Done) Advance();

        boxAlpha = Mathf.MoveTowards(boxAlpha, (step == Step.Done || step == Step.Finish && Time.time - stepStart > 14f) ? 0f : 1f, Time.deltaTime * 4f);
    }

    // ================================================================ what happened in the fight
    void OnPlayerHit(ProfessorAI who, bool heavy)
    {
        if (done) return;
        if (step == Step.Swing) { hits++; if (hits >= lightHitsNeeded) Complete(); }
        else if (step == Step.Heavy && heavy) Complete();
        if (hits == 1 && step == Step.Swing) professor.Say("How rude!");
    }

    void OnDefended(PlayerCombat.HitResult r)
    {
        if (done) return;
        if (step == Step.Block && (r == PlayerCombat.HitResult.Blocked || r == PlayerCombat.HitResult.Parried))
        {
            if (r == PlayerCombat.HitResult.Parried) parriedEarly = true;
            Complete();
        }
        else if (step == Step.Parry && r == PlayerCombat.HitResult.Parried) Complete();
        else if (step == Step.Dodge && r == PlayerCombat.HitResult.Dodged) Complete();
        if (r == PlayerCombat.HitResult.Parried) professor.Say("Impossible...");
    }

    void OnDashed()
    {
        if (done || step != Step.Dodge) return;
        // dodging while his chalk is being thrown or is flying counts
        if (Chalk.InFlight > 0 || professor.Current == ProfessorAI.State.TelegraphChalk) Complete();
    }

    void OnProfessorHit(float dealt, bool heavy, bool wasStunned)
    {
        if (!done && step == Step.Counter && wasStunned) Complete();
    }

    void OnImpactSoon()
    {
        if (step == Step.Parry && !done) CombatHUD.Popup("NOW!", Color.white);
    }

    void OnTelegraphing(ProfessorAI.State s)
    {
        if (step == Step.Dodge && s == ProfessorAI.State.TelegraphChalk) professor.Say("Catch!");
    }

    void OnProfessorDied()
    {
        if (step == Step.Done) return;
        step = Step.Done;
        done = false;
        professor.Say("My... attendance... records...");
        StartCoroutine(Victory());
    }

    IEnumerator Victory()
    {
        yield return new WaitForSeconds(2.8f);
        if (portal != null) portal.SetActive(true);
        if (interactor != null) interactor.ShowBanner("A portal has opened!", 4f);
    }

    // you were beaten: back to the start of the fight, keeping the lesson you were on
    void OnPlayerBeaten()
    {
        if (beatenRoutineRunning) return;
        StartCoroutine(Beaten());
    }

    IEnumerator Beaten()
    {
        beatenRoutineRunning = true;
        player.InputEnabled = false;
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = false;
        professor.Say("Class dismissed.");
        bool tryAgain = false;
        WastedScreen.Show(() => tryAgain = true);               // "ATTENDANCE 0%" + TRY AGAIN: nothing restarts until the player clicks it
        while (!tryAgain) yield return null;

        player.Health.Restore();
        player.ResetState();
        if (PlayerSpawn.Current != null) PlayerSpawn.Current.ResetPlayer();      // also switches walking back on
        else if (pc != null) pc.enabled = true;
        professor.ResetFight();
        if (step == Step.Counter) EnterStep(Step.Parry);
        player.InputEnabled = true;
        beatenRoutineRunning = false;
    }

    // ================================================================ drawing
    void OnGUI()
    {
        TutorialUI.Ensure();
        if (boxAlpha > 0.01f && step != Step.Done) DrawBox();
        if (step == Step.Done && portal != null && portal.activeSelf) DrawObjective("Step through the portal");
        else if (step == Step.Finish && !done && professor != null && !professor.Health.Dead) DrawObjective("Defeat " + professor.displayName);
        GUI.color = Color.white;
    }

    void DrawObjective(string text) => TutorialUI.ObjectiveStrip(text);

    void DrawBox()
    {
        float a = boxAlpha;
        Matrix4x4 saved;
        Rect box = TutorialUI.Begin(boxScale, out saved);
        TutorialUI.Panel(box, a);
        Vector2 icon = new Vector2(box.x + 120f, box.y + 112f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);

        string title = "", body = "", small = "";
        float progress = -1f;
        switch (step)
        {
            case Step.Intro:
                title = "COMBAT"; body = "Fight him with your textbook.";
                small = "Each lesson appears here.";
                TutorialUI.Key(new Rect(icon.x - 36f, icon.y - 36f, 72f, 72f), "!", true, a);
                break;
            case Step.Swing:
                title = "SWING"; body = "Get close and click the left mouse button.";
                small = "Land " + lightHitsNeeded + " hits.";
                progress = (float)hits / lightHitsNeeded;
                TutorialUI.Mouse(icon, true, false, pulse, a);
                break;
            case Step.Heavy:
                title = "HEAVY SWING"; body = "Hold left click, then release when full.";
                small = "Slower, but hits harder and breaks his focus.";
                TutorialUI.Mouse(icon + new Vector2(0f, -8f), true, false, player != null && player.Charging ? 1f : pulse, a);
                DrawChargeBar(new Rect(icon.x - 40f, icon.y + 50f, 80f, 8f), a);
                break;
            case Step.Block:
                title = "BLOCK"; body = "Hold the right mouse button to block.";
                small = "A block takes most of the damage.";
                TutorialUI.Mouse(icon, false, true, pulse, a);
                break;
            case Step.Parry:
                title = "PERFECT BLOCK"; body = "Right-click just as his swing lands.";
                small = "No damage, and it stuns him. Watch for the red flash.";
                TutorialUI.Mouse(icon, false, true, pulse, a);
                break;
            case Step.Counter:
                title = "COUNTER"; body = "He's stunned! Hit him now with left click.";
                small = "Stunned enemies take double damage.";
                TutorialUI.Mouse(icon, true, false, pulse, a);
                break;
            case Step.Dodge:
                title = "DODGE"; body = "Press SPACE while moving to dodge.";
                small = "You can't be hit for a moment. No key held = step back.";
                TutorialUI.Key(new Rect(icon.x - 82f, icon.y - 26f, 164f, 54f), "SPACE", Input_SpacePressed(), a);
                break;
            case Step.Finish:
                title = "DEFEAT HIM"; body = "Use everything you've learned.";
                small = "Click: swing. Hold: heavy. Right click: block. Space: dodge.";
                TutorialUI.Mouse(icon + new Vector2(0f, -12f), true, true, pulse, a);
                TutorialUI.Key(new Rect(icon.x - 40f, icon.y + 42f, 80f, 30f), "SPACE", false, a, TutorialUI.KeySmallStyle);
                break;
        }
        TutorialUI.Texts(box, title, body, small, progress, a);
        if (done) TutorialUI.DoneTick(box, a);
        TutorialUI.End(saved);
    }

    void DrawChargeBar(Rect r, float a)
    {
        TutorialUI.Round(r, new Color(1f, 1f, 1f, 0.12f * a), 4f);
        float f = player != null ? player.Charge01 : 0f;
        if (f > 0.01f) TutorialUI.Round(new Rect(r.x, r.y, r.width * f, r.height), f >= 1f ? new Color(1f, 0.85f, 0.3f, a) : new Color(1f, 1f, 1f, a), 4f);
    }

    static bool Input_SpacePressed()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.spaceKey.isPressed;
    }
}
