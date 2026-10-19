namespace Starfall.Interaction
{
    /// <summary>
    /// Anything the player can look at and use with the interact key.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Action text shown next to the key hint, e.g. "Repair panel".</summary>
        string Prompt { get; }

        bool CanInteract { get; }

        void Interact();
    }
}
