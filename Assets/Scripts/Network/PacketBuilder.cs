using Google.Protobuf;
using UnityEngine;
using Orlo.Proto;
using ProtoAuth = Orlo.Proto.Auth;
using ProtoWorld = Orlo.Proto.World;
using ProtoCharacter = Orlo.Proto.Character;
using ProtoAdmin = Orlo.Proto.Admin;
using ProtoEconomy = Orlo.Proto.Economy;

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

        public static byte[] CharacterListRequest(ulong sessionId)
        {
            var pkt = NewPacket();
            pkt.CharacterListRequest = new ProtoCharacter.CharacterListRequest
            {
                SessionId = sessionId
            };
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
    }
}
