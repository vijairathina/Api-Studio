using System.Windows;
using ApiStudio.Presentation.ViewModels;

namespace ApiStudio.Presentation.Views.Controls;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += SettingsWindow_Loaded;
    }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.CloseRequested += () => Close();
        }
    }
}
