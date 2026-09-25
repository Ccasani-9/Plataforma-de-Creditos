using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;

namespace PlataformaCreditos.Infrastructure;

public static class RedisExtensions
{
    public const string InstanceName = "PlataformaCreditos:";

    /// <summary>
    /// Registra IDistributedCache (Redis) para Session y Cache, y guarda en Redis las llaves de
    /// Data Protection para que las cookies de login/sesión sobrevivan a reinicios y despliegues.
    /// Sin <c>Redis:ConnectionString</c> se usa caché en memoria (solo para desarrollo local).
    /// </summary>
    public static IServiceCollection AddRedisInfraestructura(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
    {
        var connectionString = configuration["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (!env.IsDevelopment())
            {
                throw new InvalidOperationException("Falta la variable de entorno Redis__ConnectionString.");
            }

            services.AddDistributedMemoryCache();
            services.AddSingleton(new EstadoRedis(false, "Memoria (Redis no configurado)"));
            return services;
        }

        var options = ConvertirOpciones(connectionString);
        options.AbortOnConnectFail = false;
        options.ClientName = "PlataformaCreditos";
        var multiplexer = ConnectionMultiplexer.Connect(options);

        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        services.AddStackExchangeRedisCache(o =>
        {
            o.InstanceName = InstanceName;
            o.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(multiplexer);
        });
        services.AddDataProtection()
            .SetApplicationName("PlataformaCreditos")
            .PersistKeysToStackExchangeRedis(multiplexer, InstanceName + "DataProtection-Keys");
        services.AddSingleton(new EstadoRedis(true, $"Redis ({string.Join(", ", options.EndPoints)})"));
        return services;
    }

    /// <summary>
    /// Acepta tanto el formato de StackExchange.Redis (<c>host:puerto,password=...,ssl=true</c>)
    /// como el URI que muestra Redis Cloud (<c>redis://usuario:clave@host:puerto</c> o <c>rediss://</c>).
    /// </summary>
    public static ConfigurationOptions ConvertirOpciones(string connectionString)
    {
        if (!connectionString.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            return ConfigurationOptions.Parse(connectionString);
        }

        var uri = new Uri(connectionString);
        var options = new ConfigurationOptions
        {
            Ssl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase)
        };
        options.EndPoints.Add(uri.Host, uri.IsDefaultPort || uri.Port <= 0 ? 6379 : uri.Port);

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var partes = uri.UserInfo.Split(':', 2);
            if (partes.Length == 2)
            {
                options.User = Uri.UnescapeDataString(partes[0]);
                options.Password = Uri.UnescapeDataString(partes[1]);
            }
            else
            {
                options.Password = Uri.UnescapeDataString(partes[0]);
            }
        }

        return options;
    }
}

/// <summary>Indica qué backend respalda la caché y la sesión (se muestra como evidencia en la UI).</summary>
public record EstadoRedis(bool EsRedis, string Descripcion);
