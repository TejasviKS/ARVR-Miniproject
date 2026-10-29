using UnityEngine;

// VERSION 13
// The invisible "press E here" box around the beeping computer.
// You DON'T add this yourself: BeepingComputerPuzzle creates it when the game starts.
public class ComputerSwitch : Interactable
{
    [HideInInspector] public BeepingComputerPuzzle puzzle;

    public override bool CanInteract => puzzle != null && puzzle.IsBeeping;
    public override string GetPrompt() => "Press E to switch off the computer";
    public override void Interact(PlayerInteractor player) => puzzle.SwitchOff(player);
}
