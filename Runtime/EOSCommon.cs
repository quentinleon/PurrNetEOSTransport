using System;
using PurrNet.Transports;
#if EOS_SDK
using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using PlayEveryWare.EpicOnlineServices;
#endif

namespace PurrNet.EOS
{
    public abstract class EOSCommon
    {
        protected EOSTransport _transport;
        
        private ConnectionState _connectionState = ConnectionState.Disconnected;

        public ConnectionState connectionState
        {
            get => _connectionState;
            protected set
            {
                if (_connectionState == value)
                    return;

                _connectionState = value;
                OnConnectionStateChanged(_connectionState);
            }
        }

        public void Initialize(EOSTransport transport)
        {
            _transport = transport;
        }

        protected abstract void OnConnectionStateChanged(ConnectionState state);

        public virtual void SendMessages()
        {
            // EOS sends messages immediately, no batching needed
        }

#if EOS_SDK
        protected P2PInterface GetP2PInterface()
        {
            return EOSManager.Instance?.GetEOSPlatformInterface()?.GetP2PInterface();
        }

        protected ProductUserId GetLocalProductUserId()
        {
            var connectInterface = EOSManager.Instance?.GetEOSPlatformInterface()?.GetConnectInterface();
            return connectInterface?.GetLoggedInUserByIndex(0);
        }

        protected PacketReliability GetReliability(Channel channel)
        {
            return channel switch
            {
                Channel.Unreliable => PacketReliability.UnreliableUnordered,
                Channel.UnreliableSequenced => PacketReliability.UnreliableUnordered,
                Channel.ReliableOrdered => PacketReliability.ReliableOrdered,
                Channel.ReliableUnordered => PacketReliability.ReliableUnordered,
                _ => PacketReliability.ReliableOrdered
            };
        }

        protected Result Send(ProductUserId localUserId, ProductUserId remoteUserId, string socketName, ByteData data, Channel channel)
        {
            var options = new SendPacketOptions
            {
                LocalUserId = localUserId,
                RemoteUserId = remoteUserId,
                SocketId = new SocketId { SocketName = socketName },
                Channel = 0,
                Data = new ArraySegment<byte>(data.data, data.offset, data.length),
                AllowDelayedDelivery = true,
                Reliability = GetReliability(channel)
            };

            var p2pInterface = GetP2PInterface();
            var result = p2pInterface?.SendPacket(ref options) ?? Result.NotFound;

            if (result != Result.Success)
            {
                UnityEngine.Debug.LogError($"Failed to send packet: {result}");
            }

            return result;
        }

        protected bool Receive(ProductUserId localUserId, string expectedSocketName, out ProductUserId remoteUserId, out ByteData data, out byte channel)
        {
            remoteUserId = null;
            data = ByteData.empty;
            channel = 0;

            var p2pInterface = GetP2PInterface();
            if (p2pInterface == null)
                return false;

            var getSizeOptions = new GetNextReceivedPacketSizeOptions
            {
                LocalUserId = localUserId
            };
            
            var sizeResult = p2pInterface.GetNextReceivedPacketSize(ref getSizeOptions, out var packetSize);
            if (sizeResult != Result.Success)
                return false;
                
            var receiveOptions = new ReceivePacketOptions
            {
                LocalUserId = localUserId,
                MaxDataSizeBytes = packetSize
            };
            
            var buffer = new ArraySegment<byte>(new byte[packetSize]);
            var socketId = new SocketId();
            
            var result = p2pInterface.ReceivePacket(ref receiveOptions, ref remoteUserId, ref socketId, out channel, buffer, out var bytesWritten);
            
            if (result != Result.Success)
                return false;
                
            if (socketId.SocketName != expectedSocketName)
                return false;

            data = new ByteData(buffer.Array, buffer.Offset, (int)bytesWritten);
            return true;
        }

        protected ulong GetIncomingPacketCount()
        {
            var p2pInterface = GetP2PInterface();
            if (p2pInterface == null)
                return 0;
                
            var packetQueueOptions = new GetPacketQueueInfoOptions();
            var queueResult = p2pInterface.GetPacketQueueInfo(ref packetQueueOptions, out var queueInfo);
            
            return queueResult == Result.Success ? queueInfo.IncomingPacketQueueCurrentPacketCount : 0;
        }
#endif
    }
}