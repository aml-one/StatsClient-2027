using StatsClient.KnowledgeBase.Core;
using static StatsClient.MVVM.Core.DatabaseConnection;

namespace StatsClient.MVVM.Core.KnowledgeBase;

public sealed class KnowledgeBaseAutoSaveCoordinator
{
    private readonly KnowledgeBaseRepository _repository = new();
    private readonly KnowledgeBaseBackupRepository _backupRepository = new();
    private readonly KnowledgeBaseKeywordExtractor _keywordExtractor = new();
    private readonly System.Timers.Timer _debounceTimer;
    private readonly object _gate = new();

    private KnowledgeBaseSaveRequest? _pendingRequest;
    private bool _backupCapturedForSession;
    private Func<Task>? _onSaved;
    private Action<string>? _onStatusChanged;
    private Action<Exception>? _onError;

    public KnowledgeBaseAutoSaveCoordinator()
    {
        _debounceTimer = new System.Timers.Timer(800) { AutoReset = false };
        _debounceTimer.Elapsed += async (_, _) => await RunSaveAsync().ConfigureAwait(false);
    }

    public void Configure(
        Action<string> onStatusChanged,
        Func<Task>? onSaved = null,
        Action<Exception>? onError = null)
    {
        _onStatusChanged = onStatusChanged;
        _onSaved = onSaved;
        _onError = onError;
    }

    public void ResetSession()
    {
        lock (_gate)
        {
            _backupCapturedForSession = false;
            _pendingRequest = null;
        }

        _debounceTimer.Stop();
    }

    public void QueueSave(KnowledgeBaseSaveRequest request, bool captureBackupIfNeeded = true)
    {
        lock (_gate)
        {
            _pendingRequest = request;
            if (captureBackupIfNeeded)
            {
                _ = CaptureBackupIfNeededAsync(request.CardId);
            }
        }

        _onStatusChanged?.Invoke("Pending…");
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    public async Task FlushAsync()
    {
        _debounceTimer.Stop();
        await RunSaveAsync().ConfigureAwait(false);
    }

    private async Task CaptureBackupIfNeededAsync(int cardId)
    {
        if (_backupCapturedForSession || cardId <= 0)
        {
            return;
        }

        try
        {
            var existing = await _backupRepository.GetBackupAsync(cardId, Environment.MachineName).ConfigureAwait(false);
            if (existing is not null)
            {
                _backupCapturedForSession = true;
                return;
            }

            var detail = await _repository.GetCardDetailAsync(cardId).ConfigureAwait(false);
            if (detail is null)
            {
                return;
            }

            var snapshot = _repository.CreateSnapshot(detail);
            var created = await _backupRepository.TryCreateBackupIfNotExistsAsync(
                snapshot,
                Environment.MachineName).ConfigureAwait(false);

            if (created)
            {
                _backupCapturedForSession = true;
            }
        }
        catch
        {
            // backup is best-effort; editing should continue
        }
    }

    private async Task RunSaveAsync()
    {
        KnowledgeBaseSaveRequest? request;
        lock (_gate)
        {
            request = _pendingRequest;
        }

        if (request is null || request.CardId <= 0)
        {
            return;
        }

        if (KnowledgeBaseCardContentRules.IsEmpty(request))
        {
            return;
        }

        _onStatusChanged?.Invoke("Saving…");

        try
        {
            await _repository.SaveCardAsync(request).ConfigureAwait(false);

            if (request.IsAutoTagPass)
            {
                var tags = await _keywordExtractor.ExtractTagsAsync(
                    request.Title,
                    request.BodyText,
                    request.Links.Select(l => l.Label),
                    () => ReadStatsSetting("Nvidia_API_KEY")).ConfigureAwait(false);

                if (tags.Count > 0)
                {
                    await _repository.MergeAutoTagsAsync(request.CardId, tags).ConfigureAwait(false);
                }
            }

            _onStatusChanged?.Invoke("Saved");
            if (_onSaved is not null)
            {
                await _onSaved().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _onStatusChanged?.Invoke("Error");
            _onError?.Invoke(ex);
        }
    }
}
