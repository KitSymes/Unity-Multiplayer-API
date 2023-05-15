using System;
using System.Collections.Generic;

namespace KitSymes.GTRP.Internal
{
    public class PacketFormatter
    {
        public static readonly Dictionary<uint, Type> idToPacket = new Dictionary<uint, Type>();
        public static readonly Dictionary<Type, uint> packetToID = new Dictionary<Type, uint>();
        private static uint _packetCount = 0;

        public static void RegisterPacket(Type type)
        {
            if (packetToID.ContainsKey(type))
                return;

            packetToID.Add(type, _packetCount);
            idToPacket.Add(_packetCount, type);
            _packetCount++;
        }

        public static byte[] Serialise(Packet packet)
        {
            List<byte> bytes = packet.Serialise();
            bytes.InsertRange(0, BitConverter.GetBytes(packetToID[packet.GetType()]));
            return bytes.ToArray();
        }
        public static Packet Deserialise(byte[] bytes, int offset = 0)
        {
            if (bytes.Length < sizeof(uint))
                throw new ArgumentException("Invalid bytes");

            uint id = BitConverter.ToUInt32(bytes, offset);
            if (!idToPacket.ContainsKey(id))
                throw new ArgumentException($"Packet {id} not found");

            Packet packet = (Packet)Activator.CreateInstance(idToPacket[id]);
            packet.Deserialise(bytes, offset + sizeof(uint));
            return packet;
        }
    }
}
