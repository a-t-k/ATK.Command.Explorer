using ATK.Command.Communication.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ATK.Command.Explorer.Binding;

/// <summary>
/// Packt Command-Ergebnisse in eine <see cref="ApiResponse{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Alle Antworten des Explorers – Erfolg wie Fehler – laufen über dieselbe Hülle aus
/// <c>ATK.Command.Communication.Models</c>. Die UI wertet damit einheitlich
/// <c>status</c>, <c>message</c> und <c>notifications</c> aus, statt für jeden Fall eine
/// eigene Struktur zu kennen.
/// </para>
/// <para>
/// Die Hülle wird als <see cref="ApiResponse{T}"/> von <see cref="object"/> gebaut. Das
/// genügt, weil der Serialisierer das Ergebnis anhand seines Laufzeit-Typs schreibt – der
/// Ergebnistyp ist zur Compile-Zeit ohnehin unbekannt (der Dispatcher arbeitet über
/// Reflection).
/// </para>
/// <para>
/// Die Klasse ist öffentlich, damit auch der Command-Endpunkt der Anwendung dieselbe
/// Hülle bauen kann. Liefert ein Command bereits eine <see cref="ApiResponse{T}"/>, wird
/// sie unverändert durchgereicht – kein doppeltes Verpacken.
/// </para>
/// </remarks>
public static class ApiResponseFactory
{
    /// <summary>
    /// Verpackt ein Ergebnis als erfolgreiche Antwort.
    /// </summary>
    /// <param name="result">Das Ergebnis des Commands; darf <c>null</c> sein.</param>
    /// <returns>
    /// Die Hülle mit <c>status = "success"</c> – oder <paramref name="result"/> selbst,
    /// wenn es bereits eine <see cref="ApiResponse{T}"/> ist.
    /// </returns>
    public static object Success(object? result)
    {
        if (IsApiResponse(result))
        {
            return result!;
        }

        return ApiResponse<object?>.Success(result, string.Empty);
    }

    /// <summary>
    /// Verpackt einen Fehler als Antwort.
    /// </summary>
    /// <param name="status">Zustand: <c>fail</c>, <c>error</c> oder <c>forbidden</c>.</param>
    /// <param name="message">Die Meldung für die UI.</param>
    /// <param name="notifications">Optionale Hinweise, etwa zu einzelnen Feldern.</param>
    /// <returns>Die Hülle mit dem angegebenen Zustand.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Wird geworfen, wenn <paramref name="status"/> unbekannt ist.
    /// </exception>
    public static ApiResponse<object?> Failure(
        string status,
        string message,
        IEnumerable<Notification>? notifications = null)
    {
        var response = status switch
        {
            "fail" => ApiResponse<object?>.Fail(null, message),
            "error" => ApiResponse<object?>.Error(null, message),
            "forbidden" => ApiResponse<object?>.Forbidden(null, message),
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unbekannter Zustand. Erlaubt sind 'fail', 'error' und 'forbidden'."),
        };

        if (notifications is not null)
        {
            response.Notifications.AddRange(notifications);
        }

        return response;
    }

    /// <summary>
    /// Baut aus dem <see cref="ModelStateDictionary"/> Hinweise für die UI.
    /// </summary>
    /// <param name="modelState">Der ModelState der Anfrage.</param>
    /// <returns>Ein Hinweis pro Fehler, mit dem Feldnamen als Titel.</returns>
    public static IEnumerable<Notification> FromModelState(ModelStateDictionary modelState)
    {
        ArgumentNullException.ThrowIfNull(modelState);

        foreach (var entry in modelState)
        {
            foreach (var error in entry.Value.Errors)
            {
                var text = string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? error.Exception?.Message ?? "Ungültiger Wert."
                    : error.ErrorMessage;

                yield return Notification.Error(text, entry.Key);
            }
        }
    }

    /// <summary>
    /// Prüft, ob ein Wert bereits eine <see cref="ApiResponse{T}"/> ist.
    /// </summary>
    /// <param name="value">Der zu prüfende Wert.</param>
    /// <returns><c>true</c>, wenn der Typ <see cref="ApiResponse{T}"/> ist.</returns>
    public static bool IsApiResponse(object? value)
    {
        if (value is null)
        {
            return false;
        }

        var type = value.GetType();

        return type.IsGenericType
               && type.GetGenericTypeDefinition() == typeof(ApiResponse<>);
    }
}
