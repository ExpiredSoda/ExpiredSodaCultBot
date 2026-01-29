using CultBot.Extensions;
using CultBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine("=== Starting ExpiredSodaCultBot ===");

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                {
                    try
                    {
                        Console.WriteLine("Configuring services...");
                        services.AddCultBotDatabase();
                        services.AddCultBotServices();
                        Console.WriteLine("✓ All services configured");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("ERROR during service configuration:");
                        Console.WriteLine($"  Type: {ex.GetType().Name}");
                        Console.WriteLine($"  Message: {ex.Message}");
                        throw;
                    }
                })
                .Build();

            Console.WriteLine("✓ Host built successfully");
            Console.WriteLine("Starting host...");

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("=== FATAL STARTUP ERROR ===");
            Console.WriteLine($"Exception Type: {ex.GetType().FullName}");
            Console.WriteLine($"Message: {ex.Message}");
            Console.WriteLine($"Source: {ex.Source}");
            Console.WriteLine($"StackTrace:\n{ex.StackTrace}");

            if (ex.InnerException != null)
            {
                Console.WriteLine("\n=== INNER EXCEPTION ===");
                Console.WriteLine($"Type: {ex.InnerException.GetType().FullName}");
                Console.WriteLine($"Message: {ex.InnerException.Message}");
                Console.WriteLine($"StackTrace:\n{ex.InnerException.StackTrace}");
            }

            throw;
        }
    }
}
