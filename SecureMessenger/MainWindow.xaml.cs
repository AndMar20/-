using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using SecureMessenger.Services;

namespace SecureMessenger;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly MessengerService _messengerService = new();
    private string _statusMessage = "Сервер не запущен";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Messages { get; } = new();

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _messengerService.MessageReceived += OnMessageReceived;
        _messengerService.StatusChanged += UpdateStatus;
    }

    private async void StartServer_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(LocalPortBox.Text, out var port))
        {
            StatusMessage = "Неверный порт";
            return;
        }

        await _messengerService.StartAsync(port, SharedKeyBox.Password);
    }

    private void StopServer_Click(object sender, RoutedEventArgs e)
    {
        _messengerService.Stop();
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(RemotePortBox.Text, out var port))
        {
            StatusMessage = "Неверный порт получателя";
            return;
        }

        var message = MessageBoxInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            StatusMessage = "Введите текст сообщения";
            return;
        }

        try
        {
            await _messengerService.SendAsync(RemoteHostBox.Text, port, SharedKeyBox.Password, message);
            Messages.Add($"Вы: {message}");
            MessageBoxInput.Clear();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Не удалось отправить: {ex.Message}";
        }
    }

    private void OnMessageReceived(string message)
    {
        Dispatcher.Invoke(() => Messages.Add($"Собеседник: {message}"));
    }

    private void UpdateStatus(string message)
    {
        Dispatcher.Invoke(() => StatusMessage = message);
    }

    protected override void OnClosed(EventArgs e)
    {
        _messengerService.Dispose();
        base.OnClosed(e);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
