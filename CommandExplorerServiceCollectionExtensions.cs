using System.Reflection;
using ATK.Command.Explorer.Authorization;
using ATK.Command.Explorer.Binding;
using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Configuration;
using ATK.Command.Explorer.Controller;
using ATK.Command.Explorer.Documentation;
using ATK.Command.Explorer.Framework;
using ATK.Command.Explorer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ATK.Command.Explorer;

/// <summary>
/// Registrierung des Command Explorers in der Anwendung.
/// </summary>
public static class CommandExplorerServiceCollectionExtensions
{
    /// <summary>
    /// Registriert den Command Explorer für die angegebene Assembly.
    /// </summary>
    /// <typeparam name="TMarker">
    /// Typ aus der Assembly, die nach Commands durchsucht wird. Üblicherweise ein Marker-Typ
    /// der Anwendung, z. B. <c>Program</c> oder ein eigener <c>record</c>.
    /// </typeparam>
    /// <typeparam name="TCommand">
    /// Marker-Interface, das alle Commands implementieren. Bestimmt, welche Typen als
    /// Commands gelten.
    /// </typeparam>
    /// <param name="services">Die Service-Sammlung.</param>
    /// <param name="configure">Optionale Konfiguration der Explorer-Optionen.</param>
    /// <example>
    /// <code>
    /// builder.Services.AddCommandExplorer&lt;Program, IRequestCommand&gt;(options =>
    /// {
    ///     options.NamespacePrefixes.Add("MyCompany.MyProduct");
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddCommandExplorer<TMarker, TCommand>(
        this IServiceCollection services,
        Action<CommandExplorerOptions>? configure = null)
        where TCommand : class
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddCommandExplorer(
            typeof(TMarker).Assembly,
            typeof(TCommand),
            configure);
    }

    /// <summary>
    /// Registriert den Command Explorer für eine Assembly.
    /// </summary>
    /// <param name="services">Die Service-Sammlung.</param>
    /// <param name="assembly">Assembly, die nach Commands durchsucht wird.</param>
    /// <param name="commandInterface">Marker-Interface, das alle Commands implementieren.</param>
    /// <param name="configure">Optionale Konfiguration der Explorer-Optionen.</param>
    public static IServiceCollection AddCommandExplorer(
        this IServiceCollection services,
        Assembly assembly,
        Type commandInterface,
        Action<CommandExplorerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(commandInterface);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton(provider => new CommandMetadataProvider(
            assembly,
            commandInterface,
            provider.GetRequiredService<IOptions<CommandExplorerOptions>>()));

        // Authentifizierungen: löst die vom Aufrufer gewählte Id auf und baut die Header.
        // Der Token bleibt im Backend – die UI sieht nur Id, Name und Type.
        services.AddSingleton<CommandAuthenticationResolver>();

        // Fehlerhafte Einträge fallen so beim Start auf, nicht erst beim Ausführen.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<CommandExplorerOptions>,
                CommandExplorerOptionsValidator>());

        // Command-Schicht des Explorers: eigener Dispatcher, eigener ModelBinder.
        // Der Explorer kennt das Command-Interface der Anwendung nicht und bringt deshalb
        // eine minimale eigene Infrastruktur mit.
        services.AddSingleton<ExplorerCommandDispatcher>();
        services.AddSingleton<IReadOnlyList<Assembly>>(_ => [typeof(CommandExplorerController).Assembly]);

        // Für den Command-Proxy: Basis-URL aus der aktuellen Anfrage ermitteln.
        services.AddHttpContextAccessor();
        services.AddHttpClient();

        // Alle Explorer-Commands und ihre Handler registrieren.
        services.AddExplorerCommands();

        // Dokumentations-Generatoren
        services.AddSingleton<MarkdownVaultGenerator>();
        services.AddSingleton(provider => new CommandPdfGenerator(
            new CommandPdfFontResolver(
                provider.GetRequiredService<IOptions<CommandExplorerOptions>>()
                    .Value.FontDirectory)));
        services.AddSingleton(_ => new TypeScriptExportGenerator(assembly, commandInterface));
        services.AddSingleton<FrameworkTemplateProvider>();

        // Autorisierung: Die Policy wird zur Laufzeit aus den Optionen gebaut und per
        // Controller-Konvention an den Explorer gehängt. Attribute könnten keine
        // Konfigurationswerte aufnehmen.
        services.AddAuthorization();
        services.TryAddEnumerable(
            ServiceDescriptor.Transient<IAuthorizationHandler, CommandExplorerClaimHandler>());

        services.AddOptions<MvcOptions>()
            .Configure<IOptionsMonitor<CommandExplorerOptions>,
                IOptionsMonitor<AuthorizationOptions>,
                IOptionsMonitor<AuthenticationOptions>>((mvc, explorer, authorization, authentication) =>
            {
                mvc.Conventions.Add(new CommandExplorerAuthorizationConvention(
                    explorer,
                    authorization,
                    authentication));

                // Der Binder greift nur bei Parametern vom Typ IExplorerCommand.
                mvc.ModelBinderProviders.Insert(0, new ExplorerCommandModelBinderProvider(
                    [typeof(CommandExplorerController).Assembly]));
            });

        return services;
    }

    /// <summary>
    /// Registriert alle Explorer-Commands und ihre Handler.
    /// </summary>
    /// <param name="services">Die Service-Sammlung.</param>
    /// <returns>Die Service-Sammlung, damit sich Aufrufe verketten lassen.</returns>
    /// <remarks>
    /// Die Registrierung läuft über Reflection: Jeder Typ, der
    /// <see cref="IExplorerCommandHandler{TCommand,TResult}"/> implementiert, wird als
    /// transienter Dienst eingetragen. Neue Commands brauchen dadurch keine manuelle
    /// Registrierung.
    /// </remarks>
    private static IServiceCollection AddExplorerCommands(this IServiceCollection services)
    {
        var assembly = typeof(IExplorerCommand).Assembly;

        var handlerTypes = assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                           && type.GetInterfaces().Any(@interface =>
                               @interface.IsGenericType
                               && @interface.GetGenericTypeDefinition()
                               == typeof(IExplorerCommandHandler<,>)));

        foreach (var handlerType in handlerTypes)
        {
            var serviceType = handlerType.GetInterfaces().First(@interface =>
                @interface.IsGenericType
                && @interface.GetGenericTypeDefinition() == typeof(IExplorerCommandHandler<,>));

            services.TryAddTransient(serviceType, handlerType);
        }

        return services;
    }
}
