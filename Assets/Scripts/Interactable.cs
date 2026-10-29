using UnityEngine;

// VERSION 8
// Base class for EVERYTHING the player can press E on:
// seats, switches, cupboards, doors, chairs...
// Each specific thing (SitSpot, Switch, ...) inherits from this and says what happens in Interact().
public abstract class Interactable : MonoBehaviour
{
    [Tooltip("Shown on screen as: Press E to <promptText>")]
    public string promptText = "interact";

    // Text shown below the crosshair when looking at this
    public virtual string GetPrompt() => "Press E to " + promptText;

    // Return false to temporarily disable the interaction
    public virtual bool CanInteract => true;

    // What happens when the player presses E
    public abstract void Interact(PlayerInteractor player);

    // Emergency exit (e.g. stand up instantly). Things that lock the player override this.
    public virtual void ForceRelease(PlayerInteractor player) { player.Unlock(); }
}
