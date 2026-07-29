using System.Windows;
using SvgLiveEditor.Models;

namespace SvgLiveEditor;

public partial class RecoveryWindow : Window
{
    public RecoveryWindow(IReadOnlyList<RecoveryCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        InitializeComponent();
        DataContext = candidates;
        Loaded += (_, _) => RecoveryList.Focus();
    }

    public RecoveryDialogChoice Choice { get; private set; } =
        RecoveryDialogChoice.Skip;

    public RecoveryCandidate? SelectedCandidate =>
        RecoveryList.SelectedItem as RecoveryCandidate;

    private void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        if (SelectedCandidate is null)
        {
            return;
        }

        Choice = RecoveryDialogChoice.Restore;
        DialogResult = true;
    }

    private void OnDiscardClick(object sender, RoutedEventArgs e)
    {
        if (SelectedCandidate is null)
        {
            return;
        }

        Choice = RecoveryDialogChoice.Discard;
        DialogResult = true;
    }

    private void OnSkipClick(object sender, RoutedEventArgs e)
    {
        Choice = RecoveryDialogChoice.Skip;
        DialogResult = false;
    }
}
