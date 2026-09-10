namespace RobEveryone.Interaction
{
    public interface IInteractable
    {
        string InteractionPrompt { get; }
        // Whether this is actually usable right now -- e.g. a player is
        // only a valid theft target while stunned. Interactor won't
        // resolve a target whose CanInteract is false, so the E-key
        // prompt only ever appears when the action would actually work.
        bool CanInteract { get; }
        void Interact(UnityEngine.GameObject interactor);
    }
}
