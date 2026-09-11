namespace RobEveryone.Interaction
{
    // Optional secondary line an IInteractable can show below its main
    // prompt -- for something that's a structurally valid target
    // (CanInteract is true, so the main prompt already shows) but isn't
    // actually usable right now for a reason worth calling out, e.g. a
    // shop shelf you can't currently afford. Empty/null = nothing to
    // show. Deliberately separate from IInteractable itself -- most
    // interactables (PickupItem, ReadySpot, a jail cell) never have
    // anything to warn about, so this only needs implementing by the
    // few that do.
    public interface IInteractableWarning
    {
        string WarningText { get; }
    }
}
