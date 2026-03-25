using Google.Protobuf;
using Orlo.Proto;
using UnityEngine;

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
            pkt.LoginRequest = new Auth.LoginRequest
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
            pkt.RegisterRequest = new Auth.RegisterRequest
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
            pkt.CharacterSelect = new Auth.CharacterSelectRequest
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
            pkt.PlayerMove = new World.Proto.PlayerMoveInput
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
            pkt.Ping = new Auth.Ping
            {
                ClientTime = new Timestamp { Ms = (ulong)(Time.realtimeSinceStartup * 1000) }
            };
            return pkt.ToByteArray();
        }

        public static byte[] CharacterCreate(ulong sessionId, UI.CharacterCreationData data)
        {
            var pkt = NewPacket();
            pkt.CharacterCreate = new Character.CharacterCreateRequest
            {
                SessionId = sessionId,
                Identity = new Character.CharacterIdentity
                {
                    FirstName = data.FirstName,
                    LastName = data.LastName,
                    StartingSkillId = (uint)data.StartingSkillId,
                    Appearance = new Character.CharacterAppearance
                    {
                        Gender = (Character.Gender)data.Gender,
                        Race = (Character.Race)data.Race,
                        Height = data.Height,
                        Build = data.Build,
                        EyeColor = (Character.EyeColor)data.EyeColor,
                        HairStyle = (Character.HairStyle)data.HairStyle,
                        HairColor = (Character.HairColor)data.HairColor,
                        SkinTone = (Character.SkinTone)data.SkinTone,
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
            pkt.CharacterListRequest = new Character.CharacterListRequest
            {
                SessionId = sessionId
            };
            return pkt.ToByteArray();
        }
    }
}
