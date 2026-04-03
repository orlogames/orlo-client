using System;
using System.Collections.Generic;
using UnityEngine;
using Google.Protobuf;
using Orlo.Player;
using Orlo.Audio;
using Orlo.UI;
using Orlo.World;
using Orlo.Proto;
using ProtoAuth = Orlo.Proto.Auth;
using Color = UnityEngine.Color;
using ProtoWorld = Orlo.Proto.World;
using ProtoCharacter = Orlo.Proto.Character;
using ProtoAdmin = Orlo.Proto.Admin;
using ProtoEconomy = Orlo.Proto.Economy;
using ProtoEnv = Orlo.Proto.Environment;
using ProtoCombat = Orlo.Proto.Combat;
using ProtoInventory = Orlo.Proto.Inventory;
using ProtoResource = Orlo.Proto.Resource;
using ProtoTMD = Orlo.Proto.TMD;

namespace Orlo.Network
{
    /// <summary>
    /// Deserializes incoming Packet protobufs and dispatches to game systems.
    /// </summary>
    public class PacketHandler : MonoBehaviour
    {
        private static PacketHandler _instance;
        public static PacketHandler Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<PacketHandler>();
                    if (_instance == null)
                    {
                        var go = new GameObject("PacketHandler");
                        _instance = go.AddComponent<PacketHandler>();
                    }
                }
                return _instance;
            }
        }

        public event Action<ProtoAuth.LoginResponse> OnLoginResponse;
        public event Action<ProtoAuth.RegisterResponse> OnRegisterResponse;
        public event Action<ProtoAuth.CharacterSpawnResponse> OnCharacterSpawn;
        public event Action<ProtoAuth.Pong> OnPong;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
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

                // Phase 2 — Gathering (resource nodes)
                case Packet.PayloadOneofCase.GatherProgress:
                    HandleGatherProgress(packet.GatherProgress);
                    break;
                case Packet.PayloadOneofCase.GatherComplete:
                    HandleGatherComplete(packet.GatherComplete);
                    break;

                // Phase 2 — Inventory & Crafting
                case Packet.PayloadOneofCase.InventoryUpdate:
                case Packet.PayloadOneofCase.ItemAdd:
                case Packet.PayloadOneofCase.ItemRemove:
                case Packet.PayloadOneofCase.ItemMoveConfirm:
                case Packet.PayloadOneofCase.EquipmentChanged:
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

                // TMD (Terrain Manipulation Device)
                case Packet.PayloadOneofCase.TmdResult:
                    HandleTMDResult(packet.TmdResult);
                    break;
                case Packet.PayloadOneofCase.TerrainModification:
                    HandleTerrainModification(packet.TerrainModification);
                    break;
                case Packet.PayloadOneofCase.TmdStatus:
                    HandleTMDStatus(packet.TmdStatus);
                    break;
                case Packet.PayloadOneofCase.LandClaimInfo:
                    HandleLandClaimInfo(packet.LandClaimInfo);
                    break;

                // Resource Surveying & Gathering
                case Packet.PayloadOneofCase.SurveyResult:
                    HandleSurveyResult(packet.SurveyResult);
                    break;
                case Packet.PayloadOneofCase.ResourceDetail:
                    HandleResourceDetail(packet.ResourceDetail);
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

        private void HandleLoginResponse(ProtoAuth.LoginResponse resp)
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

        private void HandleRegisterResponse(ProtoAuth.RegisterResponse resp)
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

        private void HandleCharacterSpawn(ProtoAuth.CharacterSpawnResponse spawn)
        {
            Debug.Log($"[Auth] Character spawned — entity {spawn.EntityId.Id} at " +
                      $"({spawn.Transform.Position.X}, {spawn.Transform.Position.Y}, {spawn.Transform.Position.Z})");
            OnCharacterSpawn?.Invoke(spawn);
        }

        private void HandleEntitySpawn(ProtoWorld.EntitySpawn spawn)
        {
            var pos = new Vector3(spawn.Transform.Position.X, spawn.Transform.Position.Y, spawn.Transform.Position.Z);
            var rot = new Quaternion(spawn.Transform.Rotation.X, spawn.Transform.Rotation.Y,
                                     spawn.Transform.Rotation.Z, spawn.Transform.Rotation.W);
            EntityManager.Instance.SpawnEntity(spawn.EntityId.Id, spawn.EntityType, spawn.AssetId, pos, rot);
        }

        private void HandleEntityDespawn(ProtoWorld.EntityDespawn despawn)
        {
            EntityManager.Instance.DespawnEntity(despawn.EntityId.Id);
        }

        private void HandleEntityMove(ProtoWorld.EntityMove move)
        {
            var pos = new Vector3(move.Position.X, move.Position.Y, move.Position.Z);
            var rot = new Quaternion(move.Rotation.X, move.Rotation.Y, move.Rotation.Z, move.Rotation.W);
            var vel = new Vector3(move.Velocity.X, move.Velocity.Y, move.Velocity.Z);
            EntityManager.Instance.MoveEntity(move.EntityId.Id, pos, rot, vel);
        }

        private void HandleTerrainChunk(ProtoWorld.TerrainChunk chunk)
        {
            var coord = new Vector2Int(chunk.ChunkX, chunk.ChunkZ);
            byte[] splatmap = chunk.Splatmap?.Length > 0 ? chunk.Splatmap.ToByteArray() : null;
            FindFirstObjectByType<TerrainManager>()?.ApplyTerrainChunk(
                coord, (int)chunk.Resolution, chunk.Heightmap.ToByteArray(), splatmap, chunk.Seed);
        }

        private void HandleContentReveal(ProtoWorld.ContentReveal reveal)
        {
            Debug.Log($"[World] Content revealed: '{reveal.ContentId}' ({reveal.ContentType}) at " +
                      $"({reveal.Location.Position.X}, {reveal.Location.Position.Y}, {reveal.Location.Position.Z})");

            NotificationUI.Instance?.ShowDiscovery("New Discovery",
                $"Found: {reveal.ContentId} ({reveal.ContentType})");

            var pos = new Vector3(reveal.Location.Position.X, reveal.Location.Position.Y, reveal.Location.Position.Z);
            FindFirstObjectByType<MinimapUI>()?.AddMarker(pos, "poi", reveal.ContentId, new Color(0.3f, 0.9f, 0.9f));
        }

        // ─── Combat handlers ────────────────────────────────────────────────

        private void HandleCombatPacket(Packet packet)
        {
            switch (packet.PayloadCase)
            {
                case Packet.PayloadOneofCase.DamageEvent:
                    HandleDamageEvent(packet.DamageEvent);
                    break;
                case Packet.PayloadOneofCase.HealthUpdate:
                    HandleHealthUpdate(packet.HealthUpdate);
                    break;
                case Packet.PayloadOneofCase.EntityDeath:
                    HandleEntityDeath(packet.EntityDeath);
                    break;
                case Packet.PayloadOneofCase.CombatAction:
                    // Visual feedback only — no additional handling needed
                    break;
                case Packet.PayloadOneofCase.LootDrop:
                    HandleLootDrop(packet.LootDrop);
                    break;
                default:
                    Debug.Log($"[Combat] Unhandled sub-type: {packet.PayloadCase}");
                    break;
            }
        }

        private void HandleDamageEvent(ProtoCombat.DamageEvent dmg)
        {
            // Show floating damage number on the target entity
            var targetGo = EntityManager.Instance?.GetEntity(dmg.TargetEntityId);
            if (targetGo != null)
            {
                // Consider high combo or bonus as a "crit" for visual purposes
                bool isBig = dmg.ComboBonus > 0.3f || dmg.ComboCount >= 5;
                string text = isBig ? $"CRIT {dmg.FinalDamage:F0}!" : $"{dmg.FinalDamage:F0}";
                var color = isBig ? Color.yellow : Color.red;
                CombatFeedback.Instance?.ShowFloatingText(targetGo.transform.position, text, color);
            }

            // Trigger damage flash if local player was hit
            var bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (bootstrap != null && dmg.TargetEntityId == bootstrap.PlayerEntityId)
            {
                CombatHUD.Instance?.TakeDamage(dmg.FinalDamage, dmg.PoolHit.ToString());
            }
        }

        private void HandleHealthUpdate(ProtoCombat.HealthUpdate update)
        {
            var bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (bootstrap != null && update.EntityId == bootstrap.PlayerEntityId)
            {
                // Local player health update — refresh all three pools
                CombatHUD.Instance?.UpdatePools(
                    update.Vitality, update.MaxVitality,
                    update.Stamina,  update.MaxStamina,
                    update.Focus,    update.MaxFocus);
            }
            else
            {
                // Remote entity health — track for potential health bar display
                EntityManager.Instance?.UpdateEntityHealth(
                    update.EntityId,
                    update.Vitality, update.MaxVitality);
            }
        }

        private void HandleEntityDeath(ProtoCombat.EntityDeath death)
        {
            Debug.Log($"[Combat] Entity {death.EntityId} died (killed by {death.KillerEntityId})");

            // Show death text above the dying entity
            var go = EntityManager.Instance?.GetEntity(death.EntityId);
            if (go != null)
            {
                CombatFeedback.Instance?.ShowFloatingText(go.transform.position, "DEAD", Color.gray);
            }

            // Despawn after brief pause so the player sees the death
            StartCoroutine(DespawnAfterDelay(death.EntityId, 2f));

            // Notify player if they made the kill
            var bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (bootstrap != null && death.KillerEntityId == bootstrap.PlayerEntityId)
            {
                NotificationUI.Instance?.Show("Kill", "Target eliminated", 0, 3f);
            }
        }

        private System.Collections.IEnumerator DespawnAfterDelay(ulong entityId, float delay)
        {
            yield return new UnityEngine.WaitForSeconds(delay);
            EntityManager.Instance?.DespawnEntity(entityId);
        }

        private void HandleLootDrop(ProtoCombat.LootDrop loot)
        {
            // LootDrop spawns a world loot entity — show a pickup prompt
            Debug.Log($"[Combat] Loot dropped at entity {loot.LootEntityId} (table {loot.LootTableId})");

            var pos = new Vector3(loot.Position.X, loot.Position.Y, loot.Position.Z);
            FindFirstObjectByType<MinimapUI>()?.AddMarker(pos, "loot", "Loot", Color.yellow);

            NotificationUI.Instance?.Show("Loot", "Loot dropped nearby — walk over to collect", 0, 5f);
        }

        // ─── Inventory / Social / Progression stub handlers ─────────────────

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
        private void HandleEnvironmentUpdate(ProtoEnv.EnvironmentUpdate env)
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

        private void HandleAudioZoneEnter(ProtoEnv.AudioZoneEnter zone)
        {
            AudioManager.Instance?.OnAudioZoneEnter(
                zone.ZoneId, zone.MusicTrack, zone.AmbientTrack,
                zone.MusicVolume, zone.AmbientVolume);
        }

        private void HandleAudioZoneLeave(ProtoEnv.AudioZoneLeave zone)
        {
            AudioManager.Instance?.OnAudioZoneLeave(zone.ZoneId);
        }

        private void HandleSoundEvent(ProtoEnv.SoundEvent snd)
        {
            var pos = new Vector3(snd.Position.X, snd.Position.Y, snd.Position.Z);
            AudioManager.Instance?.PlaySoundAt(snd.SoundId, pos, snd.Volume, snd.Radius);
        }

        private void HandleNotification(ProtoEnv.Notification notif)
        {
            NotificationUI.Instance?.Show(notif.Title, notif.Message, (int)notif.Type, notif.Duration);
        }

        private void HandleMinimapUpdate(ProtoEnv.MinimapUpdate map)
        {
            FindFirstObjectByType<MinimapUI>()?.OnMinimapUpdate(
                map.CellX, map.CellZ, (int)map.Resolution, map.ColorData.ToByteArray());
        }

        // ─── TMD handlers ───────────────────────────────────────────────────

        private void HandleTMDResult(ProtoTMD.TMDResult result)
        {
            Debug.Log($"[TMD] Result: op={result.Operation} success={result.Success} charges={result.ChargesRemaining}");

            var tmdUI = FindFirstObjectByType<TMDUI>();
            tmdUI?.OnOperationResult(result.Success, (int)result.Operation, result.ChargesRemaining, result.Error);

            // If scan, show result count
            if (result.Operation == ProtoTMD.TMDOperation.TmdScan && result.ScanResults.Count > 0)
            {
                tmdUI?.OnScanResults(result.ScanResults.Count);
                foreach (var res in result.ScanResults)
                {
                    var pos = new Vector3(res.Position.X, res.Position.Y, res.Position.Z);
                    FindFirstObjectByType<MinimapUI>()?.AddMarker(pos, "resource", res.Name, Color.yellow);
                }
            }
        }

        private void HandleTerrainModification(ProtoTMD.TerrainModification mod)
        {
            Debug.Log($"[TMD] Terrain mod: chunk ({mod.ChunkX}, {mod.ChunkZ})");
            var terrainMgr = FindFirstObjectByType<TerrainManager>();
            if (terrainMgr != null && mod.HeightDeltas != null && mod.HeightDeltas.Length > 0)
            {
                terrainMgr.ApplyTerrainModification(mod.ChunkX, mod.ChunkZ, mod.HeightDeltas.ToByteArray());
            }
        }

        private void HandleTMDStatus(ProtoTMD.TMDStatus status)
        {
            Debug.Log($"[TMD] Status: tier={status.Tier} charges={status.Charges}/{status.MaxCharges}");
            var tmdUI = FindFirstObjectByType<TMDUI>();
            if (tmdUI == null)
            {
                var go = new GameObject("TMDUI");
                tmdUI = go.AddComponent<TMDUI>();
            }
            tmdUI.UpdateStatus((int)status.Tier, status.Charges, status.MaxCharges);
        }

        private void HandleLandClaimInfo(ProtoTMD.LandClaimInfo claim)
        {
            Debug.Log($"[TMD] Land claim {claim.ClaimId}: owner={claim.OwnerId} radius={claim.Radius} yours={claim.IsYours}");
            var pos = new Vector3(claim.Center.X, claim.Center.Y, claim.Center.Z);
            var color = claim.IsYours ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.8f, 0.3f, 0.3f);
            FindFirstObjectByType<MinimapUI>()?.AddMarker(pos, "claim",
                claim.IsYours ? "Your Claim" : "Land Claim", color);
        }

        // ─── Resource Surveying & Gathering handlers ────────────────────────

        private void HandleSurveyResult(ProtoResource.SurveyResult result)
        {
            Debug.Log($"[Resource] Survey result: {result.Spawns.Count} spawns found");

            var entries = new List<SurveyUI.SurveyEntry>();
            foreach (var spawn in result.Spawns)
            {
                var pos = new Vector3(spawn.Position.X, spawn.Position.Y, spawn.Position.Z);

                // Extract 11 quality attributes
                uint[] attrs = new uint[11];
                if (spawn.Attrs != null)
                {
                    attrs[0]  = spawn.Attrs.Conductivity;
                    attrs[1]  = spawn.Attrs.ThermalResistance;
                    attrs[2]  = spawn.Attrs.TensileStrength;
                    attrs[3]  = spawn.Attrs.Malleability;
                    attrs[4]  = spawn.Attrs.Reactivity;
                    attrs[5]  = spawn.Attrs.Density;
                    attrs[6]  = spawn.Attrs.Purity;
                    attrs[7]  = spawn.Attrs.Resonance;
                    attrs[8]  = spawn.Attrs.DecayResistance;
                    attrs[9]  = spawn.Attrs.Flexibility;
                    attrs[10] = spawn.Attrs.HarmonicResponse;
                }

                entries.Add(new SurveyUI.SurveyEntry
                {
                    SpawnId = spawn.SpawnId,
                    TypeId = spawn.TypeId,
                    Name = spawn.Name,
                    ResourceClass = (int)spawn.ResourceClass,
                    QualityTier = spawn.QualityTier,
                    Position = pos,
                    Radius = spawn.Radius,
                    Attributes = attrs
                });

                // Also create/update world ResourceNode for each spawn
                var existing = FindResourceNode(spawn.SpawnId);
                if (existing == null)
                {
                    ResourceNode.Create(spawn.SpawnId, spawn.TypeId, spawn.Name,
                        (int)spawn.ResourceClass, attrs, pos, spawn.Radius, spawn.QualityTier);
                }

                // Add to minimap
                Color markerColor = ResourceNode.GetColorForClass((int)spawn.ResourceClass);
                FindFirstObjectByType<MinimapUI>()?.AddMarker(pos, "resource", spawn.Name, markerColor);
            }

            // Feed SurveyUI
            var surveyUI = SurveyUI.Instance;
            if (surveyUI == null)
            {
                var go = new GameObject("SurveyUI");
                surveyUI = go.AddComponent<SurveyUI>();
            }
            surveyUI.OnSurveyResult(entries);
        }

        private void HandleResourceDetail(ProtoResource.ResourceDetail detail)
        {
            Debug.Log($"[Resource] Detail for spawn {detail.SpawnId}: {detail.Name} ({detail.QualityTier})");
            // TODO: Show detailed resource inspector panel if needed
        }

        private void HandleGatherProgress(ProtoInventory.GatherProgress progress)
        {
            ulong nodeId = progress.NodeEntity?.Id ?? 0;
            Debug.Log($"[Gather] Progress: node={nodeId} {progress.Progress:P0} ({progress.TotalTime:F1}s total)");

            // Update the ResourceNode component
            var node = FindResourceNode(nodeId);
            node?.OnGatherProgress(progress.Progress, progress.TotalTime);

            // Update the center-screen GatheringUI
            var gatherUI = GatheringUI.Instance;
            if (gatherUI == null)
            {
                var go = new GameObject("GatheringUI");
                gatherUI = go.AddComponent<GatheringUI>();
            }

            string name = node?.DisplayName ?? "Resource";
            int resClass = node?.ResourceClass ?? 0;
            gatherUI.ShowGathering(name, resClass, progress.Progress, progress.TotalTime);
        }

        private void HandleGatherComplete(ProtoInventory.GatherComplete complete)
        {
            ulong nodeId = complete.NodeEntity?.Id ?? 0;
            Debug.Log($"[Gather] Complete: node={nodeId} remaining={complete.NodeRemaining} tier={complete.QualityTier} items={complete.ItemsReceived.Count}");

            // Update the ResourceNode
            var node = FindResourceNode(nodeId);
            node?.OnGatherComplete(complete.NodeRemaining);

            // Build attributes from first received item's resource_attrs (if present)
            uint[] attrs = null;
            string name = node?.DisplayName ?? "Resource";
            int resClass = node?.ResourceClass ?? 0;
            uint totalQty = 0;

            foreach (var item in complete.ItemsReceived)
            {
                totalQty += item.Quantity;
                if (attrs == null && item.ResourceAttrs != null)
                {
                    attrs = new uint[11];
                    attrs[0]  = item.ResourceAttrs.Conductivity;
                    attrs[1]  = item.ResourceAttrs.ThermalResistance;
                    attrs[2]  = item.ResourceAttrs.TensileStrength;
                    attrs[3]  = item.ResourceAttrs.Malleability;
                    attrs[4]  = item.ResourceAttrs.Reactivity;
                    attrs[5]  = item.ResourceAttrs.Density;
                    attrs[6]  = item.ResourceAttrs.Purity;
                    attrs[7]  = item.ResourceAttrs.Resonance;
                    attrs[8]  = item.ResourceAttrs.DecayResistance;
                    attrs[9]  = item.ResourceAttrs.Flexibility;
                    attrs[10] = item.ResourceAttrs.HarmonicResponse;
                }
            }

            // If no attrs from items, fall back to node attrs
            if (attrs == null && node != null)
                attrs = node.Attributes;

            // Show the result panel
            var gatherUI = GatheringUI.Instance;
            if (gatherUI == null)
            {
                var go = new GameObject("GatheringUI");
                gatherUI = go.AddComponent<GatheringUI>();
            }
            gatherUI.ShowResult(name, complete.QualityTier, resClass,
                attrs, totalQty, complete.NodeRemaining);

            // Notify
            NotificationUI.Instance?.Show("Gathered",
                $"+{totalQty} {name} ({complete.QualityTier})", 0, 3f);
        }

        /// <summary>Find a ResourceNode by spawn ID in the scene.</summary>
        private ResourceNode FindResourceNode(ulong spawnId)
        {
            // ResourceNode names follow the pattern "ResourceNode_{spawnId}_..."
            foreach (var node in FindObjectsByType<ResourceNode>(FindObjectsSortMode.None))
            {
                if (node.SpawnId == spawnId) return node;
            }
            return null;
        }

        // ─── Character Creation handlers ────────────────────────────────────

        private void HandleCharacterList(ProtoCharacter.CharacterListResponse list)
        {
            Debug.Log($"[Character] Received character list: {list.Characters.Count} characters, max={list.MaxCharacters}");
            var bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null) return;

            // Build full character list for the select screen
            var characters = new System.Collections.Generic.List<CharacterSelectUI.CharacterEntry>();
            foreach (var ch in list.Characters)
            {
                characters.Add(new CharacterSelectUI.CharacterEntry
                {
                    id = ch.CharacterId?.Id ?? 0,
                    firstName = ch.FirstName,
                    lastName = ch.LastName,
                    level = (int)ch.Level,
                    zoneName = ch.ZoneName,
                    race = (int)(ch.Appearance?.Race ?? 0)
                });
            }

            bootstrap.OnCharacterListReceived(characters, (int)list.MaxCharacters);
        }

        private void HandleCharacterCreateResponse(ProtoCharacter.CharacterCreateResponse resp)
        {
            var bootstrap = FindFirstObjectByType<GameBootstrap>();
            bootstrap?.OnCharacterCreateResponse(
                resp.Success, resp.Error,
                resp.CharacterId?.Id ?? 0);
        }

        private void HandleCharacterAppearance(ProtoCharacter.CharacterAppearanceUpdate appearance)
        {
            Debug.Log($"[Character] Appearance update for entity {appearance.EntityId.Id}: " +
                      $"{appearance.FirstName} {appearance.LastName}");
            // Store appearance data on the networked entity for rendering
        }

        // ─── Economy / NPC handlers ─────────────────────────────────────────

        private void HandleNPCData(ProtoEconomy.NPCData data)
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

        private void HandleShopBuyResponse(ProtoEconomy.ShopBuyResponse resp)
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

        private void HandleShopSellResponse(ProtoEconomy.ShopSellResponse resp)
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

        private void HandleWalletUpdate(ProtoEconomy.WalletUpdate update)
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

        private void HandleMartialMoveResponse(ProtoEconomy.MartialMoveResponse resp)
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

        private void HandleMartialArtsState(ProtoEconomy.MartialArtsState state)
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

        private void HandleAdminResponse(ProtoAdmin.AdminResponse resp)
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

        private void HandleAdminState(ProtoAdmin.AdminState state)
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
