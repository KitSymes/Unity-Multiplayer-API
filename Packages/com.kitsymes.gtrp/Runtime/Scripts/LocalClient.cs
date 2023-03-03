using KitSymes.GTRP.Packets;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace KitSymes.GTRP.Internal
{
    public class LocalClient
    {
        private NetworkManager _networkManager;
        private string _ip;
        private int _port;

        private bool _running = false;

        private TcpClient _tcpClient;
        private UdpClient _udpClient;

        private SemaphoreSlim _tcpWriteAsyncLock;
        private BinaryFormatter _formatter;

        public LocalClient(NetworkManager networkManager, string ip, int port)
        {
            _networkManager = networkManager;
            _ip = ip;
            _port = port;

            _tcpClient = new TcpClient();
            _udpClient = new UdpClient();

            _tcpWriteAsyncLock = new SemaphoreSlim(1, 1);
        }

        public async Task<bool> Connect()
        {
            IPAddress address;
            if (!IPAddress.TryParse(_ip, out address))
                return false;

            try
            {
                await _tcpClient.ConnectAsync(address, _port);
            }
            catch (SocketException)
            {
                Debug.LogError("Client could not connect");
                return false;
            }

            _formatter = new BinaryFormatter();

            _udpClient.Connect(address, _port);

            _running = true;

            ReceiveTcp();
            ReceiveUdp();

            Debug.Log("Connected");

            SendTCP(new PacketConnect() { udpEndPoint = _udpClient.Client.LocalEndPoint });
            return true;
        }

        public void Stop()
        {
            _running = false;
            _tcpClient.Close();
            _udpClient?.Close();
        }

        public async void SendTCP(Packet packet)
        {
            await _tcpWriteAsyncLock.WaitAsync();

            try
            {
                MemoryStream ms = new MemoryStream();
                _formatter.Serialize(ms, packet);
                byte[] buffer = ms.GetBuffer();

                // Send Packet Size + Packet
                await _tcpClient.GetStream().WriteAsync(BitConverter.GetBytes((uint)buffer.Length));
                await _tcpClient.GetStream().WriteAsync(buffer);

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
                        continue;
                    }

                    // Deserialise Packet
                    MemoryStream ms = new MemoryStream(packetBuffer);
                    Packet packet = _formatter.Deserialize(ms) as Packet;
                    _networkManager.ClientPacketReceived(packet);
                }
                catch (IOException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    Debug.Log("TCP Disposed");
                    break;
                }
            }
            if (_running)
            {
                Debug.LogError("TCP Closed whilst client is still running");
                _networkManager.ClientStop();
            }
            Debug.Log("Client stopping receiving TCP");
        }

        public async void ReceiveUdp()
        {
            while (_running)
            {
                try
                {
                    UdpReceiveResult result = await _udpClient.ReceiveAsync();

                    // Deserialise Packet
                    MemoryStream ms = new MemoryStream(result.Buffer);
                    Packet packet = _formatter.Deserialize(ms) as Packet;
                    _networkManager.ClientPacketReceived(packet);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
            Debug.Log("Client stopping Receiving UDP");
        }

        public bool IsRunning() { return _running; }
    }
}
