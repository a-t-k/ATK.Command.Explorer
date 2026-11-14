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
/// </remarks>
internal static class ApiResponseFactory
{
    /// <summary>
    /// Verpackt ein Ergebnis als erfolgreiche Antwort.
    /// </summary>
    /// <param name="result">Das Ergebnis des Commands; darf <c>null</c> sein.</param>
    /// <returns>Die Hülle mit <c>status = "success"</c>.</returns>
    public static ApiResponse<object?> Success(object? result) =>
        ApiResponse<object?>.Success(result, string.Empty);

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
}
