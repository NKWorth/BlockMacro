using System.Windows;
using System.Windows.Controls;
using BlockMacro.Models;

namespace BlockMacro;

public sealed class BlockTemplateSelector : DataTemplateSelector
{
    public DataTemplate? DefaultTemplate { get; set; }

    public DataTemplate? ContinueUntilTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        => item is ContinueUntilBlock
            ? ContinueUntilTemplate ?? DefaultTemplate
            : DefaultTemplate;
}
