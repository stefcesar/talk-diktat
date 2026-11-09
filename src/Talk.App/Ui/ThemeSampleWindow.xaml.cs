// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Windows;

namespace Talk.App.Ui;

/// <summary>
/// Musterbogen für den Schalter --uishots: jede Vorlage aus Ui\Theme.xaml einmal, mit erfundenen Texten.
/// </summary>
public partial class ThemeSampleWindow : Window
{
    public ThemeSampleWindow()
    {
        InitializeComponent();
        WindowTheme.Apply(this);
    }
}
