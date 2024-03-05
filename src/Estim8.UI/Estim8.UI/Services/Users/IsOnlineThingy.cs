namespace Estim8.UI.Services.Users
{
    public class IsOnlineThingy(IUserProvider userProvider) : IDisposable
    {
        private User? User { get; set; }

        public async Task SetAsync()
        {
            if (User is null)
            {
                User = await userProvider.GetUserAsync();
                User.IncreaseOnline();
            }
        }

        public void Dispose()
        {
            User?.DecreaseOnline();
            User = null;
        }
    }
}