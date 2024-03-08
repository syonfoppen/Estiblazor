
using Estiblazor.UI.Services.Collections;

namespace Estiblazor.UI.Services.Users
{
    public interface IUserCollection : IMemoryCollection<User, UserId>
    {
        User GetOrCreateUser(UserId userId);
        User? GetUser(UserId userId);

    }
}