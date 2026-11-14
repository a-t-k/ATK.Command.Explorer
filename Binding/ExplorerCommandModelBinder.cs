using System.Reflection;
using System.Text.Json;
using ATK.Command.Explorer.Commands;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

namespace ATK.Command.Explorer.Binding;

/// <summary>
/// Bindet ein Explorer-Command anhand des <c>$type</c>-Feldes im JSON-Body.
/// </summary>
/// <remarks>
/// <para>
/// Der Binder liest den Body, ermittelt aus <c>$type</c> den konkreten Typ und erzeugt eine
/// Instanz. Der Typ muss <see cref="IExplorerCommand"/> implementieren und in einer der
/// freigegebenen Assemblies liegen.
/// </para>
/// <para>
/// Anders als der ModelBinder der Anwendung prüft dieser Binder keine Authentifizierung –
/// die übernimmt der Explorer-Controller über die gewählte
/// <c>CommandExplorerAuthentication</c>.
/// </para>
/// </remarks>
public sealed class ExplorerCommandModelBinder : IModelBinder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IReadOnlyList<Assembly> assemblies;

    /// <summary>
    /// Erzeugt den Binder.
    /// </summary>
    /// <param name="assemblies">Assemblies, in denen Command-Typen gesucht werden.</param>
    public ExplorerCommandModelBinder(IReadOnlyList<Assembly> assemblies)
    {
        this.assemblies = assemblies;
    }

    /// <inheritdoc />
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        // Body ist nicht wiederholbar – einmal lesen und puffern.
        using StreamReader reader = new(bindingContext.HttpContext.Request.Body);
        string json = await reader.ReadToEndAsync(bindingContext.HttpContext.RequestAborted);

        if (string.IsNullOrWhiteSpace(json))
        {
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        string? typeName;

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);

            typeName = document.RootElement.TryGetProperty("$type", out JsonElement typeElement)
                ? typeElement.GetString()
                : null;
        }
        catch (JsonException exception)
        {
            bindingContext.ModelState.AddModelError(
                bindingContext.ModelName,
                $"Ungültiges JSON: {exception.Message}");
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        if (string.IsNullOrWhiteSpace(typeName))
        {
            bindingContext.ModelState.AddModelError(
                bindingContext.ModelName,
                "Das Feld '$type' fehlt. Es muss den vollqualifizierten Typnamen enthalten.");
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        Type? commandType = this.ResolveType(typeName);

        if (commandType is null)
        {
            bindingContext.ModelState.AddModelError(
                bindingContext.ModelName,
                $"Der Typ '{typeName}' wurde nicht gefunden oder ist kein Explorer-Command.");
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        object? command;

        try
        {
            command = JsonSerializer.Deserialize(json, commandType, SerializerOptions);
        }
        catch (JsonException exception)
        {
            bindingContext.ModelState.AddModelError(
                bindingContext.ModelName,
                $"Der Body konnte nicht gelesen werden: {exception.Message}");
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        if (command is null)
        {
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        bindingContext.Result = ModelBindingResult.Success(command);
    }

    /// <summary>
    /// Sucht den Typ anhand des <c>$type</c>-Werts.
    /// </summary>
    /// <remarks>
    /// Akzeptiert <c>"Namespace.Typ, Assembly"</c> und <c>"Namespace.Typ"</c>. Der Typ muss
    /// <see cref="IExplorerCommand"/> implementieren.
    /// </remarks>
    private Type? ResolveType(string typeName)
    {
        string fullName = typeName;
        string? assemblyName = null;

        int separator = typeName.IndexOf(',', StringComparison.Ordinal);

        if (separator > 0)
        {
            fullName = typeName[..separator].Trim();
            assemblyName = typeName[(separator + 1)..].Trim();
        }

        IEnumerable<Assembly> candidates = assemblyName is null
            ? this.assemblies
            : this.assemblies.Where(assembly =>
                string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal));

        foreach (Assembly assembly in candidates)
        {
            Type? type = assembly.GetType(fullName, throwOnError: false);

            if (type is not null && typeof(IExplorerCommand).IsAssignableFrom(type))
            {
                return type;
            }
        }

        return null;
    }
}

/// <summary>
/// Liefert den <see cref="ExplorerCommandModelBinder"/> für Explorer-Commands.
/// </summary>
/// <remarks>
/// Wird in <c>MvcOptions.ModelBinderProviders</c> an erster Stelle eingetragen. Er greift nur
/// bei Parametern vom Typ <see cref="IExplorerCommand"/> – andere Parameter bleiben unberührt.
/// </remarks>
public sealed class ExplorerCommandModelBinderProvider : IModelBinderProvider
{
    private readonly IReadOnlyList<Assembly> assemblies;

    /// <summary>
    /// Erzeugt den Provider.
    /// </summary>
    /// <param name="assemblies">Assemblies, in denen Command-Typen gesucht werden.</param>
    public ExplorerCommandModelBinderProvider(IReadOnlyList<Assembly> assemblies)
    {
        this.assemblies = assemblies;
    }

    /// <inheritdoc />
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Metadata.ModelType == typeof(IExplorerCommand)
            ? new ExplorerCommandModelBinder(this.assemblies)
            : null;
    }
}
