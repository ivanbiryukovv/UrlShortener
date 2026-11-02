using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UrlShortener.Admin.Services;
using UrlShortener.Admin.ViewModels;

namespace UrlShortener.Admin;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false);
            })
            .ConfigureServices((context, services) =>
            {
                var baseUrl = (context.Configuration["Api:BaseUrl"] ?? "http://localhost:5010").TrimEnd('/');

                // Configure HttpClient for API requests; token will be obtained via login
                services.AddSingleton<ITokenProvider, TokenProvider>();
                services.AddTransient<AuthHeaderHandler>();
                services.AddHttpClient<ApiClient>(client =>
                {
                    client.BaseAddress = new Uri($"{baseUrl}/");
                }).AddHttpMessageHandler(provider => provider.GetRequiredService<AuthHeaderHandler>());

                services.AddTransient<LoginViewModel>();
                services.AddTransient<LoginWindow>();

                services.AddSingleton<CreateLinkViewModel>();
                services.AddSingleton<LinkListViewModel>();
                services.AddSingleton<StatsViewModel>();
                services.AddSingleton<AuditViewModel>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        try
        {
            var tokenProvider = _host.Services.GetRequiredService<ITokenProvider>();
            Application.Current.Resources["TokenProvider"] = tokenProvider;
        }
        catch
        {
        }

        var config = _host.Services.GetRequiredService<IConfiguration>();
        var baseUrl = (config["Api:BaseUrl"] ?? "http://localhost:5010").TrimEnd('/');
        bool apiReady = false;
        while (!apiReady)
        {
            try
            {
                using var http = new System.Net.Http.HttpClient { BaseAddress = new Uri(baseUrl + "/"), Timeout = TimeSpan.FromSeconds(2) };
                var resp = await http.GetAsync("", CancellationToken.None);
                apiReady = true;
            }
            catch
            {
                var retry = MessageBox.Show("Не удалось подключиться к API. Повторить попытку?", "Сервер недоступен", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (retry == MessageBoxResult.No)
                {
                    await _host.StopAsync();
                    _host.Dispose();
                    Shutdown();
                    return;
                }
            }
        }

        var loginWindow = _host.Services.GetRequiredService<LoginWindow>();

        loginWindow.Closed += (s, args) =>
        {
            if (loginWindow.LoginSuccessful)
            {
                var window = _host.Services.GetRequiredService<MainWindow>();
                window.DataContext = _host.Services.GetRequiredService<MainViewModel>();
                window.Closed += (s2, args2) =>
                {
                    Current.Shutdown();
                };
                window.Show();
            }
            else
            {

                Current.Shutdown();
            }
        };

        loginWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}