﻿
using Blazored.LocalStorage;

namespace Estiblazor.UI.Services.Users
{

    public class LocalStorageUserProvider : IUserProvider, IDisposable
    {
        private readonly ILocalStorageService localStorageService;
        private readonly IUserCollection userCollection;

        private User? cachedUser;

        public LocalStorageUserProvider(ILocalStorageService localStorageService, IUserCollection userCollection)
        {
            this.localStorageService = localStorageService;
            this.userCollection = userCollection;
        }

        private async Task<UserId?> TryGetFromLocalStorage()
        {
            try
            {
                return await localStorageService.GetItemAsync<UserId>("userid");
            }
            catch
            {
                return null;
            }
        }

        public async Task InitUser(UserId userId)
        {
            if(cachedUser?.Id == userId)
            {
                return;
            }
            this.cachedUser = userCollection.GetOrCreateUser(userId);
            await localStorageService.SetItemAsync("userid", userId);
        }

        public async Task LogoutAsync()
        {
            if (cachedUser is null) return;
            cachedUser.SetRoom(null);
            cachedUser = null;
            await localStorageService.RemoveItemAsync("userid");
        }

        public async Task<User?> GetUserAsync()
        {
            if (cachedUser is not null)
            {
                return cachedUser;
            }

            if (await TryGetFromLocalStorage() is { } localUserId)
            {
                cachedUser = userCollection.GetOrCreateUser(localUserId);
                return cachedUser; 
            }

            return null;
        }

        public void Dispose()
        {

        }
    }
}