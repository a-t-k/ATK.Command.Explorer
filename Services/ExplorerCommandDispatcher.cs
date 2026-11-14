using System.Collections.Concurrent;
using System.Reflection;
using ATK.Command.Explorer.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace ATK.Command.Explorer.Services;

/// <summary>
/// Führt Explorer-Commands aus, indem er den passenden Handler im DI-Container auflöst.
/// </summary>
/// <remarks>
/// <para>
/// Der Dispatcher ist bewusst schlank: Er kennt nur <see cref="IExplorerCommand{TResult}"/>
/// und <see cref="IExplorerCommandHandler{TCommand,TResult}"/>. Dadurch bleibt das Paket
/// unabhängig von einem bestimmten Mediator.
/// </para>
/// <para>
/// Die Reflection-Ergebnisse werden pro Command-Typ zwischengespeichert, damit die
/// Auflösung nicht bei jeder Anfrage erneut stattfindet.
/// </para>
/// </remarks>
public sealed class ExplorerCommandDispatcher
{
    private static readonly ConcurrentDictionary<Type, CommandInvoker> Invokers = new();

    private readonly IServiceProvider services;

    /// <summary>
    /// Erzeugt den Dispatcher.
    /// </summary>
    /// <param name="services">Der Service-Provider der Anwendung.</param>
    public ExplorerCommandDispatcher(IServiceProvider services)
    {
        this.services = services;
    }

    /// <summary>
    /// Führt ein Command aus.
    /// </summary>
    /// <param name="command">Das gebundene Command.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Das Ergebnis des Commands.</returns>
    /// <exception cref="InvalidOperationException">
    /// Wird geworfen, wenn für den Command-Typ kein Handler registriert ist.
    /// </exception>
    public Task<object?> ExecuteAsync(object command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invoker = Invokers.GetOrAdd(command.GetType(), CreateInvoker);

        return invoker(this.services, command, cancellationToken);
    }

    /// <summary>
    /// Baut den Aufruf für einen Command-Typ.
    /// </summary>
    /// <remarks>
    /// Ermittelt aus <see cref="IExplorerCommand{TResult}"/> den Ergebnistyp und daraus den
    /// Handler-Typ. Der Aufruf selbst läuft über <c>dynamic</c>, damit die generischen
    /// Parameter nicht zur Compile-Zeit bekannt sein müssen.
    /// </remarks>
    private static CommandInvoker CreateInvoker(Type commandType)
    {
        var resultType = commandType
            .GetInterfaces()
            .Where(type => type.IsGenericType
                           && type.GetGenericTypeDefinition() == typeof(IExplorerCommand<>))
            .Select(type => type.GetGenericArguments()[0])
            .FirstOrDefault();

        if (resultType is null)
        {
            throw new InvalidOperationException(
                $"Der Typ '{commandType.FullName}' implementiert "
                + $"{nameof(IExplorerCommand)}<TResult> nicht und kann nicht ausgeführt werden.");
        }

        var handlerType = typeof(IExplorerCommandHandler<,>).MakeGenericType(commandType, resultType);

        var handleMethod = handlerType.GetMethod(nameof(IExplorerCommandHandler<IExplorerCommand<object>, object>.HandleAsync))
                                  ?? throw new InvalidOperationException(
                                      $"Am Handler-Typ '{handlerType.FullName}' wurde keine "
                                      + $"'{nameof(IExplorerCommandHandler<IExplorerCommand<object>, object>.HandleAsync)}'-"
                                      + "Methode gefunden.");

        return async (services, command, cancellationToken) =>
        {
            var handler = services.GetService(handlerType)
                              ?? throw new InvalidOperationException(
                                  $"Für das Command '{commandType.Name}' ist kein Handler registriert. "
                                  + $"Registriere '{handlerType.Name}' im DI-Container.");

            var task = handleMethod.Invoke(handler, [command, cancellationToken]);

            if (task is not Task resultTask)
            {
                throw new InvalidOperationException(
                    $"Der Handler für '{commandType.Name}' hat kein Task-Ergebnis geliefert.");
            }

            await resultTask.ConfigureAwait(false);

            return resultTask.GetType().GetProperty("Result")?.GetValue(resultTask);
        };
    }

    /// <summary>
    /// Signatur des zwischengespeicherten Aufrufs.
    /// </summary>
    private delegate Task<object?> CommandInvoker(
        IServiceProvider services,
        object command,
        CancellationToken cancellationToken);
}
