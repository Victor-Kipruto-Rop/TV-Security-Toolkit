using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace TVSecurityToolkit.App.Accessibility;

/// <summary>
/// Adds a conservative fallback automation name to interactive controls that lack one.
/// Explicit AutomationProperties.Name values always win; controls without a trustworthy label
/// are left untouched rather than assigned misleading names.
/// </summary>
public static class AutomationNameBehavior
{
    public static readonly DependencyProperty EnableAutoNamesProperty =
        DependencyProperty.RegisterAttached(
            "EnableAutoNames",
            typeof(bool),
            typeof(AutomationNameBehavior),
            new PropertyMetadata(false, OnEnableAutoNamesChanged));

    public static void SetEnableAutoNames(DependencyObject element, bool value) =>
        element.SetValue(EnableAutoNamesProperty, value);

    public static bool GetEnableAutoNames(DependencyObject element) =>
        (bool)element.GetValue(EnableAutoNamesProperty);

    private static void OnEnableAutoNamesChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not FrameworkElement root || args.NewValue is not true) return;
        root.Loaded += (_, _) => ApplyFallbackNames(root);
    }

    private static void ApplyFallbackNames(DependencyObject root)
    {
        var pending = new Stack<DependencyObject>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current is FrameworkElement element)
                SetFallbackName(element);

            if (current is not Visual && current is not System.Windows.Media.Media3D.Visual3D) continue;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++)
                pending.Push(VisualTreeHelper.GetChild(current, i));
        }
    }

    private static void SetFallbackName(FrameworkElement element)
    {
        if (AutomationProperties.GetName(element).Length > 0) return;
        if (element is not (ButtonBase or Selector or TextBoxBase or PasswordBox or RangeBase)) return;

        var candidate = ReadText(element.ToolTip)
            ?? ReadText(element.Tag)
            ?? (element is ContentControl content ? ReadText(content.Content) : null)
            ?? Humanize(element.Name);

        if (!string.IsNullOrWhiteSpace(candidate))
            AutomationProperties.SetName(element, candidate.Trim());
    }

    private static string? ReadText(object? value)
    {
        if (value is null) return null;
        if (value is string text && !string.IsNullOrWhiteSpace(text)) return text;
        if (value is TextBlock textBlock && !string.IsNullOrWhiteSpace(textBlock.Text)) return textBlock.Text;
        if (value is AccessText accessText && !string.IsNullOrWhiteSpace(accessText.Text)) return accessText.Text;
        if (value is ContentControl content) return ReadText(content.Content);
        if (value is Panel panel)
        {
            foreach (UIElement child in panel.Children)
            {
                var text = ReadText(child);
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
        }
        if (value is FrameworkElement element && element.ToolTip is string tip && !string.IsNullOrWhiteSpace(tip))
            return tip;
        return null;
    }

    private static string? Humanize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var words = Regex.Replace(name.Replace('_', ' '), "([a-z0-9])([A-Z])", "$1 $2").Trim();
        return words.Length == 0 ? null : words;
    }
}
