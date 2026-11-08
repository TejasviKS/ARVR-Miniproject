using System.Collections;
using UnityEngine;

// VERSION 1
// The very first thing in the game: when you arrive in the tutorial cabin, YOU talk to yourself (four lines in the same dialogue box the
// professors use, with the same little voice blips). You cannot walk or look around until the last line is done, and the tutorial box only
// appears after that.
// Put this on one object in the cabin scene (next to a CombatHUD, which draws the box). The TutorialDirector finds it by itself and waits for it.
public class CabinIntroDialogue : MonoBehaviour
{
    [Tooltip("Seconds between arriving and the first line")]
    public float firstLineDelay = 1.0f;
    [Tooltip("A short beat between two lines")]
    public float pause = 0.4f;

    public bool Finished { get; private set; }

    static readonly string[] Lines =
    {
        "Oh no! My attendance is at 74%.",
        "I need at least 75% to be eligible to write my exams.",
        "Man, it's been rough since the faculty got replaced by AI.",
        "There's no leniency at all.",
    };

    AudioSource voice;
    AudioClip blip;

    IEnumerator Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) { pc.movementLocked = true; pc.lookLocked = true; }         // frozen from the moment you arrive

        voice = gameObject.AddComponent<AudioSource>();
        voice.spatialBlend = 0f;                                                    // your own voice: not 3D
        voice.volume = 0.8f; voice.playOnAwake = false;
        blip = RiddleNPC.MakeBlip();

        yield return new WaitForSeconds(firstLineDelay);
        Color name = Color.Lerp(TutorialUI.Good, Color.white, 0.35f);              // a soft green, like your attendance bar
        for (int i = 0; i < Lines.Length; i++)
        {
            CombatHUD.Say("You", Lines[i], -1f, name);
            StartCoroutine(Speak(Lines[i]));
            // each line waits for its own length (typing + reading time, ProfessorAI.LineSeconds); the last one has no extra beat
            yield return new WaitForSeconds(ProfessorAI.LineSeconds(Lines[i]) + (i < Lines.Length - 1 ? pause : 0f));
        }

        if (pc != null) { pc.movementLocked = false; pc.lookLocked = false; }
        Finished = true;
    }

    // the same "blip every third letter at a random pitch" as the professors
    IEnumerator Speak(string line)
    {
        float letters = 0f; int shown = 0;
        while (shown < line.Length)
        {
            letters += Time.deltaTime * ProfessorAI.LettersPerSecond;
            while (shown < line.Length && shown < (int)letters)
            {
                shown++;
                if (shown % 3 == 0 && !char.IsWhiteSpace(line[shown - 1]))
                {
                    voice.pitch = Random.Range(0.85f, 1.3f);
                    voice.PlayOneShot(blip, 0.8f);
                }
            }
            yield return null;
        }
    }
}
