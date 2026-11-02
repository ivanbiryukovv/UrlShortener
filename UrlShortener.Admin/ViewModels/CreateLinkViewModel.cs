using System.Windows;
using System.Windows.Input;
using UrlShortener.Admin.Infrastructure;
using UrlShortener.Admin.Services;
using UrlShortener.Contracts;

namespace UrlShortener.Admin.ViewModels;

public sealed class CreateLinkViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private string _orderId = string.Empty;
    private int _ttlDays = 30;
    private bool _isOneTime;
    private bool _useHmac;
    private LinkResponse? _result;
    private string? _error;
    private bool _isBusy;

    public CreateLinkViewModel(ApiClient api)
    {
        _api = api;
        CreateCommand = new RelayCommand(CreateAsync, CanCreate);
        CopyCommand = new RelayCommand(CopyAsync, () => Result is not null);
        NewGuidCommand = new RelayCommand(GenerateGuidAsync);
    }

    public string OrderId
    {
        get => _orderId;
        set { if (SetField(ref _orderId, value)) RaiseCommands(); }
    }

    public int TtlDays
    {
        get => _ttlDays;
        set => SetField(ref _ttlDays, value);
    }

    public bool IsOneTime
    {
        get => _isOneTime;
        set => SetField(ref _isOneTime, value);
    }

    public bool UseHmac
    {
        get => _useHmac;
        set => SetField(ref _useHmac, value);
    }

    public LinkResponse? Result
    {
        get => _result;
        private set { if (SetField(ref _result, value)) { OnPropertyChanged(nameof(HasResult)); RaiseCommands(); } }
    }

    public bool HasResult => Result is not null;

    public string? Error
    {
        get => _error;
        private set => SetField(ref _error, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetField(ref _isBusy, value)) RaiseCommands(); }
    }

    public RelayCommand CreateCommand { get; }
    public RelayCommand CopyCommand { get; }
    public ICommand NewGuidCommand { get; }

    private bool CanCreate() => !IsBusy && Guid.TryParse(OrderId, out _);

    private async Task CreateAsync()
    {
        Error = null;
        Result = null;
        IsBusy = true;
        try
        {
            var request = new CreateLinkRequest
            {
                OrderId = Guid.Parse(OrderId),
                TtlDays = TtlDays,
                IsOneTime = IsOneTime,
                UseHmac = UseHmac
            };
            Result = await _api.CreateLinkAsync(request);
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

    private Task CopyAsync()
    {
        if (Result is not null)
            Clipboard.SetText(Result.ShortUrl);

        return Task.CompletedTask;
    }

    private Task GenerateGuidAsync()
    {
        OrderId = Guid.NewGuid().ToString();
        return Task.CompletedTask;
    }

    private void RaiseCommands()
    {
        CreateCommand.RaiseCanExecuteChanged();
        CopyCommand.RaiseCanExecuteChanged();
    }
}
