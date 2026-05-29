using StackExchange.Redis;
using System.Runtime.CompilerServices;

internal static class AssemblyInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // S'exécute une seule fois au chargement de l'assembly
        ConfigureRedisForTests();
    }

    private static void ConfigureRedisForTests()
    {
        try
        {
            var connection = ConnectionMultiplexer.Connect("localhost:6379");
            var server = connection.GetServer("localhost", 6379);

            server.Execute("CONFIG", "SET", "save", "");
            server.Execute("CONFIG", "SET", "appendonly", "no");
            server.Execute("CONFIG", "SET", "stop-writes-on-bgsave-error", "no");
        }
        catch
        {
            // Redis pas encore dispo — les tests s'occuperont de ça
        }
    }
}