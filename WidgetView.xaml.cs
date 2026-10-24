using System.Windows;
using System.Windows.Controls;

namespace Mote;

public partial class WidgetView : UserControl
{
    public WidgetView()
    {
        InitializeComponent();
        SetMachineName(Environment.MachineName);
    }

    public void SetMachineName(string name)
    {
        NameLabel.Text = name;
        TooltipName.Text = name;
        System.Windows.Automation.AutomationProperties.SetName(CopyButton, $"复制机器名 {name}");
    }

    public void ShowCopied(bool copied)
    {
        NameLabel.SetResourceReference(TextBlock.ForegroundProperty, copied ? "Accent" : "Ink");
    }
}
