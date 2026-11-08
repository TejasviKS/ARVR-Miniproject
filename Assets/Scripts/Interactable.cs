using UnityEngine;

// VERSION 9
// Base class for EVERYTHING the player can press E on:
// seats, switches, cupboards, doors, chairs...
// Each specific thing (SitSpot, Switch, ...) inherits from this and says what happens in Interact().
public abstract class Interactable : MonoBehaviour
{
    [Tooltip("Shown on screen as: [E] <Prompt text>")]
    public string promptText = "interact";

    // Every prompt in the game looks the same: "[E] Action". Use this so the format lives in one place.
    public static string Prompt(string action)
    {
        if (string.IsNullOrEmpty(action)) return "";
        return "[E] " + char.ToUpper(action[0]) + action.Substring(1);
    }

    // Text shown below the crosshair when looking at this
    public virtual string GetPrompt() => Prompt(promptText);

    // Return false to temporarily disable the interaction
    public virtual bool CanInteract => true;

    // Return true if a left mouse click on this should work like pressing E (e.g. wall switches).
    // Off by default: seats and conversations use clicks for other things.
    public virtual bool AcceptsClick => false;

    // What happens when the player presses E
    public abstract void Interact(PlayerInteractor player);

    // Emergency exit (e.g. stand up instantly). Things that lock the player override this.
    public virtual void ForceRelease(PlayerInteractor player) { player.Unlock(); }
}
