
namespace Estim8.UI.Services.Users
{
    public interface IUserCollection
    {
        User GetOrCreateUser(UserId userId);
        User? GetUser(UserId userId);
        IEnumerable<User> GetUsers();
    }
}