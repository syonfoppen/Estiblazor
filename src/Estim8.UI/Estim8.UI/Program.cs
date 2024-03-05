using Blazored.LocalStorage;
using Estim8.UI.Components;
using Estim8.UI.Services;
using Estim8.UI.Services.Rooms;
using Estim8.UI.Services.Users;

namespace Estim8.UI
{

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services
                .AddSingleton<CounterService>()
                .AddMemoryCache()
                .AddHttpContextAccessor()
                .AddSingleton<IRoomCollection, RoomCollection>()
                .AddSingleton<IUserCollection, UserCollection>()
                .AddScoped<IUserProvider, LocalStorageUserProvider>()
                .AddScoped<IsOnlineThingy>()
                .AddBlazoredLocalStorage()
                .AddRazorComponents()
                .AddInteractiveServerComponents();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();
            app.UseAntiforgery();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
