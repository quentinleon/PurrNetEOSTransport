using System.Collections.Generic;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet.EOS
{
    [DefaultExecutionOrder(-100)]
    public class EOSTransport : GenericTransport, ITransport
    {
        [Header("EOS Settings")]
        [SerializeField] private string _socketName = "PurrNetEOS";
        [SerializeField] private string _remoteProductUserId;

        public string socketName
        {
            get => _socketName;
            set => _socketName = value;
        }

        public string remoteProductUserId
        {
            get => _remoteProductUserId;
            set => _remoteProductUserId = value;
        }

#if EOS_SDK
        public override bool isSupported => true;
#else
        public override bool isSupported => false;
#endif

        public override ITransport transport => this;

        private readonly List<Connection> _connections = new List<Connection>();

        public IReadOnlyList<Connection> connections => _connections;

        private ConnectionState _listenerState = ConnectionState.Disconnected;

        public ConnectionState listenerState
        {
            get => _listenerState;
            private set
            {
                if (_listenerState == value)
                    return;

                _listenerState = value;
                onConnectionState?.Invoke(_listenerState, true);
            }
        }

        private ConnectionState _clientState = ConnectionState.Disconnected;

        public ConnectionState clientState
        {
            get => _clientState;
            private set
            {
                if (_clientState == value)
                    return;

                _clientState = value;
                onConnectionState?.Invoke(_clientState, false);
            }
        }

        public event OnConnected onConnected;
        public event OnDisconnected onDisconnected;
        public event OnDataReceived onDataReceived;
        public event OnDataSent onDataSent;
        public event OnConnectionState onConnectionState;

        private EOSServer _server;
        private EOSClient _client;

        protected override void StartClientInternal()
        {
            Connect(_remoteProductUserId, 0);
        }

        protected override void StartServerInternal()
        {
            Listen(0);
        }

        public void Listen(ushort port)
        {
            if (_server != null)
                StopListening();

            listenerState = ConnectionState.Connecting;

            _server = new EOSServer();
            _server.Initialize(this);

            if (_server.Listen())
            {
                listenerState = ConnectionState.Connected;
            }
            else
            {
                listenerState = ConnectionState.Disconnecting;
                listenerState = ConnectionState.Disconnected;
            }

            _server.onDataReceived += OnServerData;
            _server.onRemoteConnected += OnRemoteConnected;
            _server.onRemoteDisconnected += OnRemoteDisconnected;
        }

        private void OnRemoteConnected(int connectionId)
        {
            _connections.Add(new Connection(connectionId));
            onConnected?.Invoke(new Connection(connectionId), true);
        }

        private void OnRemoteDisconnected(int connectionId)
        {
            _connections.Remove(new Connection(connectionId));
            onDisconnected?.Invoke(new Connection(connectionId), DisconnectReason.ClientRequest, true);
        }

        private void OnServerData(int connectionId, ByteData data)
        {
            onDataReceived?.Invoke(new Connection(connectionId), data, true);
        }

        public void StopListening()
        {
            if (listenerState != ConnectionState.Disconnected)
                listenerState = ConnectionState.Disconnecting;
            
            _server?.Stop();
            listenerState = ConnectionState.Disconnected;
            _server = null;
        }

        public void Connect(string address, ushort port)
        {
            if (_client != null)
                Disconnect();

            _client = new EOSClient();
            _client.Initialize(this);
            _client.onConnectionState += OnClientStateChanged;
            _client.onDataReceived += OnClientDataReceived;

            _client.Connect(address);
        }

        private void OnClientDataReceived(ByteData data)
        {
            onDataReceived?.Invoke(new Connection(-1), data, false);
        }

        private void OnClientStateChanged(ConnectionState state)
        {
            if (state == ConnectionState.Connected)
                onConnected?.Invoke(new Connection(0), false);

            if (state == ConnectionState.Disconnected)
                onDisconnected?.Invoke(new Connection(0), DisconnectReason.ClientRequest, false);

            clientState = state;
        }

        public void Disconnect()
        {
            if (_client == null)
                return;

            _client.Stop();
            _client = null;
        }

        public void RaiseDataReceived(Connection conn, ByteData data, bool asServer)
        {
            onDataReceived?.Invoke(conn, data, asServer);
        }

        public void RaiseDataSent(Connection conn, ByteData data, bool asServer)
        {
            onDataSent?.Invoke(conn, data, asServer);
        }

        public void SendToClient(Connection target, ByteData data, Channel method = Channel.ReliableOrdered)
        {
            if (listenerState is not ConnectionState.Connected)
                return;

            if (!target.isValid)
                return;

            _server.SendToConnection(target.connectionId, data, method);
            RaiseDataSent(target, data, true);
        }

        public void SendToServer(ByteData data, Channel method = Channel.ReliableOrdered)
        {
            _client.Send(data, method);
            RaiseDataSent(default, data, false);
        }

        public void CloseConnection(Connection conn)
        {
            _server.CloseConnection(conn.connectionId);
        }

        public void ReceiveMessages(float delta)
        {
            _server?.ReceiveMessages();
            _client?.ReceiveMessages();
        }

        public void SendMessages(float delta)
        {
            _server?.SendMessages();
            _client?.SendMessages();
        }
    }
}