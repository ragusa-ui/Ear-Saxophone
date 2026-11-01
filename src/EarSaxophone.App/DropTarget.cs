using System.Windows;

namespace EarSaxophone.App;

/// <summary>Marks the element in the device picture on which a dragged mapping would land (the picture draws a dashed ring).</summary>
public static class DropTarget
{
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.RegisterAttached("IsActive", typeof(bool), typeof(DropTarget), new PropertyMetadata(false));

    public static bool GetIsActive(DependencyObject element) => (bool)element.GetValue(IsActiveProperty);

    public static void SetIsActive(DependencyObject element, bool value) => element.SetValue(IsActiveProperty, value);
}
