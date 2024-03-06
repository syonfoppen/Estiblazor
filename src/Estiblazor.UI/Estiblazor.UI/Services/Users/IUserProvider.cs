namespace Estiblazor.UI.Services.Users
{
    public interface IUserProvider
    {
        Task<User?> GetUserAsync();
        Task InitUser(UserId userId);
        Task LogoutAsync();
    }

}