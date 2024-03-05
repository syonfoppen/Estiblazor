using Estim8.UI.Services.Rooms;

namespace Estim8.UI.Services.Users
{

    public class User : NotifiesPropertyChanged
    {
        public const string DEFAULTKEY = "_";

        public UserId Id { get; set; } = new UserId(string.Empty);

        private string name = string.Empty;
        public string Name { get => name; set => PropertyChange(ref name, value); }

        public RoomId DefaultRoom { get; set; } = new RoomId("default");

        private int onlineCounter = 0;

        public bool IsOnline => onlineCounter > 0;

        public void IncreaseOnline()
        {
            onlineCounter++;
            if (onlineCounter == 1)
            {
                OnPropertyChanged(nameof(IsOnline));
            }
        }

        public void DecreaseOnline()
        {
            onlineCounter--;
            if (onlineCounter <= 0)
            {
                OnPropertyChanged(nameof(IsOnline));
            }
        }

    }
}
