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
    public class Client
    {
        // 0 is Server, so this ID will start at 1
        private uint _id;
        private NetworkManager _networkManager;

        private TcpClient _tcpClient;
        private SemaphoreSlim _tcpWriteAsyncLock;

        private IPEndPoint _udpEndPoint;

        private bool _running;

        private BinaryFormatter _formatter;

        public Client(NetworkManager networkManager, uint id, TcpClient tcp)
        {
            _id = id;
            _tcpClient = tcp;
            _tcpWriteAsyncLock = new SemaphoreSlim(1, 1);
            _udpEndPoint = null;
            _running = true;
            _formatter = new BinaryFormatter();

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
                    // Read until a packet size is encountered
                    byte[] sizeBuffer = new byte[sizeof(uint)];
                    int bytesRead = await _tcpClient.GetStream().ReadAsync(sizeBuffer);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    else if (bytesRead < sizeBuffer.Length)
                    {
                        Debug.LogError("Invalid sizeBuffer, read " + bytesRead + " bytes when it should be " + sizeBuffer.Length);
                        // TODO Disconnect Client
                        continue;
                    }
                    uint bufferSize = BitConverter.ToUInt32(sizeBuffer);

                    // Try and read the whole packet
                    byte[] packetBuffer = new byte[bufferSize];
                    bytesRead = await _tcpClient.GetStream().ReadAsync(packetBuffer);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    else if (bytesRead < packetBuffer.Length)
                    {
                        Debug.LogError("Invalid packetBuffer, read " + bytesRead + " bytes when it should be " + packetBuffer.Length);
                        // TODO Disconnect Client
                        continue;
                    }

                    // Deserialise Packet
                    MemoryStream ms = new MemoryStream(packetBuffer);
                    Packet packet = _formatter.Deserialize(ms) as Packet;

                    // TODO This could be maliciously sent multiple times - does that impact anything?
                    if (packet is PacketConnect)
                        PacketConnectReceived((PacketConnect)packet);
                    else
                        _networkManager.ServerPacketReceived(this, packet);
                }
                catch (IOException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    Debug.Log("SERVER: [" + _id + "] TCP Disposed");
                    break;
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
