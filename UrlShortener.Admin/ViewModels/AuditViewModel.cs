using System.Collections.ObjectModel;
using UrlShortener.Admin.Infrastructure;
using UrlShortener.Admin.Services;
using UrlShortener.Contracts;

namespace UrlShortener.Admin.ViewModels;

public sealed class AuditViewModel : ViewModelBase
{
    private const string AnyAction = "(все)";

    private readonly ApiClient _api;

    private DateTime? _from;
    private DateTime? _to;
    private string _selectedAction = AnyAction;
    private string? _error;
    private bool _isBusy;

    public AuditViewModel(ApiClient api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(RefreshAsync);
        ResetCommand = new RelayCommand(ResetAsync);
    }

    public ObservableCollection<AuditLogItem> Items { get; } = new();

    public IReadOnlyList<string> Actions { get; } = new[]
    {
        AnyAction, "LinkCreated", "LinkVisited", "LinkDeactivated", "IpBlocked"
    };

    public DateTime? From
    {
        get => _from;
        set => SetField(ref _from, value);
    }

    public DateTime? To
    {
        get => _to;
        set => SetField(ref _to, value);
    }

    public string SelectedAction
    {
        get => _selectedAction;
        set => SetField(ref _selectedAction, value);
    }

    public string? Error
    {
        get => _error;
        private set => SetField(ref _error, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand ResetCommand { get; }

    public async Task RefreshAsync()
    {
        Error = null;
        IsBusy = true;
        try
        {
            DateTimeOffset? from = From is { } f ? new DateTimeOffset(f.Date, TimeSpan.Zero) : null;
            DateTimeOffset? to = To is { } t ? new DateTimeOffset(t.Date.AddDays(1), TimeSpan.Zero) : null;
            var action = SelectedAction == AnyAction ? null : SelectedAction;

            var items = await _api.GetAuditAsync(from, to, action, take: 200);
            Items.Clear();
            foreach (var item in items)
                Items.Add(item);
        }
        catch (ApiException ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ResetAsync()
    {
        From = null;
        To = null;
        SelectedAction = AnyAction;
        await RefreshAsync();
    }
}
