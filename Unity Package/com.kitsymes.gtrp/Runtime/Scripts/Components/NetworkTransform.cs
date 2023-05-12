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

        public bool predictMovement = true;
        public bool predictRotation = true;
        [Range(0f, 1f)]
        public float predictSquareThreshold = 0.25f;
        public float predictAngleThreshold = 7.0f;

        private Vector3 _predictedPositionStart;
        private Vector3 _predictedPositionTarget;
        private Quaternion _predictedRotationStart;
        private Quaternion _predictedRotationTarget;
        private float _predictedLerp = 1.0f;
        private float _tickRate = 20.0f;

        void Start()
        {
            _predictedLerp = 1.0f;

            _predictedPositionStart = transform.position;
            _predictedPositionTarget = transform.position;

            _predictedRotationStart = transform.rotation;
            _predictedRotationTarget = transform.rotation;

            _tickRate = NetworkManager.GetInstance().GetTickRate();
        }

        void Update()
        {
            if ((networkObject.IsOwner() && networkObject.HasAuthority()) || _predictedLerp >= 1.0f)
                return;

            _predictedLerp += Time.deltaTime * _tickRate;

            if (predictMovement && transform.position != _predictedPositionTarget)
                transform.position = Vector3.Lerp(_predictedPositionStart, _predictedPositionTarget, _predictedLerp);

            if (predictRotation && transform.rotation != _predictedRotationTarget)
                transform.rotation = Quaternion.Lerp(_predictedRotationStart, _predictedRotationTarget, _predictedLerp);
        }

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
            if (packet is not PacketNetworkTransformSync || (networkObject.IsOwner() && networkObject.HasAuthority()))
                return;

            PacketNetworkTransformSync sync = (PacketNetworkTransformSync)packet;

            //Debug.Log("Processing" + sync.timestamp + " at " + DateTime.Now + " last " + _lastSyncTimestamp);

            // Timestamp is the same as or before _lastSyncTimestamp
            if (sync.timestamp.CompareTo(_lastSyncTimestamp) <= 0)
                return;

            _predictedLerp = 0.0f;

            if (sync.containsPosition)
            {
                if (predictMovement)
                {
                    Vector3 dir = sync.position - _predictedPositionStart;
                    _predictedPositionStart = sync.position;

                    // Check if the angle between the last synced Position and new Position is small enough that we can predict it with
                    // user determined acceptible visual issues
                    if (dir.sqrMagnitude <= predictSquareThreshold)
                        _predictedPositionTarget = sync.position + dir;
                    // Otherwise, we don't want to predict so set the Target to our current Position
                    else
                        _predictedPositionTarget = sync.position;
                }
                transform.position = sync.position;
            }

            if (sync.containsRotation)
            {
                if (predictRotation)
                {
                    // Check if the angle between the last synced Rotation and new Rotation is small enough that we can predict it with
                    // user determined acceptible visual issues
                    if (Quaternion.Angle(sync.rotation, _predictedRotationStart) <= predictAngleThreshold)
                    {
                        Quaternion dir = sync.rotation * Quaternion.Inverse(_predictedRotationStart);
                        _predictedRotationTarget = dir * sync.rotation;
                    }
                    // Otherwise, we don't want to predict so set the Target to our current Rotation
                    else
                        _predictedRotationTarget = sync.rotation;

                    _predictedRotationStart = sync.rotation;
                }
                transform.rotation = sync.rotation;
            }

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

            if (!isServer && !(isOwned && !serverControlled))
                return;
            //Debug.Log($"Ticked {gameObject}");

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
                //Debug.Log($"{gameObject} moved to {transform.position}");
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
