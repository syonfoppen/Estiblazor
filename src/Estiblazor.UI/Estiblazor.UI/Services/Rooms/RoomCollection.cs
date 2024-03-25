using Microsoft.Extensions.Caching.Memory;

namespace Estiblazor.UI.Services.Rooms
{
    public class RoomCollection : IRoomCollection
    {
        public RoomCollection(IMemoryCache memoryCache)
        {
            this.memoryCache = memoryCache;
        }

        private readonly HashSet<string> roomNames = [];


        public List<string> GetRoomNames() => roomNames.ToList();

        private readonly IMemoryCache memoryCache;

        RoomViewModel IRoomCollection.GetOrCreateRoom(string roomid)
        {
            var vm = memoryCache.GetOrCreate(new RoomId(roomid), cacheEntry =>
            {
                List<EstimationStage> estimationStages =
                [
                    new EstimationStage
                    {
                        Name = "effort",
                        AvailableChoices = ["0.5", "1", "2", "3", "5", "8", "13", "20", "<i class=\"fa-solid fa-infinity\"></i>"],
                        IsRevealed = false,
                        UserChoices = [],
                    },
                    new EstimationStage
                    {
                        Name = "complexity",
                        AvailableChoices = ["S", "M", "L", "<i class=\"fa-solid fa-infinity\"></i>"],
                        IsRevealed = false,
                        UserChoices = []
                    },
                    new EstimationStage
                    {
                        Name = "Like",
                        AvailableChoices = ["<i class=\"fa-solid fa-thumbs-up\"></i>", "<i class=\"fas fa-meh\"></i>", "<i class=\"fa-solid fa-thumbs-down\"></i>"],
                        IsRevealed = false,
                        UserChoices = []

                    },
                ];
                var vm = new RoomViewModel(estimationStages) { Id = new RoomId(roomid), Name = roomid };

                OnCreate(roomid);
                ConfigureCacheEntry(cacheEntry);

                return vm;
            });

            return vm!;
        }

        private void ConfigureCacheEntry(ICacheEntry cacheEntry)
        {
            cacheEntry.SlidingExpiration = TimeSpan.FromDays(7);
            cacheEntry.RegisterPostEvictionCallback(OnEvict);
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
            var vm = memoryCache.Get<RoomViewModel>(new RoomId(roomId));

            return vm;
        }

        public void AddNewRoom(RoomViewModel vm)
        {
            using var entry = memoryCache.CreateEntry(new RoomId(vm.Id.Name));
            entry.SetValue(vm);
            OnCreate(vm.Id.Name);
            ConfigureCacheEntry(entry);
        }
    }

}
