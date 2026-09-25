using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;

namespace PlataformaCreditos.Infrastructure;

public static partial class RedisExtensions
{
    public const string InstanceName = "PlataformaCreditos:";

    private const string FormatoEsperado = "redis://default:<clave>@<host>:<puerto>";

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
        options.ConnectTimeout = 10000;
        options.ClientName = "PlataformaCreditos";
        var servidor = string.Join(", ", options.EndPoints);
        var multiplexer = ConnectionMultiplexer.Connect(options);

        // Sin Redis la sesión y las cookies (Data Protection) quedan bloqueadas en cada request:
        // fuera de Development es preferible fallar al iniciar con un mensaje claro (sin exponer la clave).
        if (!multiplexer.IsConnected && !env.IsDevelopment())
        {
            multiplexer.Dispose();
            throw new InvalidOperationException(
                $"No se pudo conectar a Redis en {servidor}. Revisa Redis__ConnectionString (formato {FormatoEsperado}), " +
                "la contraseña y que la base de datos esté activa.");
        }

        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        services.AddStackExchangeRedisCache(o =>
        {
            o.InstanceName = InstanceName;
            o.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(multiplexer);
        });
        services.AddDataProtection()
            .SetApplicationName("PlataformaCreditos")
            .PersistKeysToStackExchangeRedis(multiplexer, InstanceName + "DataProtection-Keys");
        services.AddSingleton(new EstadoRedis(true, $"Redis ({servidor})"));
        return services;
    }

    /// <summary>
    /// Acepta el formato de StackExchange.Redis (<c>host:puerto,password=...,ssl=true</c>) y el URI que
    /// muestra Redis Cloud (<c>redis://usuario:clave@host:puerto</c> o <c>rediss://</c>). Tolera que se
    /// pegue el comando completo de la consola (<c>redis-cli -u redis://...</c>) y comillas alrededor.
    /// </summary>
    public static ConfigurationOptions ConvertirOpciones(string connectionString)
    {
        var valor = Normalizar(connectionString);

        if (!valor.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) &&
            !valor.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            if (valor.Contains(' ') || valor.Contains("://"))
            {
                throw new InvalidOperationException($"Redis__ConnectionString no tiene un formato válido. Usa {FormatoEsperado}.");
            }
            return ConfigurationOptions.Parse(valor);
        }

        if (!Uri.TryCreate(valor, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            throw new InvalidOperationException($"Redis__ConnectionString no tiene un formato válido. Usa {FormatoEsperado}.");
        }

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

    private static string Normalizar(string valor)
    {
        valor = valor.Trim().Trim('"', '\'').Trim();
        // "redis-cli -u redis://..." o "redis-cli redis://..." → "redis://..."
        valor = PrefijoRedisCli().Replace(valor, string.Empty);
        return valor.Trim().Trim('"', '\'').Trim();
    }

    [GeneratedRegex(@"^redis-cli\s+(?:-u\s+)?", RegexOptions.IgnoreCase)]
    private static partial Regex PrefijoRedisCli();
}

/// <summary>Indica qué backend respalda la caché y la sesión (se muestra como evidencia en la UI).</summary>
public record EstadoRedis(bool EsRedis, string Descripcion);
