using Blazored.LocalStorage;
using Estiblazor.UI.Application.Rooms;
using Estiblazor.UI.Components;
using Estiblazor.UI.Domain.Common;
using Estiblazor.UI.Domain.Rooms;
using Estiblazor.UI.Infrastructure.Messaging;
using Estiblazor.UI.Infrastructure.Rooms;
using Estiblazor.UI.Services.Users;
using StackExchange.Redis;

namespace Estiblazor.UI
{

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

            builder.Services
                .AddMemoryCache()
                .AddHttpContextAccessor()
                .AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString))
                .AddSingleton<IRoomRepository, RedisRoomRepository>()
                .AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>()
                .AddSingleton<IRoomEventBackplane, RedisRoomEventBackplane>()
                .AddSingleton<IRoomOrchestrationService, RoomOrchestrationService>()
                .AddSingleton<IUserCollection, UserCollection>()
                .AddScoped<IRoomCreationService, RoomCreationService>()
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
