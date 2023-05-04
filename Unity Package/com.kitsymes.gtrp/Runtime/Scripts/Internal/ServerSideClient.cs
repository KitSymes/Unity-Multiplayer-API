using KitSymes.GTRP.Packets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace KitSymes.GTRP.Internal
{
    public class ServerSideClient : Client
    {
        // 0 is Server, so this ID will start at 1
        private uint _id;
        private NetworkManager _networkManager;
        private bool _connectReceived = false;

        private IPAddress _ip;
        private IPEndPoint _endPoint;

        public ServerSideClient(NetworkManager networkManager, uint id, TcpClient tcp)
        {
            _id = id;
            _networkManager = networkManager;
            _tcpClient = tcp;
            _running = true;

            _ip = ((IPEndPoint)tcp.Client.RemoteEndPoint).Address;

            _ = ReceiveTcp();
        }

        public void Stop()
        {
            _tcpClient.Close();
            _tcpClient.Dispose();
            _running = false;
        }

        public void PacketConnectReceived(PacketConnect packet)
        {
            if (_connectReceived)
                return;
            _connectReceived = true;

            _endPoint = new IPEndPoint(_ip, packet.udpEndPoint.Port);
            _otherPublicKey = packet.publicKey;

            List<Packet> packets = new List<Packet>();
            packets.Add(new PacketServerInfo() {
                yourClientID = GetID(),
                publicKey = _publicKey
            });

            if (_networkManager.GetSpawnedObjects().Count > 0)
            {
                foreach (NetworkObject obj in _networkManager.GetSpawnedObjects().Values)
                {
                    //Debug.Log($"Spawning {obj} at {obj.transform.position}");
                    packets.Add(obj.GetSpawnPacket());
                    packets.AddRange(obj.GetAllFullSyncPackets());
                }
            }

            _networkManager.SendTo(this, packets.ToArray());
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
            //Debug.Log($"SERVER: [{_id}] Client stopping receiving TCP");
        }

        public new async Task WriteTCP(byte[] data)
        {
            await base.WriteTCP(data);
        }

        public new async Task WriteTCP(Packet packet, bool useEncryption = false)
        {
            await base.WriteTCP(packet, useEncryption);
        }

        public uint GetID() { return _id; }
        public IPEndPoint GetUdpEndPoint() { return _endPoint; }
    }
}
