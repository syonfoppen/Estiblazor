using Estiblazor.UI.Services.Users;

namespace Estiblazor.UI.Services.Rooms
{
    public class ChoiceChangedEventArgs : EventArgs
    {
        public ChoiceChangedEventArgs(UserId userId, string? choice)
        {
            UserId = userId;
            Choice = choice;
        }

        public UserId UserId { get; }
        public string? Choice { get; }
    }
}
