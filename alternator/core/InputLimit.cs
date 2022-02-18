using System.Windows.Controls;

namespace guildwars2.tools.alternator;

public static class InputLimit
{
    public static string GetDecimalValueProxy(TextBox obj) => (string)obj.GetValue(DecimalValueProxyProperty);

    public static void SetDecimalValueProxy(TextBox obj, string value) => obj.SetValue(DecimalValueProxyProperty, value);

    // Using a DependencyProperty as the backing store for DecimalValueProxy.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty DecimalValueProxyProperty =
        DependencyProperty.RegisterAttached("DecimalValueProxy", typeof(string), typeof(InputLimit),
            new FrameworkPropertyMetadata("0", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, null, CoerceDecimalValueProxy));

    private static object CoerceDecimalValueProxy(DependencyObject d, object baseValue)
    {
        return decimal.TryParse(baseValue as string, out _) ? baseValue : DependencyProperty.UnsetValue;
    }
}