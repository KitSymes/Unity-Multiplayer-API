using KitSymes.GTRP.Internal;
using KitSymes.GTRP.Packets;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
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
        public delegate void ClientEventSubscriber(uint key);
        public event ClientEventSubscriber OnPlayerConnect;
        public event ClientEventSubscriber OnPlayerDisconnect;

        // Shared
        private static NetworkManager _instance;

        private List<NetworkObject> _spawnableObjects = new List<NetworkObject>();
        private Dictionary<uint, NetworkObject> _spawnedObjects = new Dictionary<uint, NetworkObject>();
        [SerializeField]
        private float _tickRate = 20;

        // Server Only
        private Dictionary<Type, Action<ServerSideClient, Packet>> _serverHandlers = new();
        private bool _serverRunning = false;
        private uint _serverClientCount;
        private Dictionary<uint, ServerSideClient> _serverSideClients;
        private TcpListener _serverTCPListener;
        private UdpClient _serverUDPClient;
        private uint _serverSpawnedObjectsCount;
        private NetworkObject _serverPlayerPrefab;
        private Dictionary<uint, NetworkObject> _serverPlayers = new();

        // Client Only
        private Dictionary<Type, Action<Packet>> _clientHandlers = new();
        private LocalClient _clientLocalClient;
        private uint _clientID;

        void SharedStart()
        {
            if (_instance == null)
                _instance = this;

            if (!IsServerRunning() || !IsClientRunning())
            {

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

        float timeSinceLastTick = 0;

        public void Update()
        {
            timeSinceLastTick += Time.deltaTime;
            if (timeSinceLastTick < 1.0f / _tickRate)
                return;
            timeSinceLastTick = 0.0f;
            foreach (NetworkObject networkObject in _spawnedObjects.Values)
                networkObject.Tick();

            if (IsServerRunning())
            {
                List<Packet> tcpPackets = new List<Packet>();
                List<Packet> udpPackets = new List<Packet>();
                foreach (NetworkObject networkObject in _spawnedObjects.Values)
                {
                    tcpPackets.AddRange(networkObject.GetTCPPackets());
                    tcpPackets.AddRange(networkObject.GetAllDynamicSyncPackets());
                    udpPackets.AddRange(networkObject.GetUDPPackets());
                    networkObject.ClearPackets();
                }
                SendToAll(tcpPackets.ToArray());
                Broadcast(udpPackets.ToArray());
            }
            else if (IsClientRunning())
            {
                List<Packet> tcpPackets = new List<Packet>();
                List<Packet> udpPackets = new List<Packet>();
                foreach (NetworkObject networkObject in _spawnedObjects.Values)
                {
                    tcpPackets.AddRange(networkObject.GetTCPPackets());
                    udpPackets.AddRange(networkObject.GetUDPPackets());
                    networkObject.ClearPackets();
                }
                SendToServerTCP(tcpPackets.ToArray());
                SendToServerUDP(udpPackets.ToArray());
            }
        }

        #region Server Methods
        public bool ServerStart(int port = 25565)
        {
            if (_serverRunning)
                return false;

            _serverTCPListener = new TcpListener(System.Net.IPAddress.Any, port);
            try
            {
                _serverTCPListener.Start();
            }
            catch (SocketException)
            {
                Debug.LogError("Could not start Server");
                return false;
            }

            _serverRunning = true;
            _serverClientCount = 0;
            _serverSideClients = new Dictionary<uint, ServerSideClient>();
            _serverSpawnedObjectsCount = 0;

            _ = AcceptTCPClients();

            _serverUDPClient = new UdpClient(port);
            _ = UdpListen();

            SharedStart();
            RegisterServerPacketHandler<PacketPing>(OnServerPacketPingReceived);
            RegisterServerPacketHandler<PacketServerRPC>(OnServerPacketServerRPCReceived);
            RegisterServerPacketHandler<PacketNetworkTransformSync>(OnServerPacketTargetedReceived);

            OnServerStart?.Invoke();
            return true;
        }
        public void ServerStop()
        {
            if (!_serverRunning)
                return;
            _serverRunning = false;

            if (_serverSideClients != null)
            {
                foreach (ServerSideClient c in _serverSideClients.Values)
                {
                    c.Stop();
                }
                _serverSideClients.Clear();
            }

            _serverTCPListener.Stop();
            _serverUDPClient.Close();

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
                Task<TcpClient> task = _serverTCPListener.AcceptTcpClientAsync();
                try
                {
                    await task;
                    // Increment _clientCount first, because we want to count clients normally, as ID 0 represents the Server (For things such as ownerID)
                    _serverClientCount++;
                    ServerSideClient c = new ServerSideClient(this, _serverClientCount, task.Result);
                    _serverSideClients.Add(c.GetID(), c);

                    List<Packet> packets = new List<Packet>();

                    if (_spawnedObjects.Count > 0)
                    {
                        foreach (NetworkObject obj in _spawnedObjects.Values)
                        {
                            //Debug.Log($"Spawning {obj} at {obj.transform.position}");
                            packets.Add(obj.GetSpawnPacket());
                            packets.AddRange(obj.GetAllFullSyncPackets());
                        }
                    }

                    if (_serverPlayerPrefab != null)
                    {
                        NetworkObject playerNetworkObject = UnityEngine.Object.Instantiate(_serverPlayerPrefab);
                        playerNetworkObject.gameObject.name = "Player " + c.GetID();
                        playerNetworkObject.ChangeOwnership(c.GetID());
                        Spawn(playerNetworkObject);
                        _serverPlayers.Add(c.GetID(), playerNetworkObject);
                    }

                    packets.Add(new PacketServerInfo() { yourClientID = c.GetID() });
                    SendTo(c, packets.ToArray());

                    OnPlayerConnect?.Invoke(c.GetID());
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
                    UdpReceiveResult result = await _serverUDPClient.ReceiveAsync();

                    foreach (ServerSideClient client in _serverSideClients.Values)
                    {
                        if (result.RemoteEndPoint.Equals(client.GetUdpEndPoint()))
                        {
                            try
                            {
                                // Deserialise Packet
                                Packet packet = PacketFormatter.Deserialise(result.Buffer);
                                ServerPacketReceived(client, packet);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning(ex);
                            }
                            break;
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    //Debug.Log("Server closed so stopping listening to UDP");
                }
            }
        }

        public void Disconnect(uint id)
        {
            if (!_serverSideClients.ContainsKey(id))
                return;

            Debug.Log("SERVER: [" + id + "] Client disconnecting");

            OnPlayerDisconnect?.Invoke(id);

            ServerSideClient client = _serverSideClients[id];
            client.Stop();
            _serverSideClients.Remove(id);

            if (_serverPlayers.ContainsKey(id))
            {
                NetworkObject networkObject = _serverPlayers[id];
                UnityEngine.Object.Destroy(networkObject.gameObject);
                _serverPlayers.Remove(id);
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

        /// <summary>
        /// Send packets to a specific Client over TCP
        /// </summary>
        /// <param name="clientID">The target Client</param>
        /// <param name="packets">The packets to send</param>
        public void SendTo(uint clientID, params Packet[] packets)
        {
            if (!_serverSideClients.ContainsKey(clientID))
            {
                Debug.LogError("Attempted to send packet to invalid clientID " + clientID);
                return;
            }

            SendTo(_serverSideClients[clientID], packets);
        }
        /// <summary>
        /// Send packets to a specific Client over TCP
        /// </summary>
        /// <param name="client">The target Client</param>
        /// <param name="packets">The packets to send</param>
        private void SendTo(ServerSideClient client, params Packet[] packets)
        {
            if (packets.Length <= 0)
                return;

            List<byte> packetBuffer = new List<byte>();
            foreach (Packet packet in packets)
            {
                // Prepare Packet
                byte[] buffer = PacketFormatter.Serialise(packet);

                // Send Packet Size + Packet
                packetBuffer.AddRange(BitConverter.GetBytes((uint)buffer.Length));
                packetBuffer.AddRange(buffer);
            }

            _ = client.WriteTCP(packetBuffer.ToArray());
        }
        /// <summary>
        /// Send packets to all Clients over TCP
        /// </summary>
        /// <param name="packets">The packets to send</param>
        public void SendToAll(params Packet[] packets)
        {
            if (packets.Length <= 0)
                return;

            List<byte> packetBuffer = new List<byte>();
            foreach (Packet packet in packets)
            {
                // Prepare Packet
                byte[] buffer = PacketFormatter.Serialise(packet);

                // Packet Size + Packet
                packetBuffer.AddRange(BitConverter.GetBytes((uint)buffer.Length));
                packetBuffer.AddRange(buffer);
            }

            foreach (ServerSideClient client in _serverSideClients.Values)
                _ = client.WriteTCP(packetBuffer.ToArray());
        }

        /// <summary>
        /// Send packets to all Clients over UDP
        /// </summary>
        /// <param name="packets">The packets to send</param>
        public void Broadcast(params Packet[] packets)
        {
            if (packets.Length <= 0)
                return;

            //List<byte> packetBuffer = new List<byte>();
            foreach (Packet packet in packets)
            {
                // Prepare Packet
                byte[] bytes = PacketFormatter.Serialise(packet);

                foreach (ServerSideClient client in _serverSideClients.Values)
                    if (client.GetUdpEndPoint() != null)
                        _serverUDPClient.SendAsync(bytes, bytes.Length, client.GetUdpEndPoint());
            }

            //foreach (Client client in _clients.Values)
            //    if (client.GetUdpEndPoint() != null)
            //        _udpClient.SendAsync(packetBuffer.ToArray(), packetBuffer.Count, client.GetUdpEndPoint());
        }

        public NetworkObject GetPlayer(uint id)
        {
            if (!_serverPlayers.ContainsKey(id))
                return null;
            return _serverPlayers[id];
        }

        // Static Functions
        public static NetworkManager GetInstance()
        {
            return _instance;
        }
        public static bool IsServer()
        {
            if (_instance == null)
                return false;

            return _instance.IsServerRunning();
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

            _spawnedObjects[_serverSpawnedObjectsCount] = obj;
            obj.Spawn(_serverSpawnedObjectsCount, obj.GetOwnerID());
            _serverSpawnedObjectsCount++;
            ExecuteEvents.Execute<INetworkMessageTarget>(obj.gameObject, null, (x, y) => x.OnServerStart());

            List<Packet> packets = new List<Packet>();
            packets.Add(obj.GetSpawnPacket());
            packets.AddRange(obj.GetAllFullSyncPackets());

            SendToAll(packets.ToArray());
        }
        public static void Spawn(GameObject obj)
        {
            if (_instance == null)
            {
                Debug.LogError("Tried to Spawn " + obj.name + " with no instance running");
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
                Debug.LogError("Tried to Despawn " + networkObject.name + " with no instance running");
                return;
            }

            _instance.Despawn(networkObject.GetNetworkID(), networkObject);
        }
        #endregion

        #region Server Handlers
        public void OnServerPacketPingReceived(ServerSideClient sender, PacketPing packet)
        {
            Debug.Log($"{sender.GetID()} Ping");
            _ = sender.WriteTCP(new PacketPong());
        }

        private void OnServerPacketServerRPCReceived(ServerSideClient sender, PacketServerRPC packet)
        {
            // Validate Packet
            if (!_spawnedObjects.ContainsKey(packet.networkObjectID))
                return;
            NetworkObject networkObject = _spawnedObjects[packet.networkObjectID];
            if (networkObject.GetOwnerID() != sender.GetID())
                return;
            if (!networkObject.HasNetworkBehaviour(packet.networkBehaviourID))
                return;
            NetworkBehaviour networkBehaviour = networkObject.GetNetworkBehaviour(packet.networkBehaviourID);

            networkBehaviour.OnPacketServerRPCReceive(packet);
        }

        private void OnServerPacketTargetedReceived(ServerSideClient sender, PacketTargeted packet)
        {
            // Validate Packet
            if (!_spawnedObjects.ContainsKey(packet.target))
                return;
            NetworkObject networkObject = _spawnedObjects[packet.target];
            if (networkObject.GetOwnerID() != sender.GetID())
                return;
            if (!networkObject.HasAuthority())
                return;

            ExecuteEvents.Execute<INetworkMessageTarget>(networkObject.gameObject, null,
                (x, y) => x.OnPacketReceive(packet));
        }
        #endregion

        #region Client Methods
        public async Task ClientStart(string ip = "127.0.0.1", int port = 25565)
        {
            if (_clientLocalClient != null || IsClientRunning())
                throw new ClientException("Client is already running");

            _clientLocalClient = new LocalClient(this, ip, port);
            bool connected = await _clientLocalClient.Connect();
            if (!connected)
            {
                _clientLocalClient.Stop();
                _clientLocalClient = null;
                throw new ClientException("Client failed to connect");
            }

            SharedStart();
            RegisterClientPacketHandler<PacketServerInfo>(OnClientPacketServerInfoReceived);
            RegisterClientPacketHandler<PacketPong>(OnClientPacketPongReceived);
            RegisterClientPacketHandler<PacketSpawnObject>(OnClientPacketSpawnObjectReceived);
            RegisterClientPacketHandler<PacketDespawnObject>(OnClientPacketDespawnObjectReceived);
            RegisterClientPacketHandler<PacketNetworkTransformSync>(OnClientPacketTargetedReceived);
            RegisterClientPacketHandler<PacketNetworkBehaviourSync>(OnClientPacketNetworkBehaviourSyncReceived);
            RegisterClientPacketHandler<PacketClientRPC>(OnClientPacketClientRPCReceived);
            RegisterClientPacketHandler<PacketOwnershipChange>(OnClientPacketOwnershipChangeReceived);
            RegisterClientPacketHandler<PacketAuthorityChange>(OnClientPacketAuthorityChangeReceived);

            OnClientStart?.Invoke();
        }
        public void ClientStop()
        {
            if (!IsClientRunning())
                return;

            _clientLocalClient.Stop();
            _clientLocalClient = null;

            _clientHandlers.Clear();

            SharedStop();

            OnClientStop?.Invoke();
        }
        public bool IsClientRunning()
        {
            return _clientLocalClient != null && _clientLocalClient.IsRunning();
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

        /// <summary>
        /// Send packets to the Server over TCP
        /// </summary>
        /// <param name="packets">The packets to send</param>
        public void SendToServerTCP(params Packet[] packets)
        {
            if (packets.Length <= 0)
                return;

            List<byte> packetBuffer = new List<byte>();
            foreach (Packet packet in packets)
            {
                // Prepare Packet
                byte[] buffer = PacketFormatter.Serialise(packet);

                // Packet Size + Packet
                packetBuffer.AddRange(BitConverter.GetBytes((uint)buffer.Length));
                packetBuffer.AddRange(buffer);
            }

            _ = _clientLocalClient.WriteTCP(packetBuffer.ToArray());
        }
        /// <summary>
        /// Send packets to the Server over UDP
        /// </summary>
        /// <param name="packets">The packets to send</param>
        public void SendToServerUDP(params Packet[] packets)
        {
            if (packets.Length <= 0)
                return;

            foreach (Packet packet in packets)
            {
                // Prepare Packet
                byte[] buffer = PacketFormatter.Serialise(packet);

                _ = _clientLocalClient.WriteUDP(buffer);
            }
        }

        // Static Functions
        public static bool IsClient()
        {
            if (_instance == null)
                return false;

            return _instance.IsClientRunning();
        }
        public static uint GetClientID()
        {
            if (_instance == null)
            {
                Debug.LogError("Tried to GetClientID with no instance running");
                return 0;
            }
            if (!_instance.IsClientRunning())
            {
                Debug.LogError("Tried to GetClientID with no client running");
                return 0;
            }
            return _instance._clientID;
        }
        #endregion

        #region Client Handlers
        private void OnClientPacketPongReceived(PacketPong packet)
        {
            Debug.Log("Pong");
        }

        private void OnClientPacketServerInfoReceived(PacketServerInfo packet)
        {
            _clientID = packet.yourClientID;
        }

        private void OnClientPacketSpawnObjectReceived(PacketSpawnObject packet)
        {
            Debug.Log($"Spawn: {packet.prefabID} {packet.ownerHasAuthority} {packet.positionX}");

            // Do not spawn duplicate if this client is host
            if (IsServerRunning() && _spawnedObjects.ContainsKey(packet.objectNetworkID))
            {
                NetworkObject temp = _spawnedObjects[packet.objectNetworkID];
                temp.HostSpawn(packet);
                return;
            }

            // Validate Packet
            if (packet.prefabID >= _spawnableObjects.Count)
                return;
            if (_spawnedObjects.ContainsKey(packet.objectNetworkID))
                return;

            NetworkObject networkObject = UnityEngine.Object.Instantiate(_spawnableObjects[(int)packet.prefabID]);
            networkObject.gameObject.transform.SetPositionAndRotation(packet.GetPosition(), packet.GetRotation());
            networkObject.gameObject.transform.localScale = packet.GetScale();

            _spawnedObjects[packet.objectNetworkID] = networkObject;
            networkObject.Spawn(packet.objectNetworkID, packet.ownerNetworkID);
        }
        private void OnClientPacketDespawnObjectReceived(PacketDespawnObject packet)
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

        private void OnClientPacketOwnershipChangeReceived(PacketOwnershipChange packet)
        {
            // Validate Packet
            if (_spawnedObjects.ContainsKey(packet.networkObjectID))
                return;
            NetworkObject networkObject = _spawnedObjects[packet.networkObjectID];
            uint oldValue = networkObject.GetOwnerID();
            networkObject.ChangeOwnership(packet.clientID);
            ExecuteEvents.Execute<INetworkMessageTarget>(networkObject.gameObject, null, (x, y) => x.OnOwnershipChange(oldValue, packet.clientID));
        }
        private void OnClientPacketAuthorityChangeReceived(PacketAuthorityChange packet)
        {
            // Validate Packet
            if (!_spawnedObjects.ContainsKey(packet.networkObjectID))
                return;
            NetworkObject networkObject = _spawnedObjects[packet.networkObjectID];
            bool oldValue = networkObject.HasAuthority();
            networkObject.ChangeAuthority(packet.hasAuthority);
            ExecuteEvents.Execute<INetworkMessageTarget>(networkObject.gameObject, null, (x, y) => x.OnAuthorityChange(oldValue, packet.hasAuthority));
        }
        private void OnClientPacketNetworkBehaviourSyncReceived(PacketNetworkBehaviourSync packet)
        {
            // Do not duplicate if this client is host
            if (IsServerRunning())
                return;

            // Validate Packet
            if (!_spawnedObjects.ContainsKey(packet.networkObjectID))
                return;
            NetworkObject networkObject = _spawnedObjects[packet.networkObjectID];
            if (!networkObject.HasNetworkBehaviour(packet.networkBehaviourID))
                return;
            NetworkBehaviour networkBehaviour = networkObject.GetNetworkBehaviour(packet.networkBehaviourID);

            if (networkBehaviour.ShouldParseSyncPacket(packet))
                networkBehaviour.ParseSyncPacket(packet);
        }
        private void OnClientPacketClientRPCReceived(PacketClientRPC packet)
        {
            // Validate Packet
            if (!_spawnedObjects.ContainsKey(packet.networkObjectID))
                return;
            NetworkObject networkObject = _spawnedObjects[packet.networkObjectID];
            if (!networkObject.HasNetworkBehaviour(packet.networkBehaviourID))
                return;
            NetworkBehaviour networkBehaviour = networkObject.GetNetworkBehaviour(packet.networkBehaviourID);

            networkBehaviour.OnPacketClientRPCReceive(packet);
        }

        private void OnClientPacketTargetedReceived(PacketTargeted packet)
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

        public void SetSpawnableObjects(List<NetworkObject> spawnableObjects)
        {
            if (IsServerRunning() || IsClientRunning())
                return;
            _spawnableObjects = spawnableObjects;
        }
        public void SetPlayerPrefab(NetworkObject playerPrefab)
        {
            if (IsServerRunning() || IsClientRunning())
                return;
            _serverPlayerPrefab = playerPrefab;
        }

        public void BeginProcessingPackets()
        {
            if (IsClientRunning())
                _clientLocalClient.SceneLoaded();
        }
    }
}
