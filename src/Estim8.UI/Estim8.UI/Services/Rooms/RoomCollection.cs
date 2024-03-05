using Microsoft.Extensions.Caching.Memory;

namespace Estim8.UI.Services.Rooms
{
    public class RoomCollection : IRoomCollection
    {
        public RoomCollection(IMemoryCache memoryCache)
        {
            this.memoryCache = memoryCache;
        }

        private readonly HashSet<string> roomNames = new();

        private sealed record class RoomKey(string RoomId)
        {
        }

        public List<string> GetRoomNames() => roomNames.ToList();

        private readonly IMemoryCache memoryCache;

        RoomViewModel IRoomCollection.GetOrCreateRoom(string roomid)
        {
            var vm = memoryCache.GetOrCreate(new RoomKey(roomid), cacheEntry =>
            {
                var vm = new RoomViewModel { Id = new RoomId(roomid), Name = roomid };
                vm.EstimationStages.Add(new EstimationStage
                {
                    Name = "effort",
                    AvailableChoices = ["0.5", "1", "2", "3", "5", "8", "13", "20", "<i class=\"fa-solid fa-infinity\"></i>"],
                    IsRevealed = false,
                    UserChoices = [],
                });
                vm.EstimationStages.Add(new EstimationStage
                {
                    Name = "complexity",
                    AvailableChoices = ["S", "M", "L", "<i class=\"fa-solid fa-infinity\"></i>"],
                    IsRevealed = false,
                    UserChoices = []
                });
                vm.EstimationStages.Add(new EstimationStage
                {
                    Name = "Like",
                    AvailableChoices = ["<i class=\"fa-solid fa-thumbs-up\"></i>", "<i class=\"fa-solid fa-thumbs-down\"></i>"],
                    IsRevealed = false,
                    UserChoices = []

                });
                OnCreate(roomid);
                cacheEntry.SlidingExpiration = TimeSpan.FromDays(7);
                cacheEntry.RegisterPostEvictionCallback(OnEvict);
                return vm;
            });

            return vm!;
        }

        private void OnCreate(string name)
        {
            roomNames.Add(name);
        }

        private void OnEvict(object key, object? value, EvictionReason reason, object? state)
        {
            if (value is RoomId roomId)
            {
                roomNames.Remove(roomId.Name);
            }
        }

        RoomViewModel? IRoomCollection.GetExistingRoom(string roomId)
        {
            var vm = memoryCache.Get<RoomViewModel>(new RoomKey(roomId));

            return vm;
        }

    }

}
