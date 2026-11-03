using System.Windows;
using System.Windows.Controls;

namespace ModernNotepad;

public sealed record DialogAction(string Label, MessageBoxResult Result, bool IsDefault = false, bool IsCancel = false);

/// <summary>Provides accessible modal prompts using the owner's palette instead of native white message boxes.</summary>
internal sealed class AppDialog : Window
{
    public MessageBoxResult Choice { get; private set; } = MessageBoxResult.Cancel;

    /// <summary>Creates a keyboard-accessible dialog with explicit save, discard and cancellation actions.</summary>
    public AppDialog(Window owner, bool dark, string title, string message, params DialogAction[] actions)
    {
        Owner = owner; Title = title; Width = 460; MinWidth = 360;
        SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        WindowTheme.PrepareDialog(this, owner, dark);
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 14, Margin = new Thickness(0, 0, 0, 24) });
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var action in actions)
        {
            var button = new Button { Content = action.Label, IsDefault = action.IsDefault, IsCancel = action.IsCancel,
                MinWidth = 90, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(12, 7, 12, 7) };
            button.Click += (_, _) => { Choice = action.Result; DialogResult = action.Result is not MessageBoxResult.Cancel; };
            buttons.Children.Add(button);
        }
        panel.Children.Add(buttons); Content = panel;
    }
}
