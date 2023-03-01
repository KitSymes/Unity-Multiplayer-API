using KitSymes.GTRP.Packets;
using UnityEngine;

namespace KitSymes.GTRP.MonoBehaviours
{
    public class NetworkTransform : NetworkBehaviour
    {
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private Vector3 _lastScale;

        private bool _positionChanged;
        private bool _rotationChanged;
        private bool _scaleChanged;

        public override void OnServerStart()
        {
            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
            _lastScale = transform.localScale;

            _positionChanged = false;
            _rotationChanged = false;
            _scaleChanged = false;
        }

        void Update()
        {
            // If the object isn't spawned, skip
            if (!networkObject.IsSpawned())
                return;

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
                // Feed it all information as it filters itself
                networkObject.AddUDPPacket(new PacketNetworkTransformSync(_positionChanged, _rotationChanged, _scaleChanged) { position = _lastPosition, rotation = _lastRotation, localScale = _lastScale});

                // Reset states
                _positionChanged = false;
                _rotationChanged = false;
                _scaleChanged = false;
            }
        }
    }
}
