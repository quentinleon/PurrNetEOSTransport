using System;
using System.Collections.Generic;
using PurrNet.Transports;
#if EOS_SDK
using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
#endif

namespace PurrNet.EOS
{
    public class EOSServer : EOSCommon
    {
        private Dictionary<int, string> _connectionToProductUserId = new Dictionary<int, string>();
        private Dictionary<string, int> _productUserIdToConnection = new Dictionary<string, int>();
        private int _nextConnectionId = 1;

        public event Action<int, ByteData> onDataReceived;
        public event Action<int> onRemoteConnected;
        public event Action<int> onRemoteDisconnected;


        public bool Listen()
        {
#if EOS_SDK
            try
            {
                var localUserId = GetLocalProductUserId();
                var socketId = new SocketId { SocketName = _transport.socketName };
                
                var addNotifyOptions = new AddNotifyPeerConnectionRequestOptions
                {
                    LocalUserId = localUserId,
                    SocketId = socketId
                };
                
                var p2pInterface = GetP2PInterface();
                p2pInterface.AddNotifyPeerConnectionRequest(ref addNotifyOptions, null, OnPeerConnectionRequest);
                
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to start EOS server: {e}");
                return false;
            }
#else
            return false;
#endif
        }

        public void SendToConnection(int connectionId, ByteData data, Channel channel)
        {
#if EOS_SDK
            if (!_connectionToProductUserId.TryGetValue(connectionId, out string productUserId))
                return;

            var localUserId = GetLocalProductUserId();
            var remoteUserId = ProductUserId.FromString(productUserId);
            
            if (localUserId != null && remoteUserId != null)
            {
                Send(localUserId, remoteUserId, _transport.socketName, data, channel);
            }
#endif
        }

        public void CloseConnection(int connectionId)
        {
#if EOS_SDK
            if (_connectionToProductUserId.TryGetValue(connectionId, out string productUserId))
            {
                var options = new CloseConnectionOptions
                {
                    LocalUserId = GetLocalProductUserId(),
                    RemoteUserId = ProductUserId.FromString(productUserId),
                    SocketId = new SocketId { SocketName = _transport.socketName }
                };

                var p2pInterface = GetP2PInterface();
                p2pInterface.CloseConnection(ref options);

                _connectionToProductUserId.Remove(connectionId);
                _productUserIdToConnection.Remove(productUserId);
            }
#endif
        }

        public void ReceiveMessages()
        {
#if EOS_SDK
            var localUserId = GetLocalProductUserId();
            if (localUserId == null)
                return;
                
            var packetCount = GetIncomingPacketCount();
            
            for (ulong i = 0; i < packetCount; i++)
            {
                if (Receive(localUserId, _transport.socketName, out var remoteUserId, out var data, out var channel))
                {
                    string productUserIdStr = remoteUserId.ToString();
                    
                    if (!_productUserIdToConnection.TryGetValue(productUserIdStr, out int connectionId))
                    {
                        connectionId = _nextConnectionId++;
                        _connectionToProductUserId[connectionId] = productUserIdStr;
                        _productUserIdToConnection[productUserIdStr] = connectionId;
                        onRemoteConnected?.Invoke(connectionId);
                    }

                    onDataReceived?.Invoke(connectionId, data);
                }
            }
#endif
        }


        public void Stop()
        {
#if EOS_SDK
            foreach (var kvp in _connectionToProductUserId)
            {
                CloseConnection(kvp.Key);
            }
            
            _connectionToProductUserId.Clear();
            _productUserIdToConnection.Clear();
#endif
        }

#if EOS_SDK
        private void OnPeerConnectionRequest(ref OnIncomingConnectionRequestInfo data)
        {
            var acceptOptions = new AcceptConnectionOptions
            {
                LocalUserId = data.LocalUserId,
                RemoteUserId = data.RemoteUserId,
                SocketId = data.SocketId
            };
            
            var p2pInterface = GetP2PInterface();
            var result = p2pInterface?.AcceptConnection(ref acceptOptions) ?? Result.NotFound;
            
            if (result == Result.Success)
            {
                int connectionId = _nextConnectionId++;
                string productUserIdStr = data.RemoteUserId.ToString();
                _connectionToProductUserId[connectionId] = productUserIdStr;
                _productUserIdToConnection[productUserIdStr] = connectionId;
                onRemoteConnected?.Invoke(connectionId);
            }
        }
#endif

        protected override void OnConnectionStateChanged(ConnectionState state)
        {
            // Server connection state changes are handled by transport
        }
    }
}