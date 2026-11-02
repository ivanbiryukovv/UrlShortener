using System.Collections.ObjectModel;
using UrlShortener.Admin.Infrastructure;
using UrlShortener.Admin.Services;
using UrlShortener.Contracts;

namespace UrlShortener.Admin.ViewModels;

public sealed class LinkListViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private LinkResponse? _selected;
    private string? _error;
    private bool _isBusy;

    public LinkListViewModel(ApiClient api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(RefreshAsync);
        DeactivateCommand = new RelayCommand(DeactivateAsync, () => Selected is { IsActive: true });
    }

    public ObservableCollection<LinkResponse> Links { get; } = new();

    public LinkResponse? Selected
    {
        get => _selected;
        set { if (SetField(ref _selected, value)) DeactivateCommand.RaiseCanExecuteChanged(); }
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
    public RelayCommand DeactivateCommand { get; }

    public async Task RefreshAsync()
    {
        Error = null;
        IsBusy = true;
        try
        {
            var links = await _api.GetAllLinksAsync();
            Links.Clear();
            foreach (var link in links)
                Links.Add(link);
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

    private async Task DeactivateAsync()
    {
        if (Selected is null)
            return;

        Error = null;
        IsBusy = true;
        try
        {
            await _api.DeactivateLinkAsync(Selected.Code);
            await RefreshAsync();
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
}
