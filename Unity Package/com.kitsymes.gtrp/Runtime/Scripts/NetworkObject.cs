using KitSymes.GTRP.Packets;
using System.Collections.Generic;
using UnityEngine;

namespace KitSymes.GTRP
{
    public sealed class NetworkObject : MonoBehaviour
    {
        /// <summary>
        /// The prefab ID of this prefab - must be serialised for Unity to save it.
        /// I believe [HideInInspector] interferes with it.
        /// </summary>
        [SerializeField]
        private uint _prefabID;

        private uint _networkID;
        private uint _ownerNetworkID = 0;
        private bool _isOwner = false;
        [SerializeField]
        private bool _hasAuthoriy = false;
        private bool _spawned = false;
        [SerializeField]
        private bool _spawnOnStart = false;
        private bool _isServer = false;

        private readonly List<Packet> _tcpPackets = new List<Packet>();
        private readonly List<Packet> _udpPackets = new List<Packet>();

        private readonly Dictionary<uint, NetworkBehaviour> _networkBehaviours = new Dictionary<uint, NetworkBehaviour>();
        private uint _networkBehavioursCount = 0;

        void Start()
        {
            _isServer = NetworkManager.IsServer();

            if (!_spawned)
            {
                gameObject.SetActive(false);
                if (_spawnOnStart)
                    if (_isServer)
                        NetworkManager.Spawn(gameObject);
                    else
                        Destroy(gameObject);
            }
        }

        /// <summary>
        /// Tick this object and its <see cref="NetworkBehaviour"/>s.
        /// </summary>
        public void Tick()
        {
            foreach (NetworkBehaviour networkBehaviour in _networkBehaviours.Values)
                networkBehaviour.Tick();
        }

        void OnDestroy()
        {
            if (_spawned && NetworkManager.IsServer())
                NetworkManager.Despawn(this);
        }

        /// <summary>
        /// Check to see if this object has been spawned yet.
        /// </summary>
        /// <returns>True if the object has been spawned.</returns>
        public bool IsSpawned() { return _spawned; }
        /// <summary>
        /// Spawn this object for this Client/Server Instance.
        /// </summary>
        /// <param name="networkID">This object's network ID.</param>
        /// <param name="ownerNetworkID">This object's owner's network ID.</param>
        /// <param name="hasAuthority">If this object's owner has authority over it.</param>
        public void Spawn(uint networkID, uint ownerNetworkID, bool hasAuthority)
        {
            if (_spawned)
                return;

            _networkID = networkID;
            ChangeOwnership(ownerNetworkID);
            ChangeAuthority(hasAuthority);

            _spawned = true;
            gameObject.SetActive(true);
        }
        /// <summary>
        /// Get the <see cref="PacketSpawnObject"/> for this object.
        /// </summary>
        /// <returns>This object's <see cref="PacketSpawnObject"/>.</returns>
        internal PacketSpawnObject GetSpawnPacket()
        {
            return new PacketSpawnObject
            {
                prefabID          = _prefabID,
                objectNetworkID   = _networkID,
                ownerNetworkID    = _ownerNetworkID,
                ownerHasAuthority = _hasAuthoriy,
                position          = transform.position,
                rotation          = transform.rotation,
                localScale        = transform.localScale,
            };
        }

        /// <summary>
        /// Add a <see cref="Packet"/> to be sent to the other side through TCP.
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> to be sent.</param>
        public void AddTCPPacket(Packet packet) { _tcpPackets.Add(packet); }
        /// <summary>
        /// Get all <see cref="Packet"/>s that should be sent to the other side using TCP.
        /// </summary>
        /// <returns>The <see cref="List{T}"/> of <see cref="Packet"/>s to be sent.</returns>
        public List<Packet> GetTCPPackets() { return _tcpPackets; }
        /// <summary>
        /// Add a <see cref="Packet"/> to be sent to the other side through UDP.
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> to be sent.</param>
        public void AddUDPPacket(Packet packet) { _udpPackets.Add(packet); }
        /// <summary>
        /// Get all Packets that should be sent to the other side using UDP.
        /// </summary>
        /// <returns>The <see cref="List{T}"/> of <see cref="Packet"/>s to be sent.</returns>
        public List<Packet> GetUDPPackets() { return _udpPackets; }
        /// <summary>
        /// Clear the TCP and UDP <see cref="Packet"/> <see cref="List{T}"/>s.
        /// </summary>
        public void ClearPackets()
        {
            _tcpPackets.Clear();
            _udpPackets.Clear();
        }

        /// <summary>
        /// Get all of the Network Behaviour Sync Packets, with all data regardless of if they have changed or not.
        /// </summary>
        /// <returns>All Network Behaviour Sync Packets with full data.</returns>
        public List<Packet> GetAllFullSyncPackets()
        {
            List<Packet> packets = new List<Packet>();
            foreach (NetworkBehaviour networkBehaviour in _networkBehaviours.Values)
                packets.Add(networkBehaviour.CreateSyncPacket(false));
            return packets;
        }
        /// <summary>
        /// Get all of the Network Behaviour Sync Packets that have changed since last check, that only contain the data that has changed.
        /// </summary>
        /// <returns>Network Behaviour Sync Packets that have changed, with only the information that has.</returns>
        public List<Packet> GetAllDynamicSyncPackets()
        {
            List<Packet> packets = new List<Packet>();

            if (!_isOwner || !_hasAuthoriy)
                return packets;

            foreach (NetworkBehaviour networkBehaviour in _networkBehaviours.Values)
                if (networkBehaviour.HasChanged())
                    packets.Add(networkBehaviour.CreateSyncPacket(true));
            return packets;
        }

        /// <summary>
        /// Get the unique Prefab ID of this object. WARNING: Attempting to get the ID of an item NOT in the spawnable prefab list may return a random valid ID.
        /// </summary>
        /// <returns>The unique ID for this prefab. See WARNING above.</returns>
        public uint GetPrefabID() { return _prefabID; }
#if UNITY_EDITOR
        /// <summary>
        /// Editor only modifying Prefab ID.
        /// Called by <see cref="GTRP.Components.NetworkManagerComponent.OnValidate"/>.
        /// </summary>
        /// <param name="id">The unique ID of this Prefab.</param>
        public void SetPrefabID(uint id) { _prefabID = id; }
#endif
        /// <summary>
        /// Get the unique network ID of this object.
        /// </summary>
        /// <returns>The unique network ID of this object.</returns>
        public uint GetNetworkID() { return _networkID; }

        /// <summary>
        /// Check if this program owns the object.
        /// Cached in <see cref="ChangeOwnership"/> and <see cref="HostSpawn"/>.
        /// </summary>
        /// <returns>True if this program owns the object.</returns>
        public bool IsOwner() { return _isOwner; }
        /// <summary>
        /// Get the network ID of this object's owner.
        /// Cached in <see cref="ChangeOwnership"/>.
        /// </summary>
        /// <returns>The unique network ID of this object's owner.</returns>
        public uint GetOwnerID() { return _ownerNetworkID; }
        /// <summary>
        /// Change the owner of this object.
        /// Updates the owner cache info and if this is the Server, adds the <see cref="PacketOwnershipChange"/> to be sent to Clients.
        /// </summary>
        /// <param name="newOwner">The new owner's unique network ID.</param>
        public void ChangeOwnership(uint newOwner)
        {
            if (newOwner == _ownerNetworkID) return;

            _ownerNetworkID = newOwner;
            _isOwner = (NetworkManager.IsServer() && newOwner == 0) ||
                (NetworkManager.IsClient() && newOwner == NetworkManager.GetClientID());

            if (_spawned && NetworkManager.IsServer())
                _tcpPackets.Add(new PacketOwnershipChange()
                {
                    networkObjectID = _networkID,
                    clientID = newOwner
                });
        }

        /// <summary>
        /// Check to see if the owner of this object has authority over it.
        /// Cached in <see cref="ChangeOwnership"/> and <see cref="HostSpawn"/>.
        /// </summary>
        /// <returns></returns>
        public bool HasAuthority() { return _hasAuthoriy; }
        /// <summary>
        /// Change whether this object's owner has authority over it.
        /// If this is the Server, adds the <see cref="PacketAuthorityChange"/> to be sent to Clients.
        /// </summary>
        /// <param name="hasAuthority"></param>
        public void ChangeAuthority(bool hasAuthority)
        {
            if (hasAuthority == _hasAuthoriy)
                return;

            _hasAuthoriy = hasAuthority;

            if (_spawned && NetworkManager.IsServer())
                _tcpPackets.Add(new PacketAuthorityChange()
                {
                    networkObjectID = _networkID,
                    hasAuthority = hasAuthority
                });
        }

        /// <summary>
        /// Check to see if this object has a Network Behaviour with the given Network Behaviour ID.
        /// </summary>
        /// <param name="networkBehaviourID">The Network Behaviour ID to check for.</param>
        /// <returns>True if this object has a Network Behaviour registered with the given ID.</returns>
        public bool HasNetworkBehaviour(uint networkBehaviourID) { return _networkBehaviours.ContainsKey(networkBehaviourID); }
        /// <summary>
        /// Get the Network Behaviour with the given Network Behaviour ID.
        /// Will throw an Exception if the ID is invalid. Use <see cref="HasNetworkBehaviour"/> first.
        /// </summary>
        /// <param name="networkBehaviourID">The Network Behaviour ID to get.</param>
        /// <returns>The <see cref="NetworkBehaviour"/> with the given ID.</returns>
        public NetworkBehaviour GetNetworkBehaviour(uint networkBehaviourID) { return _networkBehaviours[networkBehaviourID]; }
        /// <summary>
        /// Register a given <see cref="NetworkBehaviour"/> to this object.
        /// Called by <see cref="NetworkBehaviour.Awake"/>.
        /// </summary>
        /// <param name="networkBehaviour">The <see cref="NetworkBehaviour"/> to register.</param>
        /// <returns>The Network Behaviour ID the given <see cref="NetworkBehaviour"/> was registered as.</returns>
        public uint RegisterNetworkBehaviour(NetworkBehaviour networkBehaviour)
        {
            uint key = _networkBehavioursCount;
            _networkBehaviours.Add(key, networkBehaviour);
            _networkBehavioursCount++;
            return key;
        }

        /// <summary>
        /// Called in <see cref="NetworkManager.OnClientPacketSpawnObjectReceived"/> to update the owner and authority cache.
        /// </summary>
        /// <param name="packet"></param>
        internal void HostSpawn(PacketSpawnObject packet)
        {
            _isOwner = (NetworkManager.IsServer() && packet.ownerNetworkID == 0) ||
                (NetworkManager.IsClient() && packet.ownerNetworkID == NetworkManager.GetClientID());
            _hasAuthoriy = packet.ownerHasAuthority;
        }

        /// <summary>
        /// Check if this object is the Server's instance of it.
        /// Cached in <see cref="Start"/>.
        /// </summary>
        /// <returns>True if this object is the Server's instance of it.</returns>
        public bool IsServer() { return _isServer; }
    }
}
