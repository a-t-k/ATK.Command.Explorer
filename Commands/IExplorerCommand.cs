namespace ATK.Command.Explorer.Commands;

/// <summary>
/// Marker-Interface für alle Commands des Command Explorers.
/// </summary>
/// <remarks>
/// <para>
/// Der Explorer ist ein eigenständiges Paket und kennt das Command-Interface der Anwendung
/// nicht. Deshalb bringt er eine eigene, minimale Command-Schicht mit: Jedes Explorer-Command
/// implementiert dieses Interface, der <c>ExplorerCommandDispatcher</c> findet den passenden
/// Handler über den DI-Container.
/// </para>
/// <para>
/// Der Typname wird im JSON-Feld <c>$type</c> übertragen – genau wie bei den Commands der
/// Anwendung. Die UI nutzt dafür denselben <c>CommandProcessor</c>.
/// </para>
/// </remarks>
public interface IExplorerCommand
{
}

/// <summary>
/// Ein Explorer-Command mit Ergebnis.
/// </summary>
/// <typeparam name="TResult">Typ des Ergebnisses.</typeparam>
/// <remarks>
/// Das Ergebnis wird als JSON zurückgegeben. Für Datei-Downloads ist
/// <see cref="Models.FileModel"/> vorgesehen – die UI erkennt es und speichert die Datei.
/// </remarks>
public interface IExplorerCommand<TResult> : IExplorerCommand
{
}

/// <summary>
/// Verarbeitet ein Explorer-Command.
/// </summary>
/// <typeparam name="TCommand">Typ des Commands.</typeparam>
/// <typeparam name="TResult">Typ des Ergebnisses.</typeparam>
/// <remarks>
/// Handler werden im DI-Container registriert. Der Dispatcher löst sie über
/// <c>GetRequiredService&lt;IExplorerCommandHandler&lt;TCommand, TResult&gt;&gt;()</c> auf.
/// </remarks>
public interface IExplorerCommandHandler<in TCommand, TResult>
    where TCommand : IExplorerCommand<TResult>
{
    /// <summary>
    /// Führt das Command aus.
    /// </summary>
    /// <param name="command">Das gebundene Command.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Das Ergebnis des Commands.</returns>
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
