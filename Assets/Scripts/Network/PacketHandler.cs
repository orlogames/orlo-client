using System;
using UnityEngine;
using Google.Protobuf;
using Orlo.Proto;
using Orlo.World;
using Orlo.Player;
using Orlo.Audio;
using Orlo.UI;

namespace Orlo.Network
{
    /// <summary>
    /// Deserializes incoming Packet protobufs and dispatches to game systems.
    /// </summary>
    public class PacketHandler : MonoBehaviour
    {
        public static PacketHandler Instance { get; private set; }

        public event Action<Auth.LoginResponse> OnLoginResponse;
        public event Action<Auth.RegisterResponse> OnRegisterResponse;
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
                case Packet.PayloadOneofCase.RegisterResponse:
                    HandleRegisterResponse(packet.RegisterResponse);
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

                // Phase 2 — Combat
                case Packet.PayloadOneofCase.DamageEvent:
                case Packet.PayloadOneofCase.EntityDeath:
                case Packet.PayloadOneofCase.HealthUpdate:
                case Packet.PayloadOneofCase.CombatAction:
                case Packet.PayloadOneofCase.LootDrop:
                    HandleCombatPacket(packet);
                    break;

                // Phase 2 — Inventory & Crafting
                case Packet.PayloadOneofCase.InventoryUpdate:
                case Packet.PayloadOneofCase.ItemAdd:
                case Packet.PayloadOneofCase.ItemRemove:
                case Packet.PayloadOneofCase.ItemMoveConfirm:
                case Packet.PayloadOneofCase.EquipmentChanged:
                case Packet.PayloadOneofCase.GatherProgress:
                case Packet.PayloadOneofCase.GatherComplete:
                case Packet.PayloadOneofCase.CraftProgress:
                case Packet.PayloadOneofCase.CraftComplete:
                case Packet.PayloadOneofCase.RecipeDiscovered:
                case Packet.PayloadOneofCase.LootPickup:
                    HandleInventoryPacket(packet);
                    break;

                // Phase 2 — Social
                case Packet.PayloadOneofCase.ChatMessage:
                case Packet.PayloadOneofCase.SystemMessage:
                case Packet.PayloadOneofCase.PartyUpdate:
                case Packet.PayloadOneofCase.PartyInviteNotify:
                case Packet.PayloadOneofCase.FriendsList:
                case Packet.PayloadOneofCase.FriendStatus:
                    HandleSocialPacket(packet);
                    break;

                // Phase 2 — Progression
                case Packet.PayloadOneofCase.XpGain:
                case Packet.PayloadOneofCase.LevelUp:
                case Packet.PayloadOneofCase.StatAllocationResponse:
                case Packet.PayloadOneofCase.ProgressionSnapshot:
                case Packet.PayloadOneofCase.SkillRankUpResponse:
                case Packet.PayloadOneofCase.SkillsSnapshot:
                case Packet.PayloadOneofCase.QuestAvailable:
                case Packet.PayloadOneofCase.QuestProgress:
                case Packet.PayloadOneofCase.QuestComplete:
                case Packet.PayloadOneofCase.QuestTurnInResponse:
                case Packet.PayloadOneofCase.QuestLog:
                    HandleProgressionPacket(packet);
                    break;

                // Phase 3 — Environment
                case Packet.PayloadOneofCase.EnvironmentUpdate:
                    HandleEnvironmentUpdate(packet.EnvironmentUpdate);
                    break;
                case Packet.PayloadOneofCase.AudioZoneEnter:
                    HandleAudioZoneEnter(packet.AudioZoneEnter);
                    break;
                case Packet.PayloadOneofCase.AudioZoneLeave:
                    HandleAudioZoneLeave(packet.AudioZoneLeave);
                    break;
                case Packet.PayloadOneofCase.SoundEvent:
                    HandleSoundEvent(packet.SoundEvent);
                    break;
                case Packet.PayloadOneofCase.Notification:
                    HandleNotification(packet.Notification);
                    break;
                case Packet.PayloadOneofCase.MinimapUpdate:
                    HandleMinimapUpdate(packet.MinimapUpdate);
                    break;

                // Economy / NPC / Shop
                case Packet.PayloadOneofCase.NpcData:
                    HandleNPCData(packet.NpcData);
                    break;
                case Packet.PayloadOneofCase.ShopBuyResponse:
                    HandleShopBuyResponse(packet.ShopBuyResponse);
                    break;
                case Packet.PayloadOneofCase.ShopSellResponse:
                    HandleShopSellResponse(packet.ShopSellResponse);
                    break;
                case Packet.PayloadOneofCase.WalletUpdate:
                    HandleWalletUpdate(packet.WalletUpdate);
                    break;

                // Martial Arts
                case Packet.PayloadOneofCase.MartialMoveResponse:
                    HandleMartialMoveResponse(packet.MartialMoveResponse);
                    break;
                case Packet.PayloadOneofCase.MartialArtsState:
                    HandleMartialArtsState(packet.MartialArtsState);
                    break;

                // Admin
                case Packet.PayloadOneofCase.AdminResponse:
                    HandleAdminResponse(packet.AdminResponse);
                    break;
                case Packet.PayloadOneofCase.AdminState:
                    HandleAdminState(packet.AdminState);
                    break;

                // Character creation
                case Packet.PayloadOneofCase.CharacterListResponse:
                    HandleCharacterList(packet.CharacterListResponse);
                    break;
                case Packet.PayloadOneofCase.CharacterCreateResponse:
                    HandleCharacterCreateResponse(packet.CharacterCreateResponse);
                    break;
                case Packet.PayloadOneofCase.CharacterAppearance:
                    HandleCharacterAppearance(packet.CharacterAppearance);
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
                Debug.Log($"[Auth] Login successful — session {resp.SessionId}, account {resp.AccountId}");
                OnLoginResponse?.Invoke(resp);
            }
            else
            {
                Debug.LogError($"[Auth] Login failed: {resp.Error}");
                OnLoginResponse?.Invoke(resp);
            }
        }

        private void HandleRegisterResponse(Auth.RegisterResponse resp)
        {
            if (resp.Success)
            {
                Debug.Log($"[Auth] Registration successful — account {resp.AccountId}");
            }
            else
            {
                Debug.LogError($"[Auth] Registration failed: {resp.Error}");
            }
            OnRegisterResponse?.Invoke(resp);
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

            NotificationUI.Instance?.ShowDiscovery("New Discovery",
                $"Found: {reveal.ContentId} ({reveal.ContentType})");

            var pos = new Vector3(reveal.Location.Position.X, reveal.Location.Position.Y, reveal.Location.Position.Z);
            FindFirstObjectByType<MinimapUI>()?.AddMarker(pos, "poi", reveal.ContentId, new Color(0.3f, 0.9f, 0.9f));
        }

        // Phase 2 stub handlers — route to UI managers
        private void HandleCombatPacket(Packet packet)
        {
            // Routed to combat UI when implemented
            Debug.Log($"[Combat] Received: {packet.PayloadCase}");
        }

        private void HandleInventoryPacket(Packet packet)
        {
            Debug.Log($"[Inventory] Received: {packet.PayloadCase}");
        }

        private void HandleSocialPacket(Packet packet)
        {
            Debug.Log($"[Social] Received: {packet.PayloadCase}");
        }

        private void HandleProgressionPacket(Packet packet)
        {
            Debug.Log($"[Progression] Received: {packet.PayloadCase}");
        }

        // Phase 3 — Environment handlers
        private void HandleEnvironmentUpdate(Environment.Proto.EnvironmentUpdate env)
        {
            FindFirstObjectByType<SkyboxController>()?.OnEnvironmentUpdate(
                env.TimeOfDay, env.SunR, env.SunG, env.SunB,
                env.AmbientR, env.AmbientG, env.AmbientB,
                env.FogDensity, env.FogR, env.FogG, env.FogB,
                env.WeatherIntensity);

            FindFirstObjectByType<WeatherController>()?.OnEnvironmentUpdate(
                (int)env.Weather, env.WeatherIntensity,
                env.WindDirection, env.WindSpeed);

            FindFirstObjectByType<WaterPlane>()?.OnWindUpdate(env.WindDirection, env.WindSpeed);
        }

        private void HandleAudioZoneEnter(Environment.Proto.AudioZoneEnter zone)
        {
            AudioManager.Instance?.OnAudioZoneEnter(
                zone.ZoneId, zone.MusicTrack, zone.AmbientTrack,
                zone.MusicVolume, zone.AmbientVolume);
        }

        private void HandleAudioZoneLeave(Environment.Proto.AudioZoneLeave zone)
        {
            AudioManager.Instance?.OnAudioZoneLeave(zone.ZoneId);
        }

        private void HandleSoundEvent(Environment.Proto.SoundEvent snd)
        {
            var pos = new Vector3(snd.Position.X, snd.Position.Y, snd.Position.Z);
            AudioManager.Instance?.PlaySoundAt(snd.SoundId, pos, snd.Volume, snd.Radius);
        }

        private void HandleNotification(Environment.Proto.Notification notif)
        {
            NotificationUI.Instance?.Show(notif.Title, notif.Message, (int)notif.Type, notif.Duration);
        }

        private void HandleMinimapUpdate(Environment.Proto.MinimapUpdate map)
        {
            FindFirstObjectByType<MinimapUI>()?.OnMinimapUpdate(
                map.CellX, map.CellZ, (int)map.Resolution, map.ColorData.ToByteArray());
        }

        // ─── Character Creation handlers ────────────────────────────────────

        private void HandleCharacterList(Character.CharacterListResponse list)
        {
            Debug.Log($"[Character] Received character list: {list.Characters.Count} characters");
            var bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null) return;

            if (list.Characters.Count == 0)
            {
                bootstrap.OnCharacterListResponse(0, 0, "", "");
            }
            else
            {
                var first = list.Characters[0];
                bootstrap.OnCharacterListResponse(
                    list.Characters.Count,
                    first.CharacterId.Id,
                    first.FirstName,
                    first.LastName);
            }
        }

        private void HandleCharacterCreateResponse(Character.CharacterCreateResponse resp)
        {
            var bootstrap = FindFirstObjectByType<GameBootstrap>();
            bootstrap?.OnCharacterCreateResponse(
                resp.Success, resp.Error,
                resp.CharacterId?.Id ?? 0);
        }

        private void HandleCharacterAppearance(Character.CharacterAppearanceUpdate appearance)
        {
            Debug.Log($"[Character] Appearance update for entity {appearance.EntityId.Id}: " +
                      $"{appearance.FirstName} {appearance.LastName}");
            // Store appearance data on the networked entity for rendering
        }

        // ─── Economy / NPC handlers ─────────────────────────────────────────

        private void HandleNPCData(Economy.NPCData data)
        {
            Debug.Log($"[NPC] {data.NpcName}: {data.Dialogue} ({data.ShopItems.Count} items)");

            // If vendor, open shop UI
            if (data.Role == 0) // Vendor
            {
                var shop = FindFirstObjectByType<ShopUI>();
                if (shop == null)
                {
                    var go = new GameObject("ShopUI");
                    shop = go.AddComponent<ShopUI>();
                }

                var items = new System.Collections.Generic.List<ShopUI.ShopItemData>();
                foreach (var item in data.ShopItems)
                {
                    items.Add(new ShopUI.ShopItemData
                    {
                        ItemId = item.ItemId,
                        Name = item.ItemName,
                        Description = item.ItemDescription,
                        BuyPrice = item.BuyPrice,
                        SellPrice = item.SellPrice,
                        Stock = item.Stock,
                        Category = (int)item.Category,
                        Rarity = (int)item.Rarity
                    });
                }
                shop.SetItems(items);
                shop.Show(data.NpcEntityId.Id, data.NpcName, data.Dialogue, 0);
            }
        }

        private void HandleShopBuyResponse(Economy.ShopBuyResponse resp)
        {
            var shop = FindFirstObjectByType<ShopUI>();
            if (resp.Success)
            {
                shop?.UpdateBalance(resp.NewBalance);
                shop?.SetStatus("Purchased!");
            }
            else
            {
                shop?.SetStatus($"Failed: {resp.Error}");
            }
        }

        private void HandleShopSellResponse(Economy.ShopSellResponse resp)
        {
            var shop = FindFirstObjectByType<ShopUI>();
            if (resp.Success)
            {
                shop?.UpdateBalance(resp.NewBalance);
                shop?.SetStatus("Sold!");
            }
            else
            {
                shop?.SetStatus($"Failed: {resp.Error}");
            }
        }

        private void HandleWalletUpdate(Economy.WalletUpdate update)
        {
            Debug.Log($"[Economy] Wallet: {update.Credits} creds ({(update.Delta >= 0 ? "+" : "")}{update.Delta} — {update.Reason})");

            var shop = FindFirstObjectByType<ShopUI>();
            shop?.UpdateBalance(update.Credits);

            if (update.Delta != 0)
            {
                string sign = update.Delta > 0 ? "+" : "";
                NotificationUI.Instance?.Show("Credits", $"{sign}{update.Delta} creds ({update.Reason})", 0, 3f);
            }
        }

        // ─── Martial Arts handlers ──────────────────────────────────────────

        private void HandleMartialMoveResponse(Economy.MartialMoveResponse resp)
        {
            var bar = FindFirstObjectByType<CombatBarUI>();
            if (resp.Success)
            {
                string msg = $"{resp.MoveName}: {resp.Damage:F0} dmg";
                if (resp.ComboCount > 1) msg += $" (Combo x{resp.ComboCount})";
                if (!string.IsNullOrEmpty(resp.Effect)) msg += $" [{resp.Effect}]";
                bar?.ShowMoveResult(msg);
            }
            else
            {
                bar?.ShowMoveResult(resp.Error);
            }
        }

        private void HandleMartialArtsState(Economy.MartialArtsState state)
        {
            Debug.Log($"[Martial] Style={state.ActiveStyle} Rank={state.UnarmedRank} Moves={state.AvailableMoves.Count}");

            var bar = FindFirstObjectByType<CombatBarUI>();
            if (bar == null)
            {
                var go = new GameObject("CombatBarUI");
                bar = go.AddComponent<CombatBarUI>();
            }

            var moves = new System.Collections.Generic.List<CombatBarUI.MoveSlot>();
            foreach (var m in state.AvailableMoves)
            {
                moves.Add(new CombatBarUI.MoveSlot
                {
                    MoveId = m.MoveId,
                    Name = m.Name,
                    Description = m.Description,
                    DamageMultiplier = m.DamageMultiplier,
                    StaminaCost = m.StaminaCost,
                    Cooldown = m.Cooldown,
                    CooldownRemaining = m.CooldownRemaining,
                    RequiredRank = m.RequiredRank,
                    IsFinisher = m.IsComboFinisher,
                    Effect = m.Effect
                });
            }
            bar.SetState(state.ActiveStyle, state.UnarmedRank, state.ComboCount, moves);
        }

        // ─── Admin handlers ─────────────────────────────────────────────────

        private void HandleAdminResponse(Admin.AdminResponse resp)
        {
            Debug.Log($"[Admin] {(resp.Success ? "OK" : "FAIL")}: {resp.Message}");

            var panel = FindFirstObjectByType<AdminPanel>();
            if (panel == null) return;

            panel.SetStatus(resp.Message);
            if (resp.State != null)
            {
                panel.SetAdminState(resp.State.IsAdmin, resp.State.RunSpeed,
                    resp.State.FlyEnabled, resp.State.ToolPower, resp.State.GodMode);
            }
        }

        private void HandleAdminState(Admin.AdminState state)
        {
            Debug.Log($"[Admin] State sync — admin={state.IsAdmin} speed={state.RunSpeed} fly={state.FlyEnabled} toolPower={state.ToolPower}");

            // Create AdminPanel if player is admin
            if (state.IsAdmin)
            {
                var panel = FindFirstObjectByType<AdminPanel>();
                if (panel == null)
                {
                    var go = new GameObject("AdminPanel");
                    panel = go.AddComponent<AdminPanel>();
                }
                panel.SetAdminState(state.IsAdmin, state.RunSpeed,
                    state.FlyEnabled, state.ToolPower, state.GodMode);
            }
        }
    }
}
