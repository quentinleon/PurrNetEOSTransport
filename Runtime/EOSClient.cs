using System;
using PurrNet.Transports;
#if EOS_SDK
using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
#endif

namespace PurrNet.EOS
{
    public class EOSClient : EOSCommon
    {
        private string _remoteProductUserId;

        public event Action<ByteData> onDataReceived;
        public event Action<ConnectionState> onConnectionState;

        public void Connect(string remoteProductUserId)
        {
#if EOS_SDK
            try
            {
                _remoteProductUserId = remoteProductUserId;
                connectionState = ConnectionState.Connecting;
                
                var localUserId = GetLocalProductUserId();
                var socketId = new SocketId { SocketName = _transport.socketName };
                
                var acceptOptions = new AcceptConnectionOptions
                {
                    LocalUserId = localUserId,
                    RemoteUserId = ProductUserId.FromString(remoteProductUserId),
                    SocketId = socketId
                };
                
                var result = GetP2PInterface().AcceptConnection(ref acceptOptions);
                
                if (result == Result.Success)
                {
                    connectionState = ConnectionState.Connected;
                }
                else
                {
                    connectionState = ConnectionState.Disconnected;
                    UnityEngine.Debug.LogError($"Failed to connect to EOS server: {result}");
                }
            }
            catch (Exception e)
            {
                connectionState = ConnectionState.Disconnected;
                UnityEngine.Debug.LogError($"Failed to connect to EOS server: {e}");
            }
#endif
        }

        public void Send(ByteData data, Channel channel)
        {
#if EOS_SDK
            if (connectionState != ConnectionState.Connected)
                return;

            var localUserId = GetLocalProductUserId();
            var remoteUserId = ProductUserId.FromString(_remoteProductUserId);
            
            if (localUserId != null && remoteUserId != null)
            {
                Send(localUserId, remoteUserId, _transport.socketName, data, channel);
            }
#endif
        }

        public void ReceiveMessages()
        {
#if EOS_SDK
            if (connectionState != ConnectionState.Connected)
                return;

            var localUserId = GetLocalProductUserId();
            if (localUserId == null)
                return;
                
            var packetCount = GetIncomingPacketCount();
            
            for (ulong i = 0; i < packetCount; i++)
            {
                if (Receive(localUserId, _transport.socketName, out var remoteUserId, out var data, out var channel))
                {
                    // Only accept packets from the server we're connected to
                    if (remoteUserId.ToString() != _remoteProductUserId)
                        continue;

                    onDataReceived?.Invoke(data);
                }
            }
#endif
        }


        public void Stop()
        {
#if EOS_SDK
            if (connectionState != ConnectionState.Disconnected)
            {
                connectionState = ConnectionState.Disconnecting;

                var options = new CloseConnectionOptions
                {
                    LocalUserId = GetLocalProductUserId(),
                    RemoteUserId = ProductUserId.FromString(_remoteProductUserId),
                    SocketId = new SocketId { SocketName = _transport.socketName }
                };

                var p2pInterface = GetP2PInterface();
                p2pInterface.CloseConnection(ref options);

                connectionState = ConnectionState.Disconnected;
            }
#endif
        }

        protected override void OnConnectionStateChanged(ConnectionState state)
        {
            onConnectionState?.Invoke(state);
        }
    }
}