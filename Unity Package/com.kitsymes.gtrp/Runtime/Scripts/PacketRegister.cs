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
            // Optimisation based off of https://forum.unity.com/threads/gather-only-user-defined-assemblies.1218786/
MD5 md5 = MD5.Create();

            // Need to filter out the assemblies list to only user created ones (e.g. The Assembly C# and Packages)
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly =>
            !assembly.GetName().Name.StartsWith("Mono.") &&
            !assembly.GetName().Name.StartsWith("System.") &&
            !assembly.GetName().Name.StartsWith("Unity.") &&
            !assembly.GetName().Name.StartsWith("UnityEditor.") &&
            !assembly.GetName().Name.StartsWith("UnityEngine.") &&
            !assembly.GetName().Name.Equals("Bee.BeeDriver") &&
            !assembly.GetName().Name.Equals("ExCSS.Unity") &&
            !assembly.GetName().Name.Equals("log4net") &&
            !assembly.GetName().Name.Equals("Mono.Security") &&
            !assembly.GetName().Name.Equals("mscorlib") &&
            !assembly.GetName().Name.Equals("netstandard") &&
            !assembly.GetName().Name.Equals("Newtonsoft.Json") &&
            !assembly.GetName().Name.Equals("nunit.framework") &&
            !assembly.GetName().Name.Equals("PlayerBuildProgramLibrary.Data") &&
            !assembly.GetName().Name.Equals("ReportGeneratorMerged") &&
            !assembly.GetName().Name.Equals("System") &&
            !assembly.GetName().Name.Equals("UnityEditor") &&
            !assembly.GetName().Name.Equals("UnityEngine") &&
            !assembly.GetName().Name.Equals("unityplastic") &&
            !assembly.GetName().Name.Equals("Unrelated") &&
            !assembly.GetName().Name.Equals("SyntaxTree.VisualStudio.Unity.Bridge") &&
            !assembly.GetName().Name.Equals("SyntaxTree.VisualStudio.Unity.Messaging")
            ).ToArray();

            foreach (Assembly assembly in assemblies)
                foreach (Type packetType in assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Packet))))
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
