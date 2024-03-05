using Microsoft.AspNetCore.Components.Authorization;

namespace Estim8.UI.Services.Users
{
    public class UserProvider : IUserProvider
    {
        private readonly AuthenticationStateProvider authenticationStateProvider;

        public UserProvider(AuthenticationStateProvider authenticationStateProvider)
        {
            this.authenticationStateProvider = authenticationStateProvider;
        }

        public async Task<User> GetUserAsync()
        {
            var authstate = await authenticationStateProvider.GetAuthenticationStateAsync();
            var user = authstate.User;
            var name = user.Identity.Name;

            return new User { Id = new UserId(name) };
        }
    }
}
