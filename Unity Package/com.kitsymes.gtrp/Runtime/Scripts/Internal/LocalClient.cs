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
        private Queue<Packet> _tcpPacketQueue = new Queue<Packet>();
        private Queue<Packet> _udpPacketQueue = new Queue<Packet>();

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

            Debug.Log("Connected");

            ReceiveTCP();
            ReceiveUDP();

            await WriteTCP(new PacketConnect() { udpEndPoint = _udpClient.Client.LocalEndPoint as IPEndPoint });
            return true;
        }

        public void Stop()
        {
            _running = false;
            _tcpClient.Close();
            _udpClient?.Close();
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
                    if (packet is PacketServerInfo)
                    {
                        _networkManager.ClientPacketReceived(packet);
                        while (_tcpPacketQueue.Count > 0)
                            _networkManager.ClientPacketReceived(_tcpPacketQueue.Dequeue());
                        while (_udpPacketQueue.Count > 0)
                            _networkManager.ClientPacketReceived(_udpPacketQueue.Dequeue());
                        _serverInfoReceived = true;
                    }
                    else
                        ProcessTCPPacket(packet);
                }
                catch (IOException)
                {
                    Debug.Log("CLIENT: TCP Closed");
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
            Debug.Log("Client stopping receiving TCP");
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
                    Debug.Log("Recieved " + packet.GetType());
                    ProcessUDPPacket(packet);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    break;
                }
                Debug.Log("we go again");
            }
            Debug.Log("Client stopping Receiving UDP");
        }

        public async Task WriteUDP(byte[] bytes)
        {
            await _udpClient.SendAsync(bytes, bytes.Length);
        }

        private void ProcessTCPPacket(Packet packet)
        {
            if (!_sceneLoaded || !_serverInfoReceived)
                _tcpPacketQueue.Enqueue(packet);
            else
                _networkManager.ClientPacketReceived(packet);
            //Debug.Log(packet.GetType());
        }

        private void ProcessUDPPacket(Packet packet)
        {
            if (!_sceneLoaded || !_serverInfoReceived)
                _udpPacketQueue.Enqueue(packet);
            else
                _networkManager.ClientPacketReceived(packet);
            //Debug.Log(packet.GetType());
        }

        public void SceneLoaded()
        {
            _sceneLoaded = true;
        }

        public bool IsRunning() { return _running; }
    }
}
