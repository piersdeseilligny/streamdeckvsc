﻿using System;
using System.Collections.Generic;
using BarRaider.SdTools;
using Fleck;
using Newtonsoft.Json;
using StreamDeckVSC.Messages;

namespace StreamDeckVSC
{
    public class MessageServer : IDisposable
    {
        private readonly WebSocketServer server;

        private static readonly Dictionary<Guid, Client> clients = new Dictionary<Guid, Client>();

        public static Client CurrentClient { get; private set; }

        public MessageServer(string host, int port) => server = new WebSocketServer($"ws://{host}:{port}");

        public void Start()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Starting server {server.Location}");

            server.Start(connection =>
            {
                connection.OnOpen = () => OnConnected(connection);
                connection.OnClose = () => OnDisconnected(connection);
                connection.OnMessage = message => HandleMessages(connection, message);
            });
        }

        private void OnDisconnected(IWebSocketConnection connection)
        {
            clients.Remove(connection.ConnectionInfo.Id);

            ActivateConnectedClient(connection);
        }

        private void OnConnected(IWebSocketConnection connection)
        {
            clients[connection.ConnectionInfo.Id] = new Client(connection);

            ActivateConnectedClient(connection);
        }

        private void ActivateConnectedClient(IWebSocketConnection connection)
        {
            if (clients.Count == 1 && connection.ConnectionInfo.Headers.TryGetValue("X-VSSessionID", out var sessionId))
            {
                SetCurrentClient(connection.ConnectionInfo.Id, sessionId);
            }
        }

        private void HandleMessages(IWebSocketConnection connection, string rawMessage)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Received message {rawMessage}");

            var message = JsonConvert.DeserializeObject<Message>(rawMessage);

            if (!string.IsNullOrEmpty(message?.Data))
            {
                if (message.Id == nameof(ChangeActiveSessionMessage))
                {
                    var changeActiveSession = JsonConvert.DeserializeObject<ChangeActiveSessionMessage>(message.Data);

                    SetCurrentClient(connection.ConnectionInfo.Id, changeActiveSession.SessionId);
                }
            }
        }

        private void SetCurrentClient(Guid clientId, string sessionId)
        {
            CurrentClient = clients[clientId];

            var activeSessionChanged = new ActiveSessionChangedMessage(sessionId);

            foreach (var client in clients)
            {
                client.Value.Send(activeSessionChanged);
            }
        }

        public void Dispose() => server?.Dispose();
    }
}
