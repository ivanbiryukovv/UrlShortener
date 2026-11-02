using System.Windows;
using UrlShortener.Admin.ViewModels;

namespace UrlShortener.Admin;

public partial class LoginWindow : Window
{
    private const int MaxAttempts = 3;

    private int _attempts;

    public bool LoginSuccessful { get; private set; } = false;

    public LoginWindow(LoginViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private async void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LoginViewModel vm)
            return;

        try
        {
            vm.Secret = this.SecretBox.Password;
            await vm.LoginAsync();

            LoginSuccessful = true;
            this.Close();
        }
        catch (Exception ex)
        {
            _attempts++;
            if (_attempts >= MaxAttempts)
            {
                MessageBox.Show(this, "Превышено количество попыток входа.", "Доступ запрещен",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                LoginSuccessful = false;
                this.Close();
                return;
            }

            MessageBox.Show(this, $"Ошибка входа: {ex.Message}", "Вход не выполнен",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            this.SecretBox.Clear();
            this.SecretBox.Focus();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        LoginSuccessful = false;
        this.Close();
    }
}