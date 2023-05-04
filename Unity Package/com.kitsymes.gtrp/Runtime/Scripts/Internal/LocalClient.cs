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
    public class LocalClient : Client
    {
        private NetworkManager _networkManager;
        private string _ip;
        private int _port;

        private UdpClient _udpClient;
        private bool _sceneLoaded = false;
        private bool _serverInfoReceived = false;

        protected bool _canSend = false;
        private Queue<Packet> _tcpPacketQueue = new Queue<Packet>();
        private Queue<Packet> _udpPacketQueue = new Queue<Packet>();
        private Queue<Packet> _tcpEncryptedPacketQueue = new Queue<Packet>();
        private Queue<Packet> _udpEncryptedPacketQueue = new Queue<Packet>();

        public LocalClient(NetworkManager networkManager, string ip, int port)
        {
            _networkManager = networkManager;
            _ip = ip;
            _port = port;

            _tcpClient = new TcpClient();
            _udpClient = new UdpClient();
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

            _udpClient.Connect(address, _port);

            _running = true;

            //Debug.Log("Connected");

            ReceiveTCP();
            ReceiveUDP();

            await base.WriteTCP(new PacketConnect()
            {
                udpEndPoint = _udpClient.Client.LocalEndPoint as IPEndPoint,
                publicKey = _publicKey
            });
            return true;
        }

        public void Stop()
        {
            _running = false;
            _tcpClient.Close();
            _tcpClient.Dispose();
            _udpClient?.Close();
            _udpClient?.Dispose();
        }

        public async void ReceiveTCP()
        {
            while (_running)
            {
                try
                {
                    Packet packet = await ReadTCP();
                    if (packet == null)
                        break;
                    // Debug.Log("Recieved " + packet.GetType());
                    if (packet is PacketServerInfo serverInfo)
                    {
                        _otherPublicKey = serverInfo.publicKey;
                        _networkManager.ClientPacketReceived(packet);
                        _serverInfoReceived = true;
                        _canSend = true;
                        while (_tcpPacketQueue.Count > 0)
                            await base.WriteTCP(_tcpPacketQueue.Dequeue());
                        while (_tcpEncryptedPacketQueue.Count > 0)
                            await base.WriteTCP(_tcpEncryptedPacketQueue.Dequeue(), true);
                        while (_udpPacketQueue.Count > 0)
                            await WriteUDP(_udpPacketQueue.Dequeue());
                        while (_udpEncryptedPacketQueue.Count > 0)
                            await WriteUDP(_udpEncryptedPacketQueue.Dequeue(), true);
                    }
                    else
                        ProcessTCPPacket(packet);
                }
                catch (ObjectDisposedException)
                {
                    //Debug.Log("CLIENT: TCP Closed");
                    break;
                }
                catch (IOException)
                {
                    //Debug.Log("CLIENT: TCP Closed");
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogError("CLIENT: Error vvvv");
                    Debug.LogException(ex);
                    break;
                }
            }
            if (_running)
            {
                Debug.LogWarning("TCP Closed whilst client is still running");
                _networkManager.ClientStop();
            }
            //Debug.Log("Client stopping receiving TCP");
        }

        public new async Task WriteTCP(byte[] data)
        {
            await base.WriteTCP(data);
        }

        public new async Task WriteTCP(Packet packet, bool useEncryption = false)
        {
            if (!_canSend)
            {
                if (useEncryption)
                    _tcpEncryptedPacketQueue.Enqueue(packet);
                else
                    _tcpPacketQueue.Enqueue(packet);
                return;
            }

            await base.WriteTCP(packet, useEncryption);
        }

        public async void ReceiveUDP()
        {
            while (_running)
            {
                try
                {
                    UdpReceiveResult result = await _udpClient.ReceiveAsync();

                    // Deserialise Packet
                    Packet packet = PacketFormatter.Deserialise(result.Buffer);
                    //Debug.Log("Recieved " + packet.GetType());
                    ProcessUDPPacket(packet);
                }
                catch (ObjectDisposedException)
                {
                    // UDP is closing
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    break;
                }
            }
            //Debug.Log("Client stopping Receiving UDP");
        }

        public async Task WriteUDP(byte[] bytes)
        {
            await _udpClient.SendAsync(bytes, bytes.Length);
        }

        public async Task WriteUDP(Packet packet, bool useEncryption = false)
        {
            if (!_canSend)
            {
                if (useEncryption)
                    _udpEncryptedPacketQueue.Enqueue(packet);
                else
                    _udpPacketQueue.Enqueue(packet);
                return;
            }

            byte[] buffer = PacketFormatter.Serialise(packet);

            // Encrypt the data if needed
            if (useEncryption)
            {
                // Import the other side's public key
                _rsaProvider.ImportParameters(_otherPublicKey);
                // Encrypt the packet
                buffer = _rsaProvider.Encrypt(buffer, true);
                // Create a new packet so the other side knows it's encrypted
                PacketEncrypted packetEncrypted = new PacketEncrypted() { encryptedData = buffer };
                // Serialise the encrypted packet
                buffer = PacketFormatter.Serialise(packetEncrypted);
            }

            // Send Data
            await _udpClient.SendAsync(buffer, buffer.Length);
        }

        private void ProcessTCPPacket(Packet packet)
        {
            if (!_sceneLoaded || !_serverInfoReceived)
                return;
            else
                _networkManager.ClientPacketReceived(packet);
        }

        private void ProcessUDPPacket(Packet packet)
        {
            if (!_sceneLoaded || !_serverInfoReceived)
                return;
            else
                _networkManager.ClientPacketReceived(packet);
        }

        public void SceneLoaded()
        {
            _sceneLoaded = true;
        }

        public bool IsRunning() { return _running; }
    }
}
