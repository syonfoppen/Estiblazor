using Estim8.UI.Services.Rooms;

namespace Estim8.UI.Services.Users
{

    public class User : NotifiesPropertyChanged
    {
        public const string DEFAULTKEY = "_";

        public UserId Id { get; set; } = new UserId(string.Empty);

        private string name = string.Empty;
        public string Name { get => name; set => PropertyChange(ref name, value); }

        private RoomViewModel? currentRoom;

        public RoomViewModel? CurrentRoom { get => currentRoom; private set => PropertyChange(ref currentRoom, value); }

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

        public void SetRoom(RoomViewModel? room)
        {
            CurrentRoom?.RemoveUser(this);
            CurrentRoom = room;
            CurrentRoom?.AddUser(this);
        }
    }
}
