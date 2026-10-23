using System.Windows;
using ApiStudio.Presentation.ViewModels;

namespace ApiStudio.Presentation.Views.Controls;

public partial class CurlImportDialog : Window
{
    public CurlImportDialog()
    {
        InitializeComponent();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is CurlImportViewModel vm && vm.ParsedRequest != null)
        {
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show("Please provide a valid cURL command.", "Invalid cURL", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
