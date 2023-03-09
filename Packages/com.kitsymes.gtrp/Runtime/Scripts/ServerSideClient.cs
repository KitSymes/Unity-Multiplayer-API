using KitSymes.GTRP.Packets;
using System.IO;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;

namespace KitSymes.GTRP.Internal
{
    public class ServerSideClient : Client
    {
        // 0 is Server, so this ID will start at 1
        private uint _id;
        private NetworkManager _networkManager;

        private SemaphoreSlim _tcpWriteAsyncLock;

        private IPEndPoint _udpEndPoint;

        private bool _running;

        public ServerSideClient(NetworkManager networkManager, uint id, TcpClient tcp)
        {
            _id = id;
            _tcpClient = tcp;
            _tcpWriteAsyncLock = new SemaphoreSlim(1, 1);
            _udpEndPoint = null;
            _running = true;

            ReceiveTcp();
        }

        public void Stop()
        {
            _tcpClient.Close();
        }

        public void PacketConnectReceived(PacketConnect packet)
        {
            // Validate Packet
            if (packet.udpEndPoint is not IPEndPoint)
            {
                Debug.LogError("Client " + _id + " tried to send invalid PacketConnect");
                return;
            }

            _udpEndPoint = packet.udpEndPoint as IPEndPoint;
        }

        public async void SendTCP(byte[] packets)
        {
            await _tcpWriteAsyncLock.WaitAsync();

            try
            {
                await _tcpClient.GetStream().WriteAsync(packets);
                // Flush Stream
                await _tcpClient.GetStream().FlushAsync();
            }
            finally
            {
                _tcpWriteAsyncLock.Release();
            }
        }

        public async void ReceiveTcp()
        {
            while (_running)
            {
                try
                {
                    Packet packet = await ReadTCP();
                    if (packet == null)
                        break;

                    // TODO This could be maliciously sent multiple times - does that impact anything?
                    if (packet is PacketConnect)
                        PacketConnectReceived((PacketConnect)packet);
                    else
                        _networkManager.ServerPacketReceived(this, packet);
                }
                catch (IOException)
                {
                    Debug.Log("SERVER: [" + _id + "] TCP IOException");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    Debug.Log("SERVER: [" + _id + "] TCP Disposed");
                    break;
                }
                catch (ClientException ex)
                {
                    Debug.LogException(ex);
                }
            }
            if (_running)
            {
                Debug.LogError("SERVER: [" + _id + "] TCP Closed");
            }
            Debug.Log("SERVER: [" + _id + "] Client stopping receiving TCP");
        }

        public uint GetID() { return _id; }
        public IPEndPoint GetUdpEndPoint() { return _udpEndPoint; }
    }
}
