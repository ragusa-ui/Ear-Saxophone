using System.Windows;
using System.Windows.Controls;
using EarSaxophone.Core.Devices;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.App;

/// <summary>Chooses the drawing of a control in the device picture: knob, fader or key.</summary>
public sealed class ElementTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Knob { get; set; }

    public DataTemplate? Fader { get; set; }

    public DataTemplate? Button { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item is ControllerElementViewModel element
            ? element.Kind switch
            {
                ElementKind.Knob => Knob,
                ElementKind.Fader => Fader,
                _ => Button,
            }
            : base.SelectTemplate(item, container);
}
