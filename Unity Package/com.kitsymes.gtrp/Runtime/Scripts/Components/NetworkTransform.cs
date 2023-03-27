using KitSymes.GTRP.Packets;
using System;
using UnityEngine;

namespace KitSymes.GTRP.Components
{
    public class NetworkTransform : NetworkBehaviour
    {
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private Vector3 _lastScale;

        private bool _positionChanged;
        private bool _rotationChanged;
        private bool _scaleChanged;

        private DateTime _lastSyncTimestamp;

        public override void OnServerStart()
        {
            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
            _lastScale = transform.localScale;

            _positionChanged = false;
            _rotationChanged = false;
            _scaleChanged = false;

            _lastSyncTimestamp = DateTime.UtcNow;
        }

        public override void OnPacketReceive(Packet packet)
        {
            if (packet is not PacketNetworkTransformSync)
                return;

            PacketNetworkTransformSync sync = (PacketNetworkTransformSync)packet;

            //Debug.Log("Processing" + sync.timestamp + " at " + DateTime.Now + " last " + _lastSyncTimestamp);

            // Timestamp is the same as or before _lastSyncTimestamp
            if (sync.timestamp.CompareTo(_lastSyncTimestamp) <= 0)
                return;

            if (sync.containsPosition)
                transform.position = sync.position;
            if (sync.containsRotation)
                transform.rotation = sync.rotation;
            if (sync.containsScale)
                transform.localScale = sync.localScale;
            _lastSyncTimestamp = sync.timestamp;
        }

        public override void Tick()
        {
            // If the object isn't spawned skip
            if (!networkObject.IsSpawned())
                return;

            bool serverControlled = !networkObject.HasAuthority();
            bool isServer = networkObject.IsServer();
            bool isOwned = networkObject.IsOwner();

            if (!(isServer && serverControlled) && !(isOwned && !serverControlled))
                return;
            Debug.Log($"Ticked {gameObject}");

            /*
            // If we are the server, the object has authority _and_ we don't own it, skip
            if (networkObject.IsServer() && networkObject.HasAuthority() && !networkObject.IsOwner())
                return;
            else if (!networkObject.IsOwner)
            // Otherwise, if we own it and don't have authority, skip
            else if (networkObject.IsOwner() && !networkObject.HasAuthority())
                return;
            */

            // Check to see if the position, rotation and scale have changed since last frame
            if (transform.position != _lastPosition)
            {
                _positionChanged = true;
                _lastPosition = transform.position;
            }
            if (transform.rotation != _lastRotation)
            {
                _rotationChanged = true;
                _lastRotation = transform.rotation;
            }
            if (transform.localScale != _lastScale)
            {
                _scaleChanged = true;
                _lastScale = transform.localScale;
            }

            // If something has changed, we need to update
            if (_positionChanged || _rotationChanged || _scaleChanged)
            {
                // Debug.Log($"{gameObject} moved to {transform.position}");
                // Feed it all information as it filters itself
                networkObject.AddUDPPacket(new PacketNetworkTransformSync()
                {
                    target = networkObject.GetNetworkID(),
                    containsPosition = _positionChanged,
                    containsRotation = _rotationChanged,
                    containsScale = _scaleChanged,
                    position = _lastPosition,
                    rotation = _lastRotation,
                    localScale = _lastScale
                });

                // Reset states
                _positionChanged = false;
                _rotationChanged = false;
                _scaleChanged = false;
            }
        }
    }
}
