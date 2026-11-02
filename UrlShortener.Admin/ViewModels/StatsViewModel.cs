using System.Collections.ObjectModel;
using UrlShortener.Admin.Infrastructure;
using UrlShortener.Admin.Services;
using UrlShortener.Contracts;

namespace UrlShortener.Admin.ViewModels;

public sealed class StatsViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private string _code = string.Empty;
    private LinkStatsResponse? _stats;
    private string? _error;
    private bool _isBusy;

    public StatsViewModel(ApiClient api)
    {
        _api = api;
        LoadCommand = new RelayCommand(LoadAsync, () => !string.IsNullOrWhiteSpace(Code));
    }

    public string Code
    {
        get => _code;
        set { if (SetField(ref _code, value)) LoadCommand.RaiseCanExecuteChanged(); }
    }

    public LinkStatsResponse? Stats
    {
        get => _stats;
        private set { if (SetField(ref _stats, value)) OnPropertyChanged(nameof(HasStats)); }
    }

    public bool HasStats => Stats is not null;

    public ObservableCollection<DailyVisits> ByDay { get; } = new();

    public int MaxDayCount => ByDay.Count == 0 ? 1 : ByDay.Max(d => d.Count);

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

    public RelayCommand LoadCommand { get; }

    public async Task ShowAsync(string code)
    {
        Code = code;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        Error = null;
        IsBusy = true;
        try
        {
            var stats = await _api.GetStatsAsync(Code.Trim());
            ByDay.Clear();

            if (stats is null)
            {
                Stats = null;
                Error = "Ссылка не найдена";
                return;
            }

            Stats = stats;
            foreach (var day in stats.ByDay)
                ByDay.Add(day);

            OnPropertyChanged(nameof(MaxDayCount));
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
