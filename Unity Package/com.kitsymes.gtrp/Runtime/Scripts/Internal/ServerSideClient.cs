using KitSymes.GTRP.Packets;
using System.IO;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using System.Threading.Tasks;

namespace KitSymes.GTRP.Internal
{
    public class ServerSideClient : Client
    {
        // 0 is Server, so this ID will start at 1
        private uint _id;
        private NetworkManager _networkManager;

        private IPEndPoint _udpEndPoint;

        public ServerSideClient(NetworkManager networkManager, uint id, TcpClient tcp)
        {
            _id = id;
            _networkManager = networkManager;
            _tcpClient = tcp;
            _udpEndPoint = null;
            _running = true;

            _ = ReceiveTcp();
        }

        public void Stop()
        {
            _tcpClient.Close();
            _running = false;
        }

        public void PacketConnectReceived(PacketConnect packet)
        {
            _udpEndPoint = packet.udpEndPoint;
        }

        public async Task ReceiveTcp()
        {
            while (_running)
            {
                try
                {
                    Packet packet = await ReadTCP();
                    if (packet == null)
                    {
                        _networkManager.Disconnect(_id);
                        break;
                    }

                    // TODO This could be maliciously sent multiple times - does that impact anything?
                    if (packet is PacketConnect)
                        PacketConnectReceived((PacketConnect)packet);
                    else
                        _networkManager.ServerPacketReceived(this, packet);
                }
                catch (IOException)
                {
                    Debug.LogWarning($"SERVER: [{_id}] TCP IOException");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    Debug.Log($"SERVER: [{_id}] TCP Disposed");
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"SERVER: [{_id}] had an exception, so has been disconnected");
                    Debug.LogException(ex);
                    _networkManager.Disconnect(_id);
                }
            }
            if (_running)
            {
                Debug.LogError($"SERVER: [{_id}] TCP Closed");
            }
            Debug.Log($"SERVER: [{_id}] Client stopping receiving TCP");
        }

        public uint GetID() { return _id; }
        public IPEndPoint GetUdpEndPoint() { return _udpEndPoint; }
    }
}
