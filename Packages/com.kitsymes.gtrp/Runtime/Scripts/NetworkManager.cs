using KitSymes.GTRP.Internal;
using KitSymes.GTRP.Packets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KitSymes.GTRP
{
    [Serializable]
    public partial class NetworkManager
    {
        // Delegates and Events
        public delegate void EventSubscriber();
        public event EventSubscriber OnServerStart;
        public event EventSubscriber OnServerStop;
        public event EventSubscriber OnClientStart;
        public event EventSubscriber OnClientStop;

        // Shared
        private static NetworkManager _instance;

        public List<NetworkObject> _spawnableObjects;
        private uint _spawnedObjectsCount;
        public Dictionary<uint, NetworkObject> _spawnedObjects;

        // Server Only
        private Dictionary<Type, Action<ServerSideClient, Packet>> _serverHandlers = new();
        private bool _serverRunning = false;
        private uint _clientCount;
        private Dictionary<uint, ServerSideClient> _clients;
        private TcpListener _tcpListener;
        private UdpClient _udpClient;
        private BinaryFormatter _formatter;

        // Client Only
        private Dictionary<Type, Action<Packet>> _clientHandlers = new();
        private LocalClient _localClient;

        void SharedStart()
        {
            if (_instance == null)
                _instance = this;

            if (_spawnableObjects == null)
                _spawnableObjects = new List<NetworkObject>();

            // Only load the scene if has not already loaded it because it's both.
            if (!IsServerRunning() || !IsClientRunning())
            {
                _spawnedObjects = new Dictionary<uint, NetworkObject>();
            }
        }

        void SharedStop()
        {
            if (_instance != null && _instance == this && !IsClientRunning() && !IsServerRunning())
                _instance = null;

            if (!IsServerRunning() && !IsClientRunning())
            {
                _spawnedObjects.Clear();
            }
        }

        partial void RegisterPackets();

        public void Update()
        {
            if (IsServerRunning())
            {
                List<Packet> tcpPackets = new List<Packet>();
                List<Packet> udpPackets = new List<Packet>();
                foreach (NetworkObject networkObject in _spawnedObjects.Values)
                {
                    tcpPackets.AddRange(networkObject.GetTCPPackets());
                    udpPackets.AddRange(networkObject.GetUDPPackets());
                    networkObject.ClearPackets();
                }
                SendToAll(tcpPackets.ToArray());
                Broadcast(udpPackets.ToArray());
            }
        }

        #region Server Methods
        public void ServerStart(string ip = "127.0.0.1", int port = 25565)
        {
            if (_serverRunning)
                return;

            _serverRunning = true;
            _clientCount = 0;
            _clients = new Dictionary<uint, ServerSideClient>();
            _spawnedObjectsCount = 0;

            _tcpListener = new TcpListener(System.Net.IPAddress.Any, port);
            _tcpListener.Start();
            _ = AcceptTCPClients();

            _udpClient = new UdpClient(port);
            _ = UdpListen();
            _formatter = new BinaryFormatter();

            SharedStart();
            //RegisterServerPacketHandler<Packet>(ServerOnPacketConnectReceived);

            OnServerStart?.Invoke();
        }

        public void ServerStop()
        {
            if (!_serverRunning)
                return;
            _serverRunning = false;

            foreach (ServerSideClient c in _clients.Values)
            {
                c.Stop();
            }
            _clients.Clear();

            _tcpListener.Stop();
            _udpClient.Close();
            _formatter = null;

            _clientHandlers.Clear();

            SharedStop();

            OnServerStop?.Invoke();
        }

        public bool IsServerRunning()
        {
            return _serverRunning;
        }

        private async Task AcceptTCPClients()
        {
            while (_serverRunning)
            {
                Task<TcpClient> task = _tcpListener.AcceptTcpClientAsync();
                try
                {
                    await task;
                    // Increment _clientCount first, because we want to count clients normally, as ID 0 represents the Server (For things such as ownerID)
                    _clientCount++;
                    ServerSideClient c = new ServerSideClient(this, _clientCount, task.Result);
                    _clients.Add(_clientCount, c);
                    if (_spawnedObjects.Count > 0)
                    {
                        List<Packet> packets = new List<Packet>();
                        foreach (NetworkObject obj in _spawnedObjects.Values)
                            packets.Add(new PacketSpawnObject
                            {
                                prefabID = obj.GetPrefabID(),
                                objectNetworkID = obj.GetNetworkID(),
                                ownerNetworkID = obj.GetOwnerID(),
                                positionX = obj.transform.position.x,
                                positionY = obj.transform.position.y,
                                positionZ = obj.transform.position.z,
                                rotationX = obj.transform.rotation.x,
                                rotationY = obj.transform.rotation.y,
                                rotationZ = obj.transform.rotation.z,
                                rotationW = obj.transform.rotation.w,
                                localScaleX = obj.transform.localScale.x,
                                localScaleY = obj.transform.localScale.y,
                                localScaleZ = obj.transform.localScale.z,
                            });
                        SendTo(c, packets.ToArray());
                    }
                    Debug.Log("Accepted a Connection");
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
            Debug.Log("Server closed so stopping accepting TCP Clients");
        }

        private async Task UdpListen()
        {
            while (_serverRunning)
            {
                try
                {
                    UdpReceiveResult result = await _udpClient.ReceiveAsync();
                    Debug.Log("Received Udp");
                }
                catch (ObjectDisposedException)
                {
                    //Debug.Log("Server closed so stopping listening to UDP");
                }
            }
        }

        // Based off of https://stackoverflow.com/questions/30378593/register-event-handler-for-specific-subclass
        public void RegisterServerPacketHandler<T>(Action<ServerSideClient, T> handler) where T : Packet
        {
            Action<ServerSideClient, Packet> wrapper = (sender, packet) => handler(sender, packet as T);
            if (_serverHandlers.ContainsKey(typeof(T)))
                _serverHandlers[typeof(T)] += wrapper;
            else
                _serverHandlers.Add(typeof(T), wrapper);
        }

        public void ServerPacketReceived(ServerSideClient sender, Packet packet)
        {
            if (_serverHandlers.ContainsKey(packet.GetType()) && _serverHandlers[packet.GetType()] != null)
                _serverHandlers[packet.GetType()].Invoke(sender, packet);
        }

        public void SendTo(uint clientID, params Packet[] packets)
        {
            if (!_clients.ContainsKey(clientID))
            {
                Debug.LogError("Attempted to send packet to invalid clientID " + clientID);
                return;
            }

            SendTo(_clients[clientID], packets);
        }
        private void SendTo(ServerSideClient client, params Packet[] packets)
        {
            if (packets.Length <= 0)
                return;

            List<byte> packetBuffer = new List<byte>();
            foreach (Packet packet in packets)
            {
                // Prepare Packet
                MemoryStream ms = new MemoryStream();
                _formatter.Serialize(ms, packet);
                byte[] buffer = ms.GetBuffer();

                // Send Packet Size + Packet
                packetBuffer.AddRange(BitConverter.GetBytes((uint)buffer.Length));
                packetBuffer.AddRange(buffer);
            }

            client.SendTCP(packetBuffer.ToArray());
        }

        public void SendToAll(params Packet[] packets)
        {
            if (packets.Length <= 0)
                return;

            List<byte> packetBuffer = new List<byte>();
            foreach (Packet packet in packets)
            {
                // Prepare Packet
                MemoryStream ms = new MemoryStream();
                _formatter.Serialize(ms, packet);
                byte[] buffer = ms.GetBuffer();

                // Packet Size + Packet
                packetBuffer.AddRange(BitConverter.GetBytes((uint)buffer.Length));
                packetBuffer.AddRange(buffer);
            }

            foreach (ServerSideClient client in _clients.Values)
                client.SendTCP(packetBuffer.ToArray());
        }

        public void Broadcast(params Packet[] packets)
        {
            if (packets.Length <= 0)
                return;

            //List<byte> packetBuffer = new List<byte>();
            foreach (Packet packet in packets)
            {
                // Prepare Packet
                MemoryStream ms = new MemoryStream();
                _formatter.Serialize(ms, packet);
                //packetBuffer.AddRange();

                foreach (ServerSideClient client in _clients.Values)
                    if (client.GetUdpEndPoint() != null)
                        _udpClient.SendAsync(ms.GetBuffer(), ms.GetBuffer().Length, client.GetUdpEndPoint());
            }

            //foreach (Client client in _clients.Values)
            //    if (client.GetUdpEndPoint() != null)
            //        _udpClient.SendAsync(packetBuffer.ToArray(), packetBuffer.Count, client.GetUdpEndPoint());
        }

        public void Spawn(NetworkObject obj)
        {
            if (!IsServerRunning())
            {
                Debug.LogError("Tried to Spawn " + obj.name + " with no server running");
                return;
            }

            if (obj.IsSpawned())
            {
                Debug.LogError("Tried to Spawn " + obj.name + " again");
                return;
            }

            _spawnedObjects[_spawnedObjectsCount] = obj;
            obj.Spawn(_spawnedObjectsCount, 0);
            _spawnedObjectsCount++;
            ExecuteEvents.Execute<INetworkMessageTarget>(obj.gameObject, null, (x, y) => x.OnServerStart());

            SendToAll(new PacketSpawnObject
            {
                prefabID = obj.GetPrefabID(),
                objectNetworkID = obj.GetNetworkID(),
                ownerNetworkID = obj.GetOwnerID(),
                positionX = obj.transform.position.x,
                positionY = obj.transform.position.y,
                positionZ = obj.transform.position.z,
                rotationX = obj.transform.rotation.x,
                rotationY = obj.transform.rotation.y,
                rotationZ = obj.transform.rotation.z,
                rotationW = obj.transform.rotation.w,
                localScaleX = obj.transform.localScale.x,
                localScaleY = obj.transform.localScale.y,
                localScaleZ = obj.transform.localScale.z,
            });
        }

        public static void Spawn(GameObject obj)
        {
            if (_instance == null)
            {
                Debug.LogError("Tried to Spawn " + obj.name + " with no server running");
                return;
            }

            NetworkObject networkObject = obj.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                Debug.LogError("Tried to Spawn " + obj.name + " but it does not have a NetworkObject component");
                return;
            }

            _instance.Spawn(networkObject);
        }

        public void Despawn(uint id, NetworkObject networkObject)
        {
            // Validate
            if (!IsServerRunning())
            {
                Debug.LogError("Tried to Despawn " + networkObject.name + " with no server running");
                return;
            }

            if (id != networkObject.GetNetworkID())
            {
                Debug.LogError("Tried to Despawn " + networkObject.name + " but it had an ID mismatch");
                return;
            }

            if (!_spawnedObjects.ContainsKey(id))
            {
                Debug.LogError("Tried to Despawn " + networkObject.name + " but it does not exist");
                return;
            }

            if (_spawnedObjects[id] != networkObject)
            {
                Debug.LogError("Tried to Despawn " + networkObject.name + " but it does not match its ID");
                return;
            }

            if (!networkObject.IsSpawned())
            {
                Debug.LogError("Tried to Despawn " + networkObject.name + " when it wasn't spawned");
                return;
            }

            _spawnedObjects.Remove(id);
            SendToAll(new PacketDespawnObject() { objectNetworkID = id });
        }

        public static void Despawn(NetworkObject networkObject)
        {
            if (_instance == null)
            {
                Debug.LogError("Tried to Despawn " + networkObject.name + " with no server running");
                return;
            }

            _instance.Despawn(networkObject.GetNetworkID(), networkObject);
        }

        public static bool IsServer()
        {
            if (_instance == null)
            {
                Debug.LogError("Tried to execute NetworkManager Static Function with no Instance running");
                return false;
            }

            return _instance.IsServerRunning();
        }
        #endregion

        #region Server Handlers
        public void ServerOnPacketReceived(ServerSideClient sender, Packet packet)
        {
            // Validate Packet

        }
        #endregion

        #region Client Methods
        public async Task ClientStart(string ip = "127.0.0.1", int port = 25565)
        {
            if (_localClient != null || IsClientRunning())
                throw new ClientException("Client is already running");

            _localClient = new LocalClient(this, ip, port);
            bool connected = await _localClient.Connect();
            if (!connected)
            {
                _localClient.Stop();
                _localClient = null;
                throw new ClientException("Client failed to connect");
            }

            SharedStart();
            RegisterClientPacketHandler<PacketSpawnObject>(ClientPacketSpawnObjectReceived);
            RegisterClientPacketHandler<PacketDespawnObject>(ClientPacketDespawnObjectReceived);
            RegisterClientPacketHandler<PacketNetworkTransformSync>(ClientPacketTargetedReceived);

            OnClientStart?.Invoke();
        }

        public void ClientStop()
        {
            if (!IsClientRunning())
                return;

            _localClient.Stop();
            _localClient = null;

            _clientHandlers.Clear();

            SharedStop();

            OnClientStop?.Invoke();
        }

        public bool IsClientRunning()
        {
            return _localClient != null && _localClient.IsRunning();
        }

        // Based off of https://stackoverflow.com/questions/30378593/register-event-handler-for-specific-subclass
        public void RegisterClientPacketHandler<T>(Action<T> handler) where T : Packet
        {
            Action<Packet> wrapper = packet => handler(packet as T);
            if (_clientHandlers.ContainsKey(typeof(T)))
                _clientHandlers[typeof(T)] += wrapper;
            else
                _clientHandlers.Add(typeof(T), wrapper);
        }

        public void ClientPacketReceived(Packet packet)
        {
            if (_clientHandlers.ContainsKey(packet.GetType()) && _clientHandlers[packet.GetType()] != null)
                _clientHandlers[packet.GetType()].Invoke(packet);
        }
        #endregion

        #region Client Handlers
        public void ClientPacketSpawnObjectReceived(PacketSpawnObject packet)
        {
            // Do not spawn duplicate if this client is host
            if (IsServerRunning())
                return;

            // Validate Packet
            if (packet.prefabID >= _spawnableObjects.Count)
                return;
            if (_spawnedObjects.ContainsKey(packet.objectNetworkID))
                return;

            GameObject newObj = UnityEngine.Object.Instantiate(_spawnableObjects[(int)packet.prefabID].gameObject);
            newObj.transform.SetPositionAndRotation(packet.GetPosition(), packet.GetRotation());
            newObj.transform.localScale = packet.GetScale();

            NetworkObject networkObject = newObj.GetComponent<NetworkObject>();
            _spawnedObjects[packet.objectNetworkID] = networkObject;
            networkObject.Spawn(packet.objectNetworkID, packet.ownerNetworkID);
        }

        public void ClientPacketDespawnObjectReceived(PacketDespawnObject packet)
        {
            // Do not spawn duplicate if this client is host
            if (IsServerRunning())
                return;

            // Validate Packet
            if (!_spawnedObjects.ContainsKey(packet.objectNetworkID))
                return;

            UnityEngine.Object.Destroy(_spawnedObjects[packet.objectNetworkID].gameObject);
            _spawnedObjects.Remove(packet.objectNetworkID);
        }

        public void ClientPacketTargetedReceived(PacketTargeted packet)
        {
            // Do not duplicate if this client is host
            if (IsServerRunning())
                return;

            // Validate Packet
            if (!_spawnedObjects.ContainsKey(packet.target))
                return;

            ExecuteEvents.Execute<INetworkMessageTarget>(_spawnedObjects[packet.target].gameObject, null,
                (x, y) => x.OnPacketReceive(packet));
        }
        #endregion

        #region Setters
        public void SetSpawnableObjects(List<NetworkObject> spawnableObjects)
        {
            if (IsServerRunning() || IsClientRunning())
                return;
            _spawnableObjects = spawnableObjects;
        }
        #endregion
    }
}
