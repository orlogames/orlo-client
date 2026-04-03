using System.Collections.Generic;
using Google.Protobuf;
using UnityEngine;
using Orlo.Proto;
using EntityId = Orlo.Proto.EntityId;
using ProtoAuth = Orlo.Proto.Auth;
using ProtoWorld = Orlo.Proto.World;
using ProtoCharacter = Orlo.Proto.Character;
using ProtoAdmin = Orlo.Proto.Admin;
using ProtoEconomy = Orlo.Proto.Economy;
using ProtoTMD = Orlo.Proto.TMD;

namespace Orlo.Network
{
    /// <summary>
    /// Helper to build and serialize outgoing Packet protobufs.
    /// </summary>
    public static class PacketBuilder
    {
        private static uint _sequence = 0;

        private static Packet NewPacket()
        {
            return new Packet
            {
                Sequence = ++_sequence,
                Timestamp = new Timestamp
                {
                    Ms = (ulong)(Time.realtimeSinceStartup * 1000)
                }
            };
        }

        public static byte[] LoginRequest(string username, string password, string token = "", uint protocolVersion = 1)
        {
            var pkt = NewPacket();
            pkt.LoginRequest = new ProtoAuth.LoginRequest
            {
                Username = username,
                Password = password,
                Token = token,
                ProtocolVersion = protocolVersion
            };
            return pkt.ToByteArray();
        }

        public static byte[] RegisterRequest(string username, string password, string email)
        {
            var pkt = NewPacket();
            pkt.RegisterRequest = new ProtoAuth.RegisterRequest
            {
                Username = username,
                Password = password,
                Email = email
            };
            return pkt.ToByteArray();
        }

        public static byte[] CharacterSelect(ulong sessionId, string characterName)
        {
            var pkt = NewPacket();
            pkt.CharacterSelect = new ProtoAuth.CharacterSelectRequest
            {
                SessionId = sessionId,
                CharacterName = characterName
            };
            return pkt.ToByteArray();
        }

        public static byte[] PlayerMoveInput(Vector3 position, Quaternion rotation, Vector3 velocity,
            bool jumping, bool sprinting)
        {
            var pkt = NewPacket();
            pkt.PlayerMove = new ProtoWorld.PlayerMoveInput
            {
                Position = new Vec3 { X = position.x, Y = position.y, Z = position.z },
                Rotation = new Quat { X = rotation.x, Y = rotation.y, Z = rotation.z, W = rotation.w },
                Velocity = new Vec3 { X = velocity.x, Y = velocity.y, Z = velocity.z },
                Timestamp = new Timestamp { Ms = (ulong)(Time.realtimeSinceStartup * 1000) },
                Jumping = jumping,
                Sprinting = sprinting
            };
            return pkt.ToByteArray();
        }

        public static byte[] Ping()
        {
            var pkt = NewPacket();
            pkt.Ping = new ProtoAuth.Ping
            {
                ClientTime = new Timestamp { Ms = (ulong)(Time.realtimeSinceStartup * 1000) }
            };
            return pkt.ToByteArray();
        }

        public static byte[] CharacterCreate(ulong sessionId, UI.CharacterCreationData data)
        {
            var pkt = NewPacket();
            pkt.CharacterCreate = new ProtoCharacter.CharacterCreateRequest
            {
                SessionId = sessionId,
                Identity = new ProtoCharacter.CharacterIdentity
                {
                    FirstName = data.FirstName,
                    LastName = data.LastName,
                    StartingSkillId = (uint)data.StartingSkillId,
                    Appearance = new ProtoCharacter.CharacterAppearance
                    {
                        Gender = (ProtoCharacter.Gender)data.Gender,
                        Race = (ProtoCharacter.Race)data.Race,
                        Height = data.Height,
                        Build = data.Build,
                        EyeColor = (ProtoCharacter.EyeColor)data.EyeColor,
                        HairStyle = (ProtoCharacter.HairStyle)data.HairStyle,
                        HairColor = (ProtoCharacter.HairColor)data.HairColor,
                        SkinTone = (ProtoCharacter.SkinTone)data.SkinTone,
                        FaceShape = data.FaceShape,
                        JawWidth = data.JawWidth,
                        NoseSize = data.NoseSize,
                        EarSize = data.EarSize,
                        FacialMarking = (uint)data.FacialMarking
                    }
                }
            };
            return pkt.ToByteArray();
        }

        /// <summary>
        /// Build a CharacterCreate packet from the new deep AppearanceData.
        /// Populates all expanded proto fields (FaceBlendShapes, BodyMorphs, SkinDetail, etc.).
        /// </summary>
        public static byte[] CharacterCreate(ulong sessionId, UI.CharacterCreation.AppearanceData data)
        {
            var pkt = NewPacket();
            var appearance = data.ToProto();

            int startingSkillId = 0;
            if (data.SelectedSkill >= 0)
            {
                // Map skill index to server skill ID (matches SkillRegistry on server)
                int[] skillIds = { 100, 101, 203, 204, 300, 303 };
                if (data.SelectedSkill < skillIds.Length)
                    startingSkillId = skillIds[data.SelectedSkill];
            }

            pkt.CharacterCreate = new ProtoCharacter.CharacterCreateRequest
            {
                SessionId = sessionId,
                Identity = new ProtoCharacter.CharacterIdentity
                {
                    FirstName = data.FirstName ?? "",
                    LastName = data.LastName ?? "",
                    StartingSkillId = (uint)startingSkillId,
                    Appearance = appearance
                }
            };
            return pkt.ToByteArray();
        }

        public static byte[] CharacterListRequest(ulong sessionId)
        {
            var pkt = NewPacket();
            pkt.CharacterListRequest = new ProtoCharacter.CharacterListRequest
            {
                SessionId = sessionId
            };
            return pkt.ToByteArray();
        }

        // ─── Terrain Streaming ──────────────────────────────────────────────

        public static byte[] ChunkRequest(List<Vector2Int> chunks)
        {
            var pkt = NewPacket();
            var req = new ProtoWorld.ChunkRequest();
            foreach (var c in chunks)
            {
                req.Chunks.Add(new ProtoWorld.ChunkCoord { ChunkX = c.x, ChunkZ = c.y });
            }
            pkt.ChunkRequest = req;
            return pkt.ToByteArray();
        }

        public static byte[] ChunkUnload(List<Vector2Int> chunks)
        {
            var pkt = NewPacket();
            var unload = new ProtoWorld.ChunkUnload();
            foreach (var c in chunks)
            {
                unload.Chunks.Add(new ProtoWorld.ChunkCoord { ChunkX = c.x, ChunkZ = c.y });
            }
            pkt.ChunkUnload = unload;
            return pkt.ToByteArray();
        }

        // ─── Admin Commands ──────────────────────────────────────────────────

        public static byte[] AdminSetSpeed(float speed)
        {
            var pkt = NewPacket();
            pkt.AdminCommand = new ProtoAdmin.AdminCommand
            {
                SetSpeed = new ProtoAdmin.SetSpeed { Speed = speed }
            };
            return pkt.ToByteArray();
        }

        public static byte[] AdminSetFly(bool enabled)
        {
            var pkt = NewPacket();
            pkt.AdminCommand = new ProtoAdmin.AdminCommand
            {
                SetFly = new ProtoAdmin.SetFly { Enabled = enabled }
            };
            return pkt.ToByteArray();
        }

        public static byte[] AdminSpawnTool(string toolId, uint quantity)
        {
            var pkt = NewPacket();
            pkt.AdminCommand = new ProtoAdmin.AdminCommand
            {
                SpawnTool = new ProtoAdmin.SpawnTool { ToolId = toolId, Quantity = quantity }
            };
            return pkt.ToByteArray();
        }

        public static byte[] AdminSetToolPower(float power)
        {
            var pkt = NewPacket();
            pkt.AdminCommand = new ProtoAdmin.AdminCommand
            {
                SetToolPower = new ProtoAdmin.SetToolPower { Power = power }
            };
            return pkt.ToByteArray();
        }

        public static byte[] AdminTeleport(float x, float y, float z)
        {
            var pkt = NewPacket();
            pkt.AdminCommand = new ProtoAdmin.AdminCommand
            {
                Teleport = new ProtoAdmin.Teleport
                {
                    Position = new Vec3 { X = x, Y = y, Z = z }
                }
            };
            return pkt.ToByteArray();
        }

        public static byte[] AdminGodMode(bool enabled)
        {
            var pkt = NewPacket();
            pkt.AdminCommand = new ProtoAdmin.AdminCommand
            {
                GodMode = new ProtoAdmin.GodMode { Enabled = enabled }
            };
            return pkt.ToByteArray();
        }

        // ─── NPC / Shop ─────────────────────────────────────────────────────

        public static byte[] NPCInteract(ulong npcEntityId)
        {
            var pkt = NewPacket();
            pkt.NpcInteract = new ProtoEconomy.NPCInteract
            {
                NpcEntityId = new EntityId { Id = npcEntityId }
            };
            return pkt.ToByteArray();
        }

        public static byte[] ShopBuy(ulong npcEntityId, string itemId, uint quantity)
        {
            var pkt = NewPacket();
            pkt.ShopBuy = new ProtoEconomy.ShopBuyRequest
            {
                NpcEntityId = new EntityId { Id = npcEntityId },
                ItemId = itemId,
                Quantity = quantity
            };
            return pkt.ToByteArray();
        }

        public static byte[] ShopSell(ulong npcEntityId, string itemId, uint quantity)
        {
            var pkt = NewPacket();
            pkt.ShopSell = new ProtoEconomy.ShopSellRequest
            {
                NpcEntityId = new EntityId { Id = npcEntityId },
                ItemId = itemId,
                Quantity = quantity
            };
            return pkt.ToByteArray();
        }

        // ─── Martial Arts ────────────────────────────────────────────────────

        public static byte[] MartialMove(ulong targetEntityId, uint moveId)
        {
            var pkt = NewPacket();
            pkt.MartialMove = new ProtoEconomy.MartialMoveRequest
            {
                Target = new EntityId { Id = targetEntityId },
                MoveId = moveId
            };
            return pkt.ToByteArray();
        }

        // ─── TMD (Terrain Manipulation Device) ─────────────────────────────

        /// <summary>
        /// Send a TMD operation request.
        /// Operation: 0=Dig, 1=Fill, 2=Smooth, 3=Scan, 4=Reinforce
        /// </summary>
        public static byte[] TMDOperation(int operation, Vector3 position, float radius, float intensity)
        {
            var pkt = NewPacket();
            pkt.TmdRequest = new ProtoTMD.TMDRequest
            {
                Operation = (ProtoTMD.TMDOperation)operation,
                Position = new Vec3 { X = position.x, Y = position.y, Z = position.z }
            };
            return pkt.ToByteArray();
        }

        /// <summary>
        /// Place a land claim at a position with a given radius.
        /// </summary>
        public static byte[] PlaceLandClaim(Vector3 position, float radius)
        {
            var pkt = NewPacket();
            pkt.PlaceLandClaim = new ProtoTMD.PlaceLandClaim
            {
                Position = new Vec3 { X = position.x, Y = position.y, Z = position.z },
                Radius = radius
            };
            return pkt.ToByteArray();
        }
    }
}
