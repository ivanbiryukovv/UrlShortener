using System.Threading.Tasks;
using System.Windows.Input;
using UrlShortener.Admin.Infrastructure;
using UrlShortener.Admin.Services;

namespace UrlShortener.Admin.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    public LoginViewModel(ApiClient api) => _api = api;

    private string _secret = string.Empty;
    public string Secret
    {
        get => _secret;
        set => SetField(ref _secret, value);
    }

    public ICommand LoginCommand => new RelayCommand(async () => await LoginAsync());

    public async Task<string> LoginAsync()
    {
        var token = await _api.AuthenticateAsync(Secret);
        return token;
    }
}
