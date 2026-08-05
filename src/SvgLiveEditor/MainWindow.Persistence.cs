using System.IO;
using System.Text;
using System.Windows;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;

namespace SvgLiveEditor;

public partial class MainWindow
{
    private readonly SvgTemplateCatalog _templateCatalog = new();
    private readonly RecoverySnapshotStore _recoverySnapshotStore = new();
    private readonly AutoSaveFileService _autoSaveFileService = new();
    private readonly AutoSavePolicy _autoSavePolicy = new();
    private readonly SvgValidationService _persistenceValidationService = new();
    private readonly AsyncDebouncer _recoveryDebouncer =
        new(DocumentPersistencePolicy.RecoveryDelay);
    private readonly AsyncDebouncer _autoSaveDebouncer =
        new(DocumentPersistencePolicy.AutoSaveDelay);

    private long _documentSession;
    private string _recoverySnapshotId =
        RecoverySnapshotStore.CreateSnapshotId();
    private long _recoveryRevisionBaseline;
    private long _loadedSourceRevision;
    private bool _isAutoSaveEligibleDocument;
    private bool _startupDocumentLoaded;
    private string? _lastRecoveryFailure;
    private string? _lastAutoSaveFailure;

    private void InitializeDocumentPersistence()
    {
        AutoSaveMenuItem.IsChecked = _userPreferences.AutoSaveEnabled;
    }

    private void BeginDocumentSession(
        string? recoverySnapshotId,
        bool autoSaveEligible,
        long recoveryRevisionBaseline)
    {
        _recoveryDebouncer.Cancel();
        _autoSaveDebouncer.Cancel();
        _documentSession = checked(_documentSession + 1);
        _recoverySnapshotId = recoverySnapshotId
            ?? RecoverySnapshotStore.CreateSnapshotId();
        _recoveryRevisionBaseline = recoveryRevisionBaseline;
        _isAutoSaveEligibleDocument = autoSaveEligible;
        _lastRecoveryFailure = null;
        _lastAutoSaveFailure = null;
        _viewModel.Inspector.BeginDocumentSession();
    }

    private void QueuePersistenceForCurrentEdit()
    {
        QueueRecoverySnapshot();
        QueueAutoSave();
    }

    private void QueueRecoverySnapshot()
    {
        string source = SourceEditor.Text;
        string snapshotId = _recoverySnapshotId;
        string? originalPath = _viewModel.CurrentFilePath;
        string displayName = _viewModel.CurrentFileName;
        long sourceRevision = _sourceRevisionTracker.Current;
        long session = _documentSession;
        RecoverySnapshot snapshot;
        try
        {
            long recoveryRevision = RecoveryRevisionCalculator.Calculate(
                _recoveryRevisionBaseline,
                _loadedSourceRevision,
                sourceRevision);
            snapshot = RecoverySnapshotStore.CreateSnapshot(
                snapshotId,
                originalPath,
                displayName,
                source,
                recoveryRevision,
                DateTimeOffset.UtcNow);
        }
        catch (Exception exception) when (exception is EncoderFallbackException
            or ArgumentException
            or NotSupportedException)
        {
            string failure =
                $"Recovery could not capture this revision: {exception.Message}";
            if (!failure.Equals(
                    _lastRecoveryFailure,
                    StringComparison.Ordinal))
            {
                _lastRecoveryFailure = failure;
                _viewModel.SetOperationStatus(
                    $"Recovery failed · {failure}");
            }

            return;
        }

        _ = _recoveryDebouncer.DebounceAsync(async cancellationToken =>
        {
            PersistenceOperationResult result = await Task.Run(
                () => _recoverySnapshotStore.TryWrite(snapshot),
                cancellationToken).ConfigureAwait(false);
            await Dispatcher.InvokeAsync(() =>
            {
                if (!IsCurrentDocumentSnapshot(
                        session,
                        sourceRevision,
                        source,
                        originalPath)
                    || !snapshotId.Equals(
                        _recoverySnapshotId,
                        StringComparison.Ordinal))
                {
                    return;
                }

                if (result.Succeeded)
                {
                    _lastRecoveryFailure = null;
                    _viewModel.SetOperationStatus("Recovery saved");
                    return;
                }

                string failure = result.ErrorMessage
                    ?? "Recovery could not save a local snapshot.";
                if (!failure.Equals(
                        _lastRecoveryFailure,
                        StringComparison.Ordinal))
                {
                    _lastRecoveryFailure = failure;
                    _viewModel.SetOperationStatus(
                        $"Recovery failed · {failure}");
                }
            });
        });
    }

    private void QueueAutoSave()
    {
        _autoSaveDebouncer.Cancel();
        if (!DocumentPersistencePolicy.ShouldScheduleAutoSave(
                _userPreferences.AutoSaveEnabled,
                _isAutoSaveEligibleDocument,
                _viewModel.IsModified,
                _viewModel.CurrentFilePath)
            || _viewModel.CurrentFilePath is not string path)
        {
            return;
        }

        string source = SourceEditor.Text;
        long revision = _sourceRevisionTracker.Current;
        long session = _documentSession;
        _ = _autoSaveDebouncer.DebounceAsync(async cancellationToken =>
        {
            SvgValidationResult validation = await Task.Run(
                () => _persistenceValidationService.Validate(source),
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            AutoSaveValidationDecision decision =
                _autoSavePolicy.Evaluate(validation);
            if (!decision.CanWrite)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    if (IsCurrentDocumentSnapshot(
                            session,
                            revision,
                            source,
                            path))
                    {
                        _viewModel.SetOperationStatus(
                            decision.StatusMessage);
                    }
                });
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                if (IsCurrentDocumentSnapshot(
                        session,
                        revision,
                        source,
                        path))
                {
                    _viewModel.SetOperationStatus(
                        decision.StatusMessage);
                }
            });

            AutoSavePrepareResult prepared = await Task.Run(
                () => _autoSaveFileService.Prepare(path, source),
                cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                prepared.PreparedWrite?.Dispose();
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                using PreparedAutoSave? preparedWrite =
                    prepared.PreparedWrite;
                if (!IsCurrentDocumentSnapshot(
                        session,
                        revision,
                        source,
                        path))
                {
                    return;
                }

                if (!prepared.Succeeded || preparedWrite is null)
                {
                    ShowAutoSaveFailure(
                        prepared.ErrorMessage
                        ?? "Auto Save could not stage the document.");
                    return;
                }

                PersistenceOperationResult result =
                    preparedWrite.Commit();
                if (!result.Succeeded)
                {
                    ShowAutoSaveFailure(
                        result.ErrorMessage
                        ?? "Auto Save could not update the original file.");
                    return;
                }

                _viewModel.MarkSaved(path);
                ClearCurrentRecoverySnapshot(renew: true);
                _lastAutoSaveFailure = null;
                _viewModel.SetOperationStatus("Auto-saved");
            });
        });
    }

    private bool IsCurrentDocumentSnapshot(
        long session,
        long revision,
        string source,
        string? path)
    {
        return DocumentPersistenceRevisionGuard.IsCurrent(
            session,
            _documentSession,
            revision,
            _sourceRevisionTracker.Current,
            source,
            SourceEditor.Text,
            path,
            _viewModel.CurrentFilePath);
    }

    private void ShowAutoSaveFailure(string failure)
    {
        if (failure.Equals(_lastAutoSaveFailure, StringComparison.Ordinal))
        {
            return;
        }

        _lastAutoSaveFailure = failure;
        _viewModel.SetOperationStatus($"Auto Save failed · {failure}");
    }

    private void OnManualDocumentSaved()
    {
        _autoSaveDebouncer.Cancel();
        _recoveryDebouncer.Cancel();
        _isAutoSaveEligibleDocument = true;
        ClearCurrentRecoverySnapshot(renew: true);
        _lastAutoSaveFailure = null;
        _lastRecoveryFailure = null;
        _viewModel.SetOperationStatus("Saved");
    }

    private void DiscardCurrentRecoverySnapshot()
    {
        _autoSaveDebouncer.Cancel();
        _recoveryDebouncer.Cancel();
        ClearCurrentRecoverySnapshot(renew: false);
    }

    private void ClearCurrentRecoverySnapshot(bool renew)
    {
        string completedSnapshotId = _recoverySnapshotId;
        _recoverySnapshotStore.TryDelete(
            completedSnapshotId,
            retire: true);
        if (renew)
        {
            _recoverySnapshotId =
                RecoverySnapshotStore.CreateSnapshotId();
            _recoveryRevisionBaseline = 0;
            _loadedSourceRevision = _sourceRevisionTracker.Current;
        }
    }

    private void CancelDocumentPersistence()
    {
        _autoSaveDebouncer.Cancel();
        _recoveryDebouncer.Cancel();
    }

    private void DisposeDocumentPersistence()
    {
        _autoSaveDebouncer.Dispose();
        _recoveryDebouncer.Dispose();
    }

    private bool TryRestoreRecoverySnapshot()
    {
        IReadOnlyList<RecoveryCandidate> candidates =
            _recoverySnapshotStore.LoadMeaningfulCandidates(
                DateTimeOffset.UtcNow);
        if (candidates.Count == 0)
        {
            return false;
        }

        RecoveryWindow dialog = new(candidates)
        {
            Owner = this
        };
        dialog.ShowDialog();
        RecoveryCandidate? selected = dialog.SelectedCandidate;
        if (dialog.Choice == RecoveryDialogChoice.Discard
            && selected is not null)
        {
            bool deleted = _recoverySnapshotStore.TryDelete(
                selected.Snapshot.SnapshotId,
                retire: true);
            _viewModel.SetOperationStatus(
                deleted
                    ? "Selected recovery snapshot discarded."
                    : "The selected recovery snapshot could not be deleted.");
            return false;
        }

        if (dialog.Choice != RecoveryDialogChoice.Restore
            || selected is null)
        {
            return false;
        }

        LoadIntoEditor(
            selected.Snapshot.Source,
            selected.RestorablePath,
            isModified: true,
            recoverySnapshotId: selected.Snapshot.SnapshotId,
            autoSaveEligible: false,
            recoveryRevisionBaseline: selected.Snapshot.Revision,
            queueInitialRecovery: false);
        _viewModel.SetOperationStatus(
            "Recovered snapshot loaded · Save to keep it");
        return true;
    }

    private void OnNewFromTemplateClick(
        object sender,
        RoutedEventArgs e)
    {
        IReadOnlyList<SvgTemplateDefinition> templates;
        try
        {
            templates = _templateCatalog.LoadAll();
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or IOException
            or DecoderFallbackException)
        {
            MessageBox.Show(
                this,
                $"The template gallery could not be loaded: {exception.Message}",
                "Templates unavailable",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        TemplateGalleryWindow dialog = new(templates)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true
            || dialog.SelectedTemplate is not SvgTemplateDefinition selected
            || !ConfirmCanLeaveCurrentDocument())
        {
            return;
        }

        LoadIntoEditor(
            selected.Source,
            path: null,
            isModified: true,
            recoverySnapshotId: null,
            autoSaveEligible: false,
            queueInitialRecovery: true);
        _viewModel.SetOperationStatus(
            $"Template opened · {selected.Name} · Save As required");
    }

    private void OnAutoSaveClick(object sender, RoutedEventArgs e)
    {
        _userPreferences = _userPreferences with
        {
            AutoSaveEnabled = AutoSaveMenuItem.IsChecked
        };
        _userPreferencesService.TrySave(_userPreferences);

        if (!_userPreferences.AutoSaveEnabled)
        {
            _autoSaveDebouncer.Cancel();
            _viewModel.SetOperationStatus("Auto Save off");
            return;
        }

        if (!_isAutoSaveEligibleDocument)
        {
            _viewModel.SetOperationStatus(
                "Auto Save on · Open or save a named SVG/TXT document to enable it");
            return;
        }

        _viewModel.SetOperationStatus("Auto Save on");
        if (_viewModel.IsModified)
        {
            QueueAutoSave();
        }
    }
}
