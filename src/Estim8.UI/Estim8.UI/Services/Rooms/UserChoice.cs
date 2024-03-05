using Estim8.UI.Services.Users;

namespace Estim8.UI.Services.Rooms
{
    public class UserChoice : NotifiesPropertyChanged
    {
        private string choice = string.Empty;
        public string Choice { get => choice; set => PropertyChange(ref choice, value); }



        private UserId userId = new(string.Empty);
        public UserId UserId { get => userId; set => PropertyChange(ref userId, value); }

    }
}
