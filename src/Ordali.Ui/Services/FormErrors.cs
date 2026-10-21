using Microsoft.AspNetCore.Components.Forms;
using Ordali.Client;

namespace Ordali.Ui.Services;

public static class FormErrors
{
    /// <summary>
    /// Puts the API's field errors next to their inputs. Returns false when the error has no
    /// field errors, so the caller shows it as a banner instead.
    /// </summary>
    public static bool Show(ApiException error, EditContext context, ValidationMessageStore store)
    {
        if (error.Errors.Count == 0)
        {
            return false;
        }

        foreach (var (field, messages) in error.Errors)
        {
            // ASP.NET Core returns camelCase keys ("sku"); the model properties are PascalCase.
            var name = field.Length == 0 ? field : char.ToUpperInvariant(field[0]) + field[1..];
            store.Add(context.Field(name), messages);
        }

        context.NotifyValidationStateChanged();
        return true;
    }
}
