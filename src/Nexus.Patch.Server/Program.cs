using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexus.Patch.Server.Services;

namespace Nexus.Patch.Server;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = CreateWebApplication(args);

        builder.Services.AddControllers();
        
        builder.Services.AddActivatedSingleton<IGameDataFiles, GameDataFiles>();
        
        var app = builder.Build();
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        
        app.UseRouting();
        app.MapControllers();
        await app.RunAsync();
    }
    
    public static WebApplicationBuilder CreateWebApplication(string[] args) 
        => WebApplication.CreateBuilder(args);
}