using System.Security.Claims;
using Estiblazor.Application;
using Microsoft.AspNetCore.Components.Authorization;

namespace Estiblazor.UI.Services;

public sealed class BrowserSession(AuthenticationStateProvider authentication) : IBrowserSession
{
    public async Task<Guid> GetUserIdAsync() =>
        Guid.Parse(
            (await authentication.GetAuthenticationStateAsync()).User.FindFirstValue(
                ClaimTypes.NameIdentifier
            )!
        );
}
