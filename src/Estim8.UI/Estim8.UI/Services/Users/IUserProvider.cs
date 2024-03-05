namespace Estim8.UI.Services.Users
{
    public interface IUserProvider
    {
        Task<User> GetUserAsync();
    }

}