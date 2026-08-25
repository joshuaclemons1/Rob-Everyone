namespace RobEveryone.Interaction
{
    public interface IInteractable
    {
        string InteractionPrompt { get; }
        void Interact(UnityEngine.GameObject interactor);
    }
}
