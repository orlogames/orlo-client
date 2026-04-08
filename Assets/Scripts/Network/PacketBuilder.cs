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
using ProtoResource = Orlo.Proto.Resource;
using ProtoInventory = Orlo.Proto.Inventory;
using ProtoSocial = Orlo.Proto.Social;
using ProtoLobby = Orlo.Proto.Lobby;
using ProtoBadges = Orlo.Proto.Badges;

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

        // ─── Chat ───────────────────────────────────────────────────────────

        public static byte[] ChatSend(int channel, string content, string whisperTarget = "")
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = (ProtoSocial.ChatChannel)channel,
                Content = content,
                WhisperTarget = whisperTarget ?? ""
            };
            return pkt.ToByteArray();
        }

        // ─── Admin: Creature Commands ───────────────────────────────────────

        public static byte[] AdminSpawnCreature(string creatureType, float x, float y, float z)
        {
            var pkt = NewPacket();
            pkt.AdminCommand = new ProtoAdmin.AdminCommand
            {
                SpawnCreature = new ProtoAdmin.SpawnCreature
                {
                    CreatureType = creatureType,
                    Position = new Vec3 { X = x, Y = y, Z = z }
                }
            };
            return pkt.ToByteArray();
        }

        public static byte[] AdminListCreatures()
        {
            var pkt = NewPacket();
            pkt.AdminCommand = new ProtoAdmin.AdminCommand
            {
                ListCreatures = new ProtoAdmin.ListCreatures()
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

        // ─── Resource Surveying & Gathering ──────────────────────────────────

        /// <summary>Send a TMD survey/scan request to find nearby resource spawns.</summary>
        public static byte[] SurveyRequest(Vector3 position, float range)
        {
            var pkt = NewPacket();
            pkt.SurveyRequest = new ProtoResource.SurveyRequest
            {
                Position = new Vec3 { X = position.x, Y = position.y, Z = position.z },
                ScanRange = range
            };
            return pkt.ToByteArray();
        }

        /// <summary>Start gathering from a resource node entity.</summary>
        public static byte[] GatherStart(ulong nodeEntityId)
        {
            var pkt = NewPacket();
            pkt.GatherStart = new ProtoInventory.GatherStart
            {
                NodeEntity = new EntityId { Id = nodeEntityId }
            };
            return pkt.ToByteArray();
        }

        /// <summary>Cancel an in-progress gather.</summary>
        public static byte[] GatherCancel(ulong nodeEntityId)
        {
            var pkt = NewPacket();
            pkt.GatherCancel = new ProtoInventory.GatherCancel
            {
                NodeEntity = new EntityId { Id = nodeEntityId }
            };
            return pkt.ToByteArray();
        }

        // ─── Crafting ───────────────────────────────────────────────────────

        /// <summary>
        /// Send a craft request using the existing proto CraftRequest message.
        /// Used as fallback until assembly/experiment proto messages are added.
        /// </summary>
        public static byte[] CraftRequest(uint recipeId, uint station)
        {
            var pkt = NewPacket();
            pkt.CraftRequest = new ProtoInventory.CraftRequest
            {
                RecipeId = recipeId,
                Station = (ProtoInventory.CraftingStation)station
            };
            return pkt.ToByteArray();
        }

        /// <summary>Cancel crafting in progress.</summary>
        public static byte[] CraftCancel()
        {
            var pkt = NewPacket();
            pkt.CraftCancel = new ProtoInventory.CraftCancel();
            return pkt.ToByteArray();
        }

        /// <summary>
        /// Start 2-phase assembly: place resources in slots and begin crafting.
        /// </summary>
        public static byte[] CraftAssemble(string schematicId, List<(uint slotIndex, ulong spawnId, uint qty)> resources)
        {
            var pkt = NewPacket();
            var req = new ProtoInventory.CraftAssembleRequest
            {
                SchematicId = schematicId
            };
            foreach (var (slotIndex, spawnId, qty) in resources)
            {
                req.Slots.Add(new ProtoInventory.ResourceSlotFill
                {
                    SlotIndex = slotIndex,
                    ResourceSpawnId = spawnId,
                    Quantity = qty
                });
            }
            pkt.CraftAssembleRequest = req;
            return pkt.ToByteArray();
        }

        /// <summary>
        /// Spend experiment points on stat categories.
        /// </summary>
        public static byte[] CraftExperiment(List<(string category, uint points)> allocations)
        {
            var pkt = NewPacket();
            var req = new ProtoInventory.CraftExperimentRequest();
            foreach (var (category, points) in allocations)
            {
                req.Allocations.Add(new ProtoInventory.ExperimentAllocation
                {
                    Category = category,
                    Points = points
                });
            }
            pkt.CraftExperimentRequest = req;
            return pkt.ToByteArray();
        }

        /// <summary>
        /// Finalize crafting and name the item.
        /// </summary>
        public static byte[] CraftFinalize(string itemName = "")
        {
            var pkt = NewPacket();
            pkt.CraftFinalizeRequest = new ProtoInventory.CraftFinalizeRequest
            {
                ItemName = itemName
            };
            return pkt.ToByteArray();
        }

        // ─── Inventory Actions ──────────────────────────────────────────────

        /// <summary>Request to equip an item from inventory.</summary>
        public static byte[] EquipItem(uint slotIndex, ProtoInventory.EquipmentSlot targetSlot = ProtoInventory.EquipmentSlot.None)
        {
            var pkt = NewPacket();
            pkt.EquipItemRequest = new ProtoInventory.EquipItemRequest
            {
                SlotIndex = slotIndex,
                TargetSlot = targetSlot
            };
            return pkt.ToByteArray();
        }

        /// <summary>Request to unequip an item to inventory.</summary>
        public static byte[] UnequipItem(ProtoInventory.EquipmentSlot slot)
        {
            var pkt = NewPacket();
            pkt.UnequipItemRequest = new ProtoInventory.UnequipItemRequest
            {
                Slot = slot
            };
            return pkt.ToByteArray();
        }

        /// <summary>Request to drop an item from inventory.</summary>
        public static byte[] DropItem(uint slotIndex, uint quantity)
        {
            var pkt = NewPacket();
            pkt.DropItemRequest = new ProtoInventory.DropItemRequest
            {
                SlotIndex = slotIndex,
                Quantity = quantity
            };
            return pkt.ToByteArray();
        }

        /// <summary>Request to move an item between inventory slots.</summary>
        public static byte[] MoveItem(uint fromSlot, uint toSlot)
        {
            var pkt = NewPacket();
            pkt.ItemMoveRequest = new ProtoInventory.ItemMoveRequest
            {
                FromSlot = fromSlot,
                ToSlot = toSlot
            };
            return pkt.ToByteArray();
        }

        /// <summary>Request to split a stack.</summary>
        public static byte[] SplitStack(uint slotIndex, uint splitQuantity, uint targetSlot)
        {
            var pkt = NewPacket();
            pkt.SplitStackRequest = new ProtoInventory.SplitStackRequest
            {
                SlotIndex = slotIndex,
                SplitQuantity = splitQuantity,
                TargetSlot = targetSlot
            };
            return pkt.ToByteArray();
        }

        /// <summary>Request to pick up a loot entity in the world.</summary>
        public static byte[] LootPickup(ulong lootEntityId)
        {
            var pkt = NewPacket();
            pkt.LootPickupRequest = new ProtoInventory.LootPickupRequest
            {
                LootEntity = new EntityId { Id = lootEntityId }
            };
            return pkt.ToByteArray();
        }

        // ─── Friends ────────────────────────────────────────────────────────

        public static byte[] FriendRequest(string targetName)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/friend_request {targetName}",
                WhisperTarget = targetName
            };
            return pkt.ToByteArray();
        }

        public static byte[] FriendAccept(string targetName)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/friend_accept {targetName}",
                WhisperTarget = targetName
            };
            return pkt.ToByteArray();
        }

        public static byte[] FriendDecline(string targetName)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/friend_decline {targetName}",
                WhisperTarget = targetName
            };
            return pkt.ToByteArray();
        }

        public static byte[] FriendRemove(string targetName)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/friend_remove {targetName}",
                WhisperTarget = targetName
            };
            return pkt.ToByteArray();
        }

        public static byte[] FriendBlock(string targetName)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/block {targetName}",
                WhisperTarget = targetName
            };
            return pkt.ToByteArray();
        }

        public static byte[] FriendUnblock(string targetName)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/unblock {targetName}",
                WhisperTarget = targetName
            };
            return pkt.ToByteArray();
        }

        public static byte[] FriendNote(string targetName, string note)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/friend_note {targetName} {note}",
                WhisperTarget = targetName
            };
            return pkt.ToByteArray();
        }

        public static byte[] SetPlayerStatus(int status)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/status {status}"
            };
            return pkt.ToByteArray();
        }

        // ─── Party ──────────────────────────────────────────────────────────

        public static byte[] PartyInvite(string targetName)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/party_invite {targetName}",
                WhisperTarget = targetName
            };
            return pkt.ToByteArray();
        }

        public static byte[] PartyAccept(string fromName)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/party_accept {fromName}",
                WhisperTarget = fromName
            };
            return pkt.ToByteArray();
        }

        // ─── Guild ──────────────────────────────────────────────────────────

        public static byte[] CreateGuild(string name, string tag)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/guild_create {name} {tag}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] GuildInviteRequest(string targetName)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/guild_invite {targetName}",
                WhisperTarget = targetName
            };
            return pkt.ToByteArray();
        }

        public static byte[] GuildInviteResponse(bool accept)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = accept ? "/guild_accept" : "/guild_decline"
            };
            return pkt.ToByteArray();
        }

        public static byte[] LeaveGuild()
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = "/guild_leave"
            };
            return pkt.ToByteArray();
        }

        public static byte[] GuildKick(string targetName)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/guild_kick {targetName}",
                WhisperTarget = targetName
            };
            return pkt.ToByteArray();
        }

        public static byte[] GuildPromote(string targetName)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/guild_promote {targetName}",
                WhisperTarget = targetName
            };
            return pkt.ToByteArray();
        }

        public static byte[] GuildDemote(string targetName)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/guild_demote {targetName}",
                WhisperTarget = targetName
            };
            return pkt.ToByteArray();
        }

        public static byte[] SetGuildMOTD(string motd)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/guild_motd {motd}"
            };
            return pkt.ToByteArray();
        }

        public struct GuildRankData
        {
            public string Name;
            public bool CanInvite;
            public bool CanKick;
            public bool CanPromote;
            public bool CanEditMotd;
            public bool CanBankDeposit;
            public bool CanBankWithdraw;
            public long BankWithdrawLimit;
        }

        public static byte[] SetGuildRanks(GuildRankData[] ranks)
        {
            // Serialize rank data as command — server parses
            var sb = new System.Text.StringBuilder("/guild_ranks");
            for (int i = 0; i < ranks.Length; i++)
            {
                var r = ranks[i];
                sb.Append($" {i}:{r.Name}:{(r.CanInvite?1:0)}:{(r.CanKick?1:0)}:{(r.CanPromote?1:0)}:{(r.CanEditMotd?1:0)}:{(r.CanBankDeposit?1:0)}:{(r.CanBankWithdraw?1:0)}:{r.BankWithdrawLimit}");
            }
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = sb.ToString()
            };
            return pkt.ToByteArray();
        }

        // ─── Guild Bank ─────────────────────────────────────────────────────

        public static byte[] GuildBankOpen(int tab)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/gbank_open {tab}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] GuildBankDeposit(uint slotIndex, uint quantity)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/gbank_deposit {slotIndex} {quantity}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] GuildBankWithdraw(int bankTab, int slotIndex)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/gbank_withdraw {bankTab} {slotIndex}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] GuildBankLogRequest()
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = "/gbank_log"
            };
            return pkt.ToByteArray();
        }

        // ─── Mail ───────────────────────────────────────────────────────────

        public static byte[] SendMail(string to, string subject, string body, long credits = 0, long codPrice = 0)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/mail_send {to}|{subject}|{body}|{credits}|{codPrice}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] MailListRequest()
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = "/mail_list"
            };
            return pkt.ToByteArray();
        }

        public static byte[] MailReadRequest(ulong mailId)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/mail_read {mailId}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] MailCollect(ulong mailId)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/mail_collect {mailId}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] MailDelete(ulong mailId)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/mail_delete {mailId}"
            };
            return pkt.ToByteArray();
        }

        // ─── Emotes & Social ────────────────────────────────────────────────

        public static byte[] EmoteRequestExtended(string emoteId, ulong targetEntityId)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/emote {emoteId} {targetEntityId}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] WhoRequest(string filter = "")
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/who {filter}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] RollRequest(int min = 1, int max = 100)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/roll {min} {max}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] BlockPlayer(string targetName)
        {
            return FriendBlock(targetName);
        }

        public static byte[] ReportPlayer(string targetName, string reason)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/report {targetName} {reason}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] MutePlayer(string targetName)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/mute {targetName}",
                WhisperTarget = targetName
            };
            return pkt.ToByteArray();
        }

        public static byte[] CommendPlayer(string targetName)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/commend {targetName}",
                WhisperTarget = targetName
            };
            return pkt.ToByteArray();
        }

        // ─── LFG ───────────────────────────────────────────────────────────

        public static byte[] PostLFG(string activity, string description, int maxSize, int minLevel, int maxLevel)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/lfg_post {activity}|{description}|{maxSize}|{minLevel}|{maxLevel}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] RemoveLFG(ulong listingId)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/lfg_remove {listingId}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] ApplyLFG(ulong listingId)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/lfg_apply {listingId}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] LFGBoardRequest()
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = "/lfg_list"
            };
            return pkt.ToByteArray();
        }

        // ─── Circles ────────────────────────────────────────────────────────

        public static byte[] CreateCircle(string name)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/circle_create {name}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] CircleInvite(string circleName, string targetName)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/circle_invite {circleName} {targetName}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] CircleInviteResponse(string circleName, bool accept)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = accept ? $"/circle_accept {circleName}" : $"/circle_decline {circleName}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] CircleLeave(string circleName)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/circle_leave {circleName}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] CircleKick(string circleName, string targetName)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/circle_kick {circleName} {targetName}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] CircleChat(string circleName, string message)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/circle_chat {circleName} {message}"
            };
            return pkt.ToByteArray();
        }

        // ─── Bulletin Board ─────────────────────────────────────────────────

        public static byte[] PostBulletin(string category, string title, string body)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/bulletin_post {category}|{title}|{body}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] RemoveBulletin(ulong postId)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/bulletin_remove {postId}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] BulletinBoardRequest(string category)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/bulletin_list {category}"
            };
            return pkt.ToByteArray();
        }

        // ─── Strain ─────────────────────────────────────────────────────────

        public static byte[] StrainCureRequest()
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = "/strain_cure"
            };
            return pkt.ToByteArray();
        }

        // ─── Player Profile ─────────────────────────────────────────────────

        public static byte[] PlayerProfileRequest(string targetName)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/profile {targetName}",
                WhisperTarget = targetName
            };
            return pkt.ToByteArray();
        }

        public static byte[] SetPlayerProfile(string bio, int titleIndex)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/profile_set {titleIndex}|{bio}"
            };
            return pkt.ToByteArray();
        }

        // ─── Settings Sync ──────────────────────────────────────────────────

        public static byte[] SettingsSyncRequest()
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = "/settings_sync"
            };
            return pkt.ToByteArray();
        }

        public static byte[] ServerStatusRequest()
        {
            var pkt = NewPacket();
            pkt.ServerStatusRequest = new ProtoLobby.ServerStatusRequest();
            return pkt.ToByteArray();
        }

        // ─── Character Management ───────────────────────────────────────────

        public static byte[] CharacterDeleteExtended(ulong characterId)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/char_delete {characterId}"
            };
            return pkt.ToByteArray();
        }

        public static byte[] CancelCharacterDelete(ulong characterId)
        {
            var pkt = NewPacket();
            pkt.ChatSend = new ProtoSocial.ChatSend
            {
                Channel = ProtoSocial.ChatChannel.System,
                Content = $"/char_delete_cancel {characterId}"
            };
            return pkt.ToByteArray();
        }

        // ─── Quest Actions ──────────────────────────────────────────────────

        /// <summary>Accept a quest from an NPC.</summary>
        public static byte[] QuestAccept(string questId)
        {
            var pkt = NewPacket();
            pkt.QuestAccept = new Orlo.Proto.Progression.QuestAccept
            {
                QuestId = questId
            };
            return pkt.ToByteArray();
        }

        /// <summary>Turn in a completed quest to an NPC.</summary>
        public static byte[] QuestTurnIn(string questId)
        {
            var pkt = NewPacket();
            pkt.QuestTurnIn = new Orlo.Proto.Progression.QuestTurnIn
            {
                QuestId = questId
            };
            return pkt.ToByteArray();
        }

        /// <summary>Abandon an active quest.</summary>
        public static byte[] QuestAbandon(string questId)
        {
            // TODO: QuestAbandon proto message not yet defined — use QuestTurnIn as placeholder
            // Server should handle abandon via a separate message type when added
            Debug.LogWarning($"[PacketBuilder] QuestAbandon not yet in proto — sending as log only for quest {questId}");
            var pkt = NewPacket();
            // Placeholder: send a system-level abandon via chat or admin channel
            // Real implementation needs a QuestAbandon message in progression.proto
            pkt.QuestTurnIn = new Orlo.Proto.Progression.QuestTurnIn
            {
                QuestId = questId
            };
            return pkt.ToByteArray();
        }

        /// <summary>Request the full badge list (earned + unearned).</summary>
        public static byte[] BadgeListRequest()
        {
            var pkt = NewPacket();
            pkt.BadgeListRequest = new ProtoBadges.BadgeListRequest();
            return pkt.ToByteArray();
        }

        /// <summary>Set up to 3 showcase badge IDs.</summary>
        public static byte[] SetShowcase(uint[] badgeIds)
        {
            var pkt = NewPacket();
            var msg = new ProtoBadges.SetShowcase();
            foreach (var id in badgeIds)
                msg.BadgeIds.Add(id);
            pkt.SetShowcase = msg;
            return pkt.ToByteArray();
        }
    }
}

