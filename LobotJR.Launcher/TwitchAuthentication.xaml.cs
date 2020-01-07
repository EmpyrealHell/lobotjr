﻿using LobotJR.Shared.Authentication;
using LobotJR.Shared.Client;
using LobotJR.Shared.Utility;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Navigation;

namespace LobotJR.Launcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string _cancelAuthUri = "https://id.twitch.tv/oauth2/authorize";
        private const string _scope = "chat:read chat:edit whispers:read whispers:edit channel_subscriptions";

        private ClientData _clientData;
        private bool _isNavigating = false;
        private string _state = Guid.NewGuid().ToString();


        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadClientData();
            LoadTwitchAuthPage();
        }

        private void Browser_Navigated(object sender, NavigationEventArgs e)
        {
            if (_isNavigating)
            {
                // The wpf browser control doesn't have an event for redirects or load failures
                // if we get to the navigated event twice without a load complete, it's probably
                // a redirect, which means our twitch client data is wrong.
                MessageBox.Show(this, "Unable to load twitch authentication page, please confirm your client data and try again.", "Twitch Authentication Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateClientData();

            }
            _isNavigating = true;

            if (e.Uri.ToString().Equals(_cancelAuthUri))
            {
                Close();
            }
            else if (e.Uri.ToString().StartsWith(_clientData.RedirectUri))
            {
                // If you're not familiar with Linq, this is going to look like complete nonsense,
                // but it's basically just a way to turn the url we get back into something we can actually use
                var returnValues = e.Uri.Query.Substring(1).Split('&')
                    .Select(x => x.Split('=')).ToDictionary(key => key[0], value => value[1]);

                if (returnValues["state"] == _state)
                {
                    var tokenData = AuthToken.Fetch(_clientData.ClientId, _clientData.ClientSecret, returnValues["code"], _clientData.RedirectUri);
                    FileUtils.WriteTokenData();
                    using (var lobot = new Process())
                    {
                        lobot.StartInfo.FileName = "LobotJR.exe";
                        lobot.StartInfo.UseShellExecute = true;
                        lobot.Start();
                    }
                    Close();
                }
                else
                {
                    MessageBox.Show(this, "CSRF attack detected, exiting application", "Security Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    Close();
                }
            }
        }

        private void Browser_LoadCompleted(object sender, NavigationEventArgs e)
        {
            _isNavigating = false;
        }

        private void AddQuery(UriBuilder builder, string rawKey, string rawValue)
        {
            var key = Uri.EscapeUriString(rawKey);
            var value = Uri.EscapeUriString(rawValue);
            if (!string.IsNullOrWhiteSpace(builder.Query))
            {
                builder.Query = builder.Query.Substring(1) + "&" + key + "=" + value;
            }
            else
            {
                builder.Query = key + "=" + value;
            }
        }

        private void LoadClientData()
        {
            if (FileUtils.HasClientData())
            {
                _clientData = FileUtils.ReadClientData();
                if (string.IsNullOrWhiteSpace(_clientData.ClientId)
                    || string.IsNullOrWhiteSpace(_clientData.ClientSecret)
                    || string.IsNullOrWhiteSpace(_clientData.RedirectUri))
                {
                    UpdateClientData();
                }
            }
            else
            {
                _clientData = new ClientData();
                _clientData.ClientSecret = FileUtils.ReadLegacySecret();
                UpdateClientData();
            }
        }

        private void UpdateClientData()
        {
            var updateModal = new UpdateConfig();
            updateModal.ClientIdValue = _clientData.ClientId;
            updateModal.ClientSecretValue= _clientData.ClientSecret;
            updateModal.RedirectUriValue = _clientData.RedirectUri;
            var result = updateModal.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                MessageBox.Show(this, "Unable to launch lobot without proper client config. Closing.", "Missing Client Config", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
            else
            {
                _clientData.ClientId = updateModal.ClientIdValue;
                _clientData.ClientSecret = updateModal.ClientSecretValue;
                _clientData.RedirectUri = updateModal.RedirectUriValue;
                FileUtils.WriteClientData(_clientData);
            }
        }

        private void LoadTwitchAuthPage()
        {
            var builder = new UriBuilder("https", "id.twitch.tv");
            builder.Path = "oauth2/authorize";
            AddQuery(builder, "client_id", _clientData.ClientId);
            AddQuery(builder, "redirect_uri", _clientData.RedirectUri);
            AddQuery(builder, "response_type", "code");
            AddQuery(builder, "scope", _scope);
            AddQuery(builder, "force_verify", "true");
            AddQuery(builder, "state", _state);
            Browser.Navigate(builder.Uri);
        }
    }
}
