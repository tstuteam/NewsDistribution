using System.Windows;
using System.Diagnostics;
using System.Collections.ObjectModel;
using NewsDistribution;
using NewsDistribution.Client;

namespace ClientTCPWpfApp;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int Port = 8910;
    private readonly NewsClient _client = new();

    private bool _connecting = false;

    public ObservableCollection<News> News { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        _client.OnSubscribeAttempt += status =>
        {
            Trace.WriteLine($"Subscribe attempt: {status}.");
            _connecting = false;

            if (status == ConnectStatus.Success)
            {
                Dispatcher.Invoke(() => News.Clear());
                return;
            }

            var message = status switch
            {
                ConnectStatus.UnableToConnect => "Не удалось найти сервер.",
                ConnectStatus.AlreadyConnected => "Соединение уже установлено.",
                ConnectStatus.Rejected => "Запрос был отвергнут сервером.",
                _ => "Неизвестная ошибка."
            };

            Dispatcher.Invoke(() =>
                MessageBox.Show(message, "Не удалось подключиться")
            );
        };

        _client.OnUnsubscribe += () =>
            Trace.WriteLine("Disconnected.");

        _client.OnNewsReceived += news =>
            Dispatcher.Invoke(() => News.Insert(0, news));
    }

    ~MainWindow()
    {
        _client.Unsubscribe();
    }

    private void ConnectButton_onClick(object sender, RoutedEventArgs e)
    {
        if (_connecting)
            return;

        var name = UserName.Text;

        if (name.Length == 0)
        {
            MessageBox.Show("Введите имя.", "Не указано значение поля");
            return;
        };

        _connecting = true;

        _client.Subscribe(Address.Text, Port, name);
    }

    private void DisconnectButton_onClick(object sender, RoutedEventArgs e)
    {
        _client.Unsubscribe();
    }

    private void NewsListItem_Click(object sender, RoutedEventArgs e)
    {
        var itemIndex = NewsTextBlock.SelectedIndex;

        if (itemIndex == -1)
            return;

        var (title, description, content) = News[itemIndex];

        Clipboard.SetText($"{title}\n\n{description}\n\n{content}");
    }
}