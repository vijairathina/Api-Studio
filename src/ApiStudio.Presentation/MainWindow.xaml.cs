using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ApiStudio.Domain.Entities;
using ApiStudio.Presentation.ViewModels;
using ApiStudio.Presentation.Views.Controls;

namespace ApiStudio.Presentation;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void OnTreeViewItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem item && item.DataContext is CollectionItemViewModel node)
        {
            if (node.NodeType == CollectionNodeType.Request && node.UnderlyingModel is ApiRequest req)
            {
                _viewModel.OpenRequestInTab(req);
                e.Handled = true;
            }
        }
    }

    private void ManageEnvironments_Click(object sender, RoutedEventArgs e)
    {
        var win = new EnvironmentManagerWindow
        {
            DataContext = _viewModel,
            Owner = this
        };
        win.ShowDialog();
    }

    private void OnTabItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is RequestTabViewModel tab)
        {
            _viewModel.SelectTab(tab);
        }
    }
}