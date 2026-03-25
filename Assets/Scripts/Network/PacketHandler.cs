using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orlo.Network
{
    /// <summary>
    /// Routes incoming packets to registered handlers by type.
    /// Sits between NetworkManager (raw bytes) and game systems (typed messages).
    /// </summary>
    public class PacketHandler : MonoBehaviour
    {
        public static PacketHandler Instance { get; private set; }

        // Packet type IDs matching the protobuf oneof field numbers
        public enum PacketType : uint
        {
            LoginResponse = 11,
            CharacterSpawn = 13,
            Pong = 15,
            EntitySpawn = 20,
            EntityDespawn = 21,
            EntityMove = 22,
            ZoneTransition = 24,
            TerrainChunk = 25,
            ContentReveal = 26,
        }

        private readonly Dictionary<PacketType, Action<byte[]>> _handlers = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            NetworkManager.Instance.OnPacketReceived += HandleRawPacket;
        }

        private void OnDisable()
        {
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.OnPacketReceived -= HandleRawPacket;
        }

        public void Register(PacketType type, Action<byte[]> handler)
        {
            _handlers[type] = handler;
        }

        private void HandleRawPacket(byte[] data)
        {
            // TODO: Deserialize Packet protobuf, extract oneof case,
            // dispatch to registered handler. For now, log.
            Debug.Log($"[PacketHandler] Received {data.Length} bytes");
        }
    }
}
