using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ModularERPPlatform.Shell.Wpf;

/// <summary>
/// The shell window. Holds a single region; navigation arrives in Phase 1.5.
/// </summary>
public partial class ShellWindow : Window
{
    /// <summary>Initialises the shell window.</summary>
    /// <param name="viewModel">The shell view model.</param>
    public ShellWindow(ShellViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
    }
}
