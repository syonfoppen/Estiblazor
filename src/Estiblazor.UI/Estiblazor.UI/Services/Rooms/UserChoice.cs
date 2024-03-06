using Estiblazor.UI.Services.Users;

namespace Estiblazor.UI.Services.Rooms
{
    public class UserChoice : NotifiesPropertyChanged
    {
        private string choice = string.Empty;
        public string Choice { get => choice; set => PropertyChange(ref choice, value); }



        private UserId userId = new(string.Empty);
        public UserId UserId { get => userId; set => PropertyChange(ref userId, value); }

    }
}
