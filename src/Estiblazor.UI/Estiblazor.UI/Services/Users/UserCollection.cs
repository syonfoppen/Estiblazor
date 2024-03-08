
using Estiblazor.UI.Services.Collections;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Estiblazor.UI.Services.Users
{

    public class UserCollection(IMemoryCache memoryCache, IOptions<MemoryCollectionOptions<User>> options)
        : MemoryCollection<User, UserId>(memoryCache, options), IUserCollection
    {

        public User GetOrCreateUser(UserId userId)
        {
            return GetOrCreate(userId, () => new User { Id = userId, Name = userId.name });
        }

        public User? GetUser(UserId userId)
        {
            return base.GetItem(userId);
        }
    }
}