using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using NewsDistribution;
using System.Collections.Generic;

namespace ServerTCPWpfApp;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int _port = 8910;
    private readonly NewsServer _server = new();

    public Dictionary<string, NetworkClient> Clients
    {
        get => _server.Clients;
    }

    public MainWindow()
    {
        InitializeComponent();
    }

    ~MainWindow()
    {
        _server.Shutdown();
    }

    private void sendButton_Click(object sender, RoutedEventArgs e)
    {
    }

    private void enableButton_Click(object sender, RoutedEventArgs e)
    {
        if (_server.Start(_port))
            StatusLabel.Content = "Server started, waiting for connections.";
        else
            StatusLabel.Content = "Failed to start the server.";

        //_server.On
    }

    private void disableButton_Click(object sender, RoutedEventArgs e)
    {
        _server.Shutdown();
        StatusLabel.Content = "Server stopped.";
    }
}