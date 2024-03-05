using Estim8.UI.Services.Users;

namespace Estim8.UI.Services.Rooms
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
