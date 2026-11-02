using UrlShortener.Admin.Infrastructure;

namespace UrlShortener.Admin.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private int _selectedTab;

    public MainViewModel(
        CreateLinkViewModel create,
        LinkListViewModel list,
        StatsViewModel stats,
        AuditViewModel audit)
    {
        Create = create;
        List = list;
        Stats = stats;
        Audit = audit;

        OpenStatsCommand = new RelayCommand(OpenStatsAsync, () => List.Selected is not null);
        List.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LinkListViewModel.Selected))
                OpenStatsCommand.RaiseCanExecuteChanged();
        };
    }

    public CreateLinkViewModel Create { get; }
    public LinkListViewModel List { get; }
    public StatsViewModel Stats { get; }
    public AuditViewModel Audit { get; }

    public RelayCommand OpenStatsCommand { get; }

    public int SelectedTab
    {
        get => _selectedTab;
        set => SetField(ref _selectedTab, value);
    }

    private async Task OpenStatsAsync()
    {
        if (List.Selected is null)
            return;

        SelectedTab = 2;
        await Stats.ShowAsync(List.Selected.Code);
    }
}
