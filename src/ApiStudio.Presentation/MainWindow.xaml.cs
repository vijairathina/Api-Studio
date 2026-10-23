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
            fe.BringIntoView();
        }
    }

    private void OnTabsScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scv)
        {
            scv.ScrollToHorizontalOffset(scv.HorizontalOffset - (e.Delta * 0.75));
            e.Handled = true;
        }
    }

    private void ScrollTabsLeft_Click(object sender, RoutedEventArgs e)
    {
        TabsScrollViewer.ScrollToHorizontalOffset(Math.Max(0, TabsScrollViewer.HorizontalOffset - 160));
    }

    private void ScrollTabsRight_Click(object sender, RoutedEventArgs e)
    {
        TabsScrollViewer.ScrollToHorizontalOffset(TabsScrollViewer.HorizontalOffset + 160);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}