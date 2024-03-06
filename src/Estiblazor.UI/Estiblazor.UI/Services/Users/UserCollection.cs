
using Microsoft.Extensions.Caching.Memory;

namespace Estiblazor.UI.Services.Users
{
    public class UserCollection(IMemoryCache memoryCache) : IUserCollection
    {
        private readonly List<UserId> userIds = new List<UserId>();

        public User GetOrCreateUser(UserId userId)
        {
            return memoryCache.GetOrCreate(userId, cacheEntry =>
            {
                User value = new() { Id = userId, Name = userId.name };
                cacheEntry
                    .SetValue(value)
                    .SetSlidingExpiration(TimeSpan.FromHours(2))
                    .RegisterPostEvictionCallback(OnEvict);
                userIds.Add(userId);
                return value;
            })!;
        }

        private void OnEvict(object key, object? value, EvictionReason reason, object? state)
        {
            if (key is UserId userId)
            {
                userIds.Remove(userId);
            }
        }

        public User? GetUser(UserId userId)
        {
            return memoryCache.Get(userId) as User;
        }

        public IEnumerable<User> GetUsers()
        {
            foreach (var userId in userIds)
            {
                yield return GetUser(userId)!;
            }
        }
    }

}