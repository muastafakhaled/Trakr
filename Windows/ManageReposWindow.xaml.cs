using System.IO;
using System.Windows;
using System.Windows.Forms;
using JiraTimeTracker.Services;

namespace JiraTimeTracker.Windows;

public partial class ManageReposWindow : Window
{
    private readonly ConfigService _config;

    public ManageReposWindow(ConfigService config)
    {
        _config = config;
        InitializeComponent();
        Refresh();
    }

    private void Refresh() => PathList.ItemsSource = null;

    private void PathList_SelectionChanged(object s, System.Windows.Controls.SelectionChangedEventArgs e)
        => RemoveBtn.IsEnabled = PathList.SelectedItem != null;

    private void Browse_Click(object s, RoutedEventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "Select folder containing your repos" };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            NewPathBox.Text = dlg.SelectedPath;
    }

    private void Add_Click(object s, RoutedEventArgs e)
    {
        var path = NewPathBox.Text.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(path)) return;

        if (!Directory.Exists(path))
        {
            System.Windows.MessageBox.Show($"Path not found:\n{path}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_config.Current.Repos.Contains(path))
        {
            System.Windows.MessageBox.Show("Path already in list.", "Info",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _config.Current.Repos.Add(path);
        _config.Save();
        NewPathBox.Text = "";
        PathList.ItemsSource = null;
        PathList.ItemsSource = _config.Current.Repos;
    }

    private void Remove_Click(object s, RoutedEventArgs e)
    {
        if (PathList.SelectedItem is string path)
        {
            _config.Current.Repos.Remove(path);
            _config.Save();
            PathList.ItemsSource = null;
            PathList.ItemsSource = _config.Current.Repos;
        }
    }

    private void Done_Click(object s, RoutedEventArgs e) => Close();

    protected override void OnContentRendered(System.EventArgs e)
    {
        base.OnContentRendered(e);
        PathList.ItemsSource = _config.Current.Repos;
    }
}