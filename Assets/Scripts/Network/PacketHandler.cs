using System;
using UnityEngine;
using Google.Protobuf;
using Orlo.Proto;
using Orlo.World;
using Orlo.Player;

namespace Orlo.Network
{
    /// <summary>
    /// Deserializes incoming Packet protobufs and dispatches to game systems.
    /// </summary>
    public class PacketHandler : MonoBehaviour
    {
        public static PacketHandler Instance { get; private set; }

        public event Action<Auth.LoginResponse> OnLoginResponse;
        public event Action<Auth.CharacterSpawnResponse> OnCharacterSpawn;
        public event Action<Auth.Pong> OnPong;

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

        private void HandleRawPacket(byte[] data)
        {
            Packet packet;
            try
            {
                packet = Packet.Parser.ParseFrom(data);
            }
            catch (InvalidProtocolBufferException ex)
            {
                Debug.LogError($"[PacketHandler] Failed to parse packet: {ex.Message}");
                return;
            }

            switch (packet.PayloadCase)
            {
                case Packet.PayloadOneofCase.LoginResponse:
                    HandleLoginResponse(packet.LoginResponse);
                    break;
                case Packet.PayloadOneofCase.CharacterSpawn:
                    HandleCharacterSpawn(packet.CharacterSpawn);
                    break;
                case Packet.PayloadOneofCase.Pong:
                    OnPong?.Invoke(packet.Pong);
                    break;
                case Packet.PayloadOneofCase.EntitySpawn:
                    HandleEntitySpawn(packet.EntitySpawn);
                    break;
                case Packet.PayloadOneofCase.EntityDespawn:
                    HandleEntityDespawn(packet.EntityDespawn);
                    break;
                case Packet.PayloadOneofCase.EntityMove:
                    HandleEntityMove(packet.EntityMove);
                    break;
                case Packet.PayloadOneofCase.TerrainChunk:
                    HandleTerrainChunk(packet.TerrainChunk);
                    break;
                case Packet.PayloadOneofCase.ContentReveal:
                    HandleContentReveal(packet.ContentReveal);
                    break;
                default:
                    Debug.LogWarning($"[PacketHandler] Unhandled payload type: {packet.PayloadCase}");
                    break;
            }
        }

        private void HandleLoginResponse(Auth.LoginResponse resp)
        {
            if (resp.Success)
            {
                Debug.Log($"[Auth] Login successful — session {resp.SessionId}");
                OnLoginResponse?.Invoke(resp);
            }
            else
            {
                Debug.LogError($"[Auth] Login failed: {resp.Error}");
            }
        }

        private void HandleCharacterSpawn(Auth.CharacterSpawnResponse spawn)
        {
            Debug.Log($"[Auth] Character spawned — entity {spawn.EntityId.Id} at " +
                      $"({spawn.Transform.Position.X}, {spawn.Transform.Position.Y}, {spawn.Transform.Position.Z})");
            OnCharacterSpawn?.Invoke(spawn);
        }

        private void HandleEntitySpawn(World.Proto.EntitySpawn spawn)
        {
            var pos = new Vector3(spawn.Transform.Position.X, spawn.Transform.Position.Y, spawn.Transform.Position.Z);
            var rot = new Quaternion(spawn.Transform.Rotation.X, spawn.Transform.Rotation.Y,
                                     spawn.Transform.Rotation.Z, spawn.Transform.Rotation.W);
            EntityManager.Instance.SpawnEntity(spawn.EntityId.Id, spawn.EntityType, spawn.AssetId, pos, rot);
        }

        private void HandleEntityDespawn(World.Proto.EntityDespawn despawn)
        {
            EntityManager.Instance.DespawnEntity(despawn.EntityId.Id);
        }

        private void HandleEntityMove(World.Proto.EntityMove move)
        {
            var pos = new Vector3(move.Position.X, move.Position.Y, move.Position.Z);
            var rot = new Quaternion(move.Rotation.X, move.Rotation.Y, move.Rotation.Z, move.Rotation.W);
            var vel = new Vector3(move.Velocity.X, move.Velocity.Y, move.Velocity.Z);
            EntityManager.Instance.MoveEntity(move.EntityId.Id, pos, rot, vel);
        }

        private void HandleTerrainChunk(World.Proto.TerrainChunk chunk)
        {
            var coord = new Vector2Int(chunk.ChunkX, chunk.ChunkZ);
            FindObjectOfType<TerrainManager>()?.ApplyTerrainChunk(
                coord, (int)chunk.Resolution, chunk.Heightmap.ToByteArray(), chunk.Seed);
        }

        private void HandleContentReveal(World.Proto.ContentReveal reveal)
        {
            Debug.Log($"[World] Content revealed: '{reveal.ContentId}' ({reveal.ContentType}) at " +
                      $"({reveal.Location.Position.X}, {reveal.Location.Position.Y}, {reveal.Location.Position.Z})");
            // TODO: Trigger UI notification, map marker, quest log entry
        }
    }
}
