using HackathonStarter.Services;
using MudBlazor;

namespace HackathonStarter.Components.Common;

public static class DialogExtensions
{
    /// <summary>"Are you sure?" dialog. Returns true when confirmed.</summary>
    public static async Task<bool> ConfirmAsync(this IDialogService dialogs, string title, string message, string? confirm = null, bool destructive = false)
    {
        var result = await dialogs.ShowMessageBoxAsync(new MessageBoxOptions
        {
            Title = title,
            Message = message,
            YesText = confirm ?? Lang.T("Confirm"),
            CancelText = Lang.T("Cancel"),
        }, new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true });
        return result == true;
    }
}
