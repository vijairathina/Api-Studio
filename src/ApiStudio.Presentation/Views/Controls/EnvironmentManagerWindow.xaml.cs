using System.Windows;
using ApiStudio.Domain.Entities;
using ApiStudio.Presentation.ViewModels;

namespace ApiStudio.Presentation.Views.Controls;

public partial class EnvironmentManagerWindow : Window
{
    public EnvironmentManagerWindow()
    {
        InitializeComponent();
    }

    private void AddEnvironment_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainVm)
        {
            var newEnv = new EnvironmentViewModel(new ApiEnvironment { Name = $"Environment {mainVm.Environments.Count + 1}" });
            newEnv.DeleteCommand = new Common.RelayCommand(() => mainVm.Environments.Remove(newEnv));
            mainVm.Environments.Add(newEnv);
            mainVm.SelectedEnvironment = newEnv;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
