using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System;
using System.Reflection;
using System.Linq;
using UnityEngine;

namespace KitSymes.GTRP
{
    public sealed class PacketRegister
    {
        private static PacketRegister _instance = new PacketRegister();
        public static PacketRegister Instance { get { if (_instance == null) _instance = new PacketRegister(); return _instance; } }

        public Dictionary<Type, uint> messageTypeToId = new Dictionary<Type, uint>();
        public Dictionary<uint, Type> idToMessageType = new Dictionary<uint, Type>();

        private PacketRegister()
        {
            // Based off of https://stackoverflow.com/questions/51020619/unique-id-for-each-class
            IEnumerable<Type> packetTypes = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsSubclassOf(typeof(Packet)));
            MD5 md5 = MD5.Create();
            foreach (Type packetType in packetTypes)
            {
                byte[] md5Bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(packetType.AssemblyQualifiedName));
                uint id = BitConverter.ToUInt32(md5Bytes);

                messageTypeToId[packetType] = id;
                idToMessageType[id] = packetType;

                Debug.Log(packetType + ": " + id);
            }
        }
    }
}
