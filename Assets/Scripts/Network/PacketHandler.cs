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
using ProtoProgression = Orlo.Proto.Progression;

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

        // Death overlay state
        private bool _deathOverlayActive;
        private float _deathOverlayTimer;

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
                case Packet.PayloadOneofCase.AdminCreatureList:
                    HandleCreatureList(packet.AdminCreatureList);
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

            // Apply scale if server sent it (trees have scale_variation 0.8-1.3)
            if (spawn.Transform.Scale != null)
            {
                float sx = spawn.Transform.Scale.X;
                float sy = spawn.Transform.Scale.Y;
                float sz = spawn.Transform.Scale.Z;
                if (sx > 0.01f || sy > 0.01f || sz > 0.01f)
                {
                    var go = EntityManager.Instance.GetEntity(spawn.EntityId.Id);
                    if (go != null)
                        go.transform.localScale = new Vector3(sx, sy, sz);
                }
            }

            // Track entity name for targeting display
            string displayName = !string.IsNullOrEmpty(spawn.AssetId) ? spawn.AssetId : $"Entity {spawn.EntityType}";
            // Clean up asset IDs like "creature_stalker" to "Stalker"
            if (displayName.Contains("_"))
            {
                string[] parts = displayName.Split('_');
                displayName = parts.Length > 1 ? CapitalizeFirst(parts[parts.Length - 1]) : CapitalizeFirst(parts[0]);
            }
            EntityManager.Instance.SetEntityName(spawn.EntityId.Id, displayName);
        }

        private static string CapitalizeFirst(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpper(s[0]) + s.Substring(1);
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

            // Check if the local player died
            var bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (bootstrap != null && death.EntityId == bootstrap.PlayerEntityId)
            {
                // Local player death — show death overlay instead of death animation
                var chatUI = FindFirstObjectByType<ChatUI>();
                chatUI?.AddSystemMessage("You have been defeated! Respawning at Threshold...");
                StartCoroutine(ShowDeathOverlay());

                // Clear any current target
                TargetingSystem.Instance?.ClearTarget();
                return;
            }

            var go = EntityManager.Instance?.GetEntity(death.EntityId);
            if (go != null)
            {
                CombatFeedback.Instance?.ShowFloatingText(go.transform.position, "DEAD", Color.gray);

                // Play death animation: fall over and fade out
                StartCoroutine(DeathAnimation(go, death.EntityId));
            }
            else
            {
                // Entity not found, just schedule cleanup
                StartCoroutine(DespawnAfterDelay(death.EntityId, 5f));
            }

            // Clear target if the dead entity was our target
            TargetingSystem.Instance?.ClearTargetIfMatch(death.EntityId);

            // Notify player if they made the kill
            if (bootstrap != null && death.KillerEntityId == bootstrap.PlayerEntityId)
            {
                NotificationUI.Instance?.Show("Kill", "Target eliminated", 0, 3f);
            }
        }

        private System.Collections.IEnumerator ShowDeathOverlay()
        {
            _deathOverlayActive = true;
            _deathOverlayTimer = 10f;

            // Count down every frame
            while (_deathOverlayTimer > 0f)
            {
                _deathOverlayTimer -= Time.deltaTime;
                yield return null;
            }

            _deathOverlayActive = false;
        }

        private void OnGUI()
        {
            if (!_deathOverlayActive) return;

            float s = UIScaler.Scale;

            // Full-screen dark overlay
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            // "DEFEATED" title
            GUI.color = new Color(0.8f, 0.15f, 0.1f);
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = UIScaler.ScaledFontSize(48),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 80 * s), "DEFEATED", titleStyle);

            // Respawn countdown
            int secondsLeft = Mathf.CeilToInt(_deathOverlayTimer);
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = UIScaler.ScaledFontSize(18),
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(0, Screen.height * 0.45f, Screen.width, 40 * s),
                $"Respawning in {secondsLeft} seconds...", subStyle);

            // Hint text
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            var hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = UIScaler.ScaledFontSize(12),
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(0, Screen.height * 0.52f, Screen.width, 30 * s),
                "You will respawn at the nearest settlement.", hintStyle);

            GUI.color = Color.white;
        }

        private System.Collections.IEnumerator DeathAnimation(GameObject go, ulong entityId)
        {
            if (go == null) yield break;

            // Phase 1: Fall over (rotate 90 degrees on X axis over 0.5s)
            float fallDuration = 0.5f;
            float elapsed = 0f;
            Quaternion startRot = go.transform.rotation;
            Quaternion endRot = startRot * Quaternion.Euler(90f, 0f, 0f);

            while (elapsed < fallDuration && go != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fallDuration);
                // Ease out for natural fall
                t = 1f - (1f - t) * (1f - t);
                go.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
                yield return null;
            }

            // Phase 2: Hold corpse visible, then fade out over last 1.5s of the 5s total
            float holdTime = 3f;
            yield return new UnityEngine.WaitForSeconds(holdTime);

            // Phase 3: Fade out
            float fadeDuration = 1.5f;
            elapsed = 0f;
            var renderers = go != null ? go.GetComponentsInChildren<Renderer>() : null;

            // Store original colors and switch materials to transparent mode
            if (renderers != null)
            {
                foreach (var r in renderers)
                {
                    foreach (var mat in r.materials)
                    {
                        mat.SetFloat("_Mode", 3); // Transparent
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.SetInt("_ZWrite", 0);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.EnableKeyword("_ALPHABLEND_ON");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.renderQueue = 3000;
                    }
                }
            }

            while (elapsed < fadeDuration && go != null)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                if (renderers != null)
                {
                    foreach (var r in renderers)
                    {
                        foreach (var mat in r.materials)
                        {
                            var c = mat.color;
                            c.a = alpha;
                            mat.color = c;
                        }
                    }
                }
                yield return null;
            }

            // Final despawn
            EntityManager.Instance?.DespawnEntity(entityId);
        }

        private System.Collections.IEnumerator DespawnAfterDelay(ulong entityId, float delay)
        {
            yield return new UnityEngine.WaitForSeconds(delay);
            EntityManager.Instance?.DespawnEntity(entityId);
        }

        private void HandleLootDrop(ProtoCombat.LootDrop loot)
        {
            Debug.Log($"[Combat] Loot dropped: entity={loot.LootEntityId} table={loot.LootTableId}");

            var pos = new Vector3(loot.Position.X, loot.Position.Y, loot.Position.Z);

            // Spawn a visible glowing loot entity in the world
            EntityManager.Instance?.SpawnLootEntity(loot.LootEntityId, pos, "Loot");

            // Minimap marker
            FindFirstObjectByType<MinimapUI>()?.AddMarker(pos, "loot", "Loot", Color.yellow);

            NotificationUI.Instance?.Show("Loot", "Loot dropped nearby — press F to pick up", 0, 5f);
        }

        // ─── Inventory / Social / Progression stub handlers ─────────────────

        private void HandleInventoryPacket(Packet packet)
        {
            switch (packet.PayloadCase)
            {
                case Packet.PayloadOneofCase.InventoryUpdate:
                    HandleInventoryUpdate(packet.InventoryUpdate);
                    break;
                case Packet.PayloadOneofCase.ItemAdd:
                    HandleItemAdd(packet.ItemAdd);
                    break;
                case Packet.PayloadOneofCase.ItemRemove:
                    HandleItemRemove(packet.ItemRemove);
                    break;
                case Packet.PayloadOneofCase.ItemMoveConfirm:
                    HandleItemMoveConfirm(packet.ItemMoveConfirm);
                    break;
                case Packet.PayloadOneofCase.EquipmentChanged:
                    HandleEquipmentChanged(packet.EquipmentChanged);
                    break;
                case Packet.PayloadOneofCase.CraftProgress:
                    HandleCraftProgress(packet.CraftProgress);
                    break;
                case Packet.PayloadOneofCase.CraftComplete:
                    HandleCraftComplete(packet.CraftComplete);
                    break;
                case Packet.PayloadOneofCase.RecipeDiscovered:
                    HandleRecipeDiscovered(packet.RecipeDiscovered);
                    break;
                case Packet.PayloadOneofCase.LootPickup:
                    HandleLootPickup(packet.LootPickup);
                    break;
                default:
                    Debug.Log($"[Inventory] Received: {packet.PayloadCase}");
                    break;
            }
        }

        private void HandleCraftProgress(ProtoInventory.CraftProgress progress)
        {
            Debug.Log($"[Crafting] Progress: recipe={progress.RecipeId} {progress.Progress:P0}");
            CraftingUI.Instance?.OnCraftProgress(progress.RecipeId, progress.Progress);
        }

        private void HandleCraftComplete(ProtoInventory.CraftComplete complete)
        {
            Debug.Log($"[Crafting] Complete: recipe={complete.RecipeId} success={complete.Success}");

            var crafting = CraftingUI.Instance;
            if (crafting == null) return;

            if (complete.Result != null && complete.Result.Metadata != null && complete.Result.Metadata.Count > 0)
            {
                // Build a detailed result from the proto item metadata
                var stats = new Dictionary<string, float>();
                foreach (var kvp in complete.Result.Metadata)
                {
                    if (float.TryParse(kvp.Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float val))
                        stats[kvp.Key] = val;
                }

                var result = new CraftingUI.CraftResultData
                {
                    ItemName = complete.Result.Metadata.ContainsKey("name")
                        ? complete.Result.Metadata["name"]
                        : $"Item #{complete.Result.ItemId}",
                    CraftedBy = complete.Result.CraftedBy ?? "",
                    AssemblyTier = complete.Result.Metadata.ContainsKey("tier")
                        ? complete.Result.Metadata["tier"]
                        : "Good",
                    Stats = stats,
                    Condition = complete.Result.Condition > 0 ? complete.Result.Condition : 1.0f
                };
                crafting.OnCraftComplete(result);
            }
            else
            {
                // Simple path -- no detailed result data
                crafting.OnSimpleCraftComplete(complete.Success, $"Recipe #{complete.RecipeId}");
            }

            if (complete.Success)
            {
                NotificationUI.Instance?.Show("Crafting", "Item crafted successfully!", 0, 3f);
            }
        }

        private void HandleRecipeDiscovered(ProtoInventory.RecipeDiscovered recipe)
        {
            Debug.Log($"[Crafting] New recipe discovered: {recipe.RecipeName} (id={recipe.RecipeId})");
            NotificationUI.Instance?.Show("Recipe Discovered", recipe.RecipeName, 0, 5f);
        }

        // HandleGatherProgress and HandleGatherComplete are defined below
        // with full ResourceNode + GatheringUI integration

        // ─── Inventory sync handlers ────────────────────────────────────────

        private void HandleInventoryUpdate(ProtoInventory.InventoryUpdate update)
        {
            Debug.Log($"[Inventory] Full update: {update.Slots.Count} slots, {update.Equipment.Count} equipment, " +
                      $"weight={update.TotalWeight:F1}/{update.MaxWeight:F1}");

            var inv = InventoryUI.Instance;
            if (inv == null)
            {
                var go = new GameObject("InventoryUI");
                inv = go.AddComponent<InventoryUI>();
            }

            var items = new List<InventoryUI.ItemSlot>();
            foreach (var slot in update.Slots)
            {
                items.Add(ProtoItemToSlot(slot.Item, (int)slot.SlotIndex));
            }

            var equipment = new List<InventoryUI.ItemSlot>();
            foreach (var eq in update.Equipment)
            {
                equipment.Add(ProtoItemToSlot(eq.Item, MapEquipSlotToIndex(eq.Slot)));
            }

            inv.SetItems(items, equipment, update.TotalWeight, update.MaxWeight);

            // Sync the paper-doll EquipmentUI with the full server state
            var equipUI = EquipmentUI.Instance;
            if (equipUI != null)
            {
                var equipDict = new Dictionary<int, InventoryUI.ItemSlot>();
                foreach (var eq in update.Equipment)
                {
                    int protoSlotId = (int)eq.Slot;
                    equipDict[protoSlotId] = ProtoItemToSlot(eq.Item, protoSlotId);
                }
                equipUI.SetEquipment(equipDict);
            }

            // Refresh all equipment visuals on the player model
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var visualMgr = player.GetComponent<EquipmentVisualManager>();
                if (visualMgr == null)
                    visualMgr = player.AddComponent<EquipmentVisualManager>();
                visualMgr.RefreshAllVisuals();
            }
        }

        private void HandleItemAdd(ProtoInventory.ItemAdd add)
        {
            Debug.Log($"[Inventory] Item added to slot {add.SlotIndex}: item#{add.Item?.ItemId} x{add.Item?.Quantity}");

            var inv = InventoryUI.Instance;
            if (inv == null) return;

            var slot = ProtoItemToSlot(add.Item, (int)add.SlotIndex);
            inv.AddItem((int)add.SlotIndex, slot, add.TotalWeight);

            // Show notification with item name
            string name = add.Item?.Metadata != null && add.Item.Metadata.ContainsKey("name")
                ? add.Item.Metadata["name"]
                : $"Item #{add.Item?.ItemId}";
            uint qty = add.Item?.Quantity ?? 1;
            NotificationUI.Instance?.Show("Item Received",
                qty > 1 ? $"Received: {name} x{qty}" : $"Received: {name}", 0, 3f);

            // Flash the inventory icon to alert the player
            inv.FlashNewItem();

            // Show floating "+item" text at player position
            var bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (bootstrap != null)
            {
                var playerGo = EntityManager.Instance?.GetEntity(bootstrap.PlayerEntityId);
                if (playerGo != null)
                {
                    CombatFeedback.Instance?.ShowFloatingText(
                        playerGo.transform.position,
                        qty > 1 ? $"+{qty} {name}" : $"+{name}",
                        new Color(0.3f, 1f, 0.3f));
                }
            }

            Debug.Log($"[Inventory] Pickup sound would play here for: {name}");
        }

        private void HandleItemRemove(ProtoInventory.ItemRemove remove)
        {
            Debug.Log($"[Inventory] Item removed from slot {remove.SlotIndex}: -{remove.QuantityRemoved}");

            var inv = InventoryUI.Instance;
            if (inv == null) return;

            inv.RemoveItem((int)remove.SlotIndex, remove.QuantityRemoved, remove.TotalWeight);
        }

        private void HandleItemMoveConfirm(ProtoInventory.ItemMoveConfirm confirm)
        {
            Debug.Log($"[Inventory] Move confirmed: slot {confirm.FromSlot} -> {confirm.ToSlot}");

            var inv = InventoryUI.Instance;
            if (inv == null) return;

            var fromItem = ProtoItemToSlot(confirm.FromItem, (int)confirm.FromSlot);
            var toItem = ProtoItemToSlot(confirm.ToItem, (int)confirm.ToSlot);
            inv.ConfirmMove((int)confirm.FromSlot, (int)confirm.ToSlot, fromItem, toItem);
        }

        private void HandleEquipmentChanged(ProtoInventory.EquipmentChanged changed)
        {
            Debug.Log($"[Inventory] Equipment changed: slot={changed.Slot} inv_slot={changed.InventorySlot}");

            // Update the inline equipment panel in InventoryUI
            var inv = InventoryUI.Instance;
            if (inv != null)
            {
                int equipIdx = MapEquipSlotToIndex(changed.Slot);
                var equipItem = ProtoItemToSlot(changed.Item, equipIdx);
                var invItem = ProtoItemToSlot(changed.InventoryItem, (int)changed.InventorySlot);
                inv.UpdateEquipment(equipIdx, equipItem, (int)changed.InventorySlot, invItem);
            }

            // Update the paper-doll EquipmentUI (server-authoritative: this is the ONLY
            // place where equipment display state is mutated based on server confirmation)
            var equipUI = EquipmentUI.Instance;
            if (equipUI != null)
            {
                int protoSlotId = (int)changed.Slot;
                if (changed.Item != null && changed.Item.ItemId != 0)
                {
                    // Server confirmed an item is now equipped in this slot
                    var itemSlot = ProtoItemToSlot(changed.Item, protoSlotId);
                    equipUI.ServerEquipItem(protoSlotId, itemSlot);

                    // Attach equipment visual to player model
                    var player = GameObject.FindWithTag("Player");
                    if (player != null)
                    {
                        var visualMgr = player.GetComponent<EquipmentVisualManager>();
                        if (visualMgr == null)
                            visualMgr = player.AddComponent<EquipmentVisualManager>();
                        visualMgr.OnSlotEquipped(protoSlotId, itemSlot);
                    }
                }
                else
                {
                    // Server confirmed this slot is now empty
                    equipUI.ServerUnequipItem(protoSlotId);

                    // Remove equipment visual from player model
                    var player = GameObject.FindWithTag("Player");
                    if (player != null)
                    {
                        var visualMgr = player.GetComponent<EquipmentVisualManager>();
                        visualMgr?.OnSlotUnequipped((int)changed.Slot);
                    }
                }
            }
        }

        private void HandleLootPickup(ProtoInventory.LootPickup pickup)
        {
            ulong lootId = pickup.LootEntity?.Id ?? 0;
            var pos = new Vector3(pickup.Position.X, pickup.Position.Y, pickup.Position.Z);
            Debug.Log($"[Inventory] Loot pickup confirmed: entity={lootId} ({pickup.Items.Count} items)");

            // Remove the loot entity from the world (if not already removed optimistically)
            if (lootId != 0)
                EntityManager.Instance?.DespawnLootEntity(lootId);

            // Show what was picked up
            foreach (var item in pickup.Items)
            {
                string name = item?.Metadata != null && item.Metadata.ContainsKey("name")
                    ? item.Metadata["name"]
                    : $"Item #{item?.ItemId}";
                uint qty = item?.Quantity ?? 1;
                NotificationUI.Instance?.Show("Looted",
                    qty > 1 ? $"{name} x{qty}" : name, 0, 3f);
            }
        }

        /// <summary>Convert a proto ItemStack to InventoryUI.ItemSlot.</summary>
        private InventoryUI.ItemSlot ProtoItemToSlot(ProtoInventory.ItemStack item, int slotIndex)
        {
            if (item == null || item.ItemId == 0)
                return default;

            string name = item.Metadata != null && item.Metadata.ContainsKey("name")
                ? item.Metadata["name"]
                : $"Item #{item.ItemId}";
            string desc = item.Metadata != null && item.Metadata.ContainsKey("description")
                ? item.Metadata["description"]
                : "";
            int rarity = 0;
            if (item.Metadata != null && item.Metadata.ContainsKey("rarity"))
                int.TryParse(item.Metadata["rarity"], out rarity);
            float weight = 0;
            if (item.Metadata != null && item.Metadata.ContainsKey("weight"))
                float.TryParse(item.Metadata["weight"],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out weight);
            int category = 0;
            if (item.Metadata != null && item.Metadata.ContainsKey("category"))
                int.TryParse(item.Metadata["category"], out category);

            // Resource quality attributes
            uint[] resAttrs = null;
            if (item.ResourceAttrs != null)
            {
                resAttrs = new uint[11];
                resAttrs[0]  = item.ResourceAttrs.Conductivity;
                resAttrs[1]  = item.ResourceAttrs.ThermalResistance;
                resAttrs[2]  = item.ResourceAttrs.TensileStrength;
                resAttrs[3]  = item.ResourceAttrs.Malleability;
                resAttrs[4]  = item.ResourceAttrs.Reactivity;
                resAttrs[5]  = item.ResourceAttrs.Density;
                resAttrs[6]  = item.ResourceAttrs.Purity;
                resAttrs[7]  = item.ResourceAttrs.Resonance;
                resAttrs[8]  = item.ResourceAttrs.DecayResistance;
                resAttrs[9]  = item.ResourceAttrs.Flexibility;
                resAttrs[10] = item.ResourceAttrs.HarmonicResponse;
            }

            return new InventoryUI.ItemSlot
            {
                Occupied = true,
                ItemId = (uint)slotIndex, // slot index for SetItems mapping
                Name = name,
                Description = desc,
                RarityColor = InventoryUI.GetRarityColor(rarity),
                Rarity = rarity,
                Category = category,
                Weight = weight,
                StackCount = (int)item.Quantity,
                Condition = item.Condition > 0 ? item.Condition : 1f,
                MaxCondition = item.MaxCondition > 0 ? item.MaxCondition : 1f,
                CraftedBy = item.CraftedBy ?? "",
                ResourceAttrs = resAttrs
            };
        }

        /// <summary>Map proto EquipmentSlot enum to local array index.</summary>
        private int MapEquipSlotToIndex(ProtoInventory.EquipmentSlot slot)
        {
            return slot switch
            {
                ProtoInventory.EquipmentSlot.Head => 0,
                ProtoInventory.EquipmentSlot.Chest => 1,
                ProtoInventory.EquipmentSlot.Legs => 2,
                ProtoInventory.EquipmentSlot.Feet => 3,
                ProtoInventory.EquipmentSlot.Gloves => 4,
                ProtoInventory.EquipmentSlot.LeftBracer => 5,
                ProtoInventory.EquipmentSlot.RightBracer => 6,
                ProtoInventory.EquipmentSlot.LeftBicep => 7,
                ProtoInventory.EquipmentSlot.RightBicep => 8,
                ProtoInventory.EquipmentSlot.Shoulders => 9,
                ProtoInventory.EquipmentSlot.Belt => 10,
                ProtoInventory.EquipmentSlot.Backpack => 11,
                ProtoInventory.EquipmentSlot.LeftWrist => 12,
                ProtoInventory.EquipmentSlot.RightWrist => 13,
                ProtoInventory.EquipmentSlot.LeftHand => 14,
                ProtoInventory.EquipmentSlot.RightHand => 15,
                ProtoInventory.EquipmentSlot.TwoHands => 16,
                _ => 0
            };
        }

        // ─── Social / Progression handlers ──────────────────────────────────

        private void HandleSocialPacket(Packet packet)
        {
            switch (packet.PayloadCase)
            {
                case Packet.PayloadOneofCase.ChatMessage:
                    HandleChatMessage(packet.ChatMessage);
                    break;
                case Packet.PayloadOneofCase.SystemMessage:
                    HandleSystemMessage(packet.SystemMessage);
                    break;
                case Packet.PayloadOneofCase.PartyUpdate:
                    HandlePartyUpdate(packet.PartyUpdate);
                    break;
                case Packet.PayloadOneofCase.PartyInviteNotify:
                    HandlePartyInvite(packet.PartyInviteNotify);
                    break;
                case Packet.PayloadOneofCase.FriendsList:
                    HandleFriendsList(packet.FriendsList);
                    break;
                case Packet.PayloadOneofCase.FriendStatus:
                    HandleFriendStatus(packet.FriendStatus);
                    break;
                default:
                    Debug.Log($"[Social] Unhandled: {packet.PayloadCase}");
                    break;
            }
        }

        private void HandlePartyUpdate(Orlo.Proto.Social.PartyUpdate update)
        {
            var partyUI = FindFirstObjectByType<PartyUI>();
            if (partyUI != null)
            {
                var members = new System.Collections.Generic.List<PartyUI.PartyMember>();
                foreach (var m in update.Members)
                {
                    members.Add(new PartyUI.PartyMember
                    {
                        Name = m.Name,
                        CurrentHP = m.Health,
                        MaxHP = m.MaxHealth,
                        IsLeader = m.Name == update.LeaderName
                    });
                }
                partyUI.SetPartyData(members);
            }
        }

        private void HandlePartyInvite(Orlo.Proto.Social.PartyInviteNotify invite)
        {
            // Route to CharacterSelectUI if in lobby, or ChatUI if in game
            var charSelect = FindFirstObjectByType<CharacterSelectUI>();
            if (charSelect != null)
            {
                charSelect.AddPartyInvite(invite.InviterName);
            }
            else
            {
                var chatUI = FindFirstObjectByType<ChatUI>();
                chatUI?.AddSystemMessage($"{invite.InviterName} invited you to a party. Type /party_accept to join.");
            }
        }

        private void HandleFriendsList(Orlo.Proto.Social.FriendsList list)
        {
            var friendsUI = FriendsUI.Instance;
            if (friendsUI == null) return;

            var friends = new System.Collections.Generic.List<FriendsUI.FriendEntry>();
            foreach (var f in list.Friends)
            {
                friends.Add(new FriendsUI.FriendEntry
                {
                    Name = f.Name,
                    Online = f.Online,
                    ZoneName = f.ZoneName,
                    Note = "",
                    Category = 0
                });
            }
            friendsUI.SetFriendsList(friends);
        }

        private void HandleFriendStatus(Orlo.Proto.Social.FriendStatusNotify status)
        {
            FriendsUI.Instance?.UpdateFriendStatus(status.Name, status.Online, "");

            // Also route to CharacterSelectUI lobby
            var charSelect = FindFirstObjectByType<CharacterSelectUI>();
            if (charSelect != null)
            {
                var lobbyFriends = new System.Collections.Generic.List<CharacterSelectUI.LobbyFriend>();
                lobbyFriends.Add(new CharacterSelectUI.LobbyFriend
                {
                    Name = status.Name,
                    Online = status.Online,
                    Zone = ""
                });
                charSelect.SetLobbyFriends(lobbyFriends);
            }
        }

        private void HandleChatMessage(Orlo.Proto.Social.ChatMessage msg)
        {
            // Map proto channel enum to display channel name
            string channel = msg.Channel switch
            {
                Orlo.Proto.Social.ChatChannel.Proximity => "Say",
                Orlo.Proto.Social.ChatChannel.Zone => "Zone",
                Orlo.Proto.Social.ChatChannel.Global => "Global",
                Orlo.Proto.Social.ChatChannel.Party => "Party",
                Orlo.Proto.Social.ChatChannel.Whisper => "Whisper",
                Orlo.Proto.Social.ChatChannel.System => "System",
                Orlo.Proto.Social.ChatChannel.Guild => "Guild",
                Orlo.Proto.Social.ChatChannel.Say => "Say",
                Orlo.Proto.Social.ChatChannel.Yell => "Yell",
                Orlo.Proto.Social.ChatChannel.Trade => "Trade",
                Orlo.Proto.Social.ChatChannel.Lfg => "LFG",
                Orlo.Proto.Social.ChatChannel.Officer => "Officer",
                Orlo.Proto.Social.ChatChannel.Raid => "Raid",
                Orlo.Proto.Social.ChatChannel.Mentor => "Mentor",
                Orlo.Proto.Social.ChatChannel.Circle => "Circle",
                _ => "Global"
            };

            var chatUI = ChatUI.Instance;
            if (chatUI != null)
                chatUI.ReceiveMessage(msg.SenderName, channel, msg.Content);
            else
                Debug.Log($"[Chat] [{channel}] {msg.SenderName}: {msg.Content}");

            // Rate limit feedback
            if (channel == "System" && msg.Content.Contains("too fast"))
            {
                ChatUI.Instance?.ShowRateLimitWarning();
            }

            // Route guild chat to lobby if in character select
            if (channel == "Guild")
            {
                var charSelect = FindFirstObjectByType<CharacterSelectUI>();
                charSelect?.AddGuildChatMessage($"{msg.SenderName}: {msg.Content}");
            }
        }

        private void HandleSystemMessage(Orlo.Proto.Social.SystemMessage msg)
        {
            var chatUI = FindFirstObjectByType<ChatUI>();
            if (chatUI != null)
                chatUI.AddSystemMessage(msg.Text);
            else
                Debug.Log($"[System] {msg.Text}");
        }

        private void HandleProgressionPacket(Packet packet)
        {
            switch (packet.PayloadCase)
            {
                case Packet.PayloadOneofCase.QuestAvailable:
                    HandleQuestAvailable(packet.QuestAvailable);
                    break;
                case Packet.PayloadOneofCase.QuestProgress:
                    HandleQuestProgress(packet.QuestProgress);
                    break;
                case Packet.PayloadOneofCase.QuestComplete:
                    HandleQuestComplete(packet.QuestComplete);
                    break;
                case Packet.PayloadOneofCase.QuestTurnInResponse:
                    HandleQuestTurnInResponse(packet.QuestTurnInResponse);
                    break;
                case Packet.PayloadOneofCase.QuestLog:
                    HandleQuestLog(packet.QuestLog);
                    break;
                case Packet.PayloadOneofCase.XpGain:
                    HandleXPGain(packet.XpGain);
                    break;
                case Packet.PayloadOneofCase.LevelUp:
                    HandleLevelUp(packet.LevelUp);
                    break;
                default:
                    Debug.Log($"[Progression] Received: {packet.PayloadCase}");
                    break;
            }
        }

        // ─── Quest handlers ─────────────────────────────────────────────────

        private void HandleQuestAvailable(ProtoProgression.QuestAvailable available)
        {
            var q = available.Quest;
            if (q == null) return;
            Debug.Log($"[Quest] Available: {q.Name} (id={q.QuestId})");

            var questDialog = QuestDialogUI.Instance;
            if (questDialog == null)
            {
                var go = new GameObject("QuestDialogUI");
                questDialog = go.AddComponent<QuestDialogUI>();
            }

            var questData = ProtoQuestToData(q);
            // Show as offer — NPC name comes from the NPC interaction context
            questDialog.ShowQuestOffer(0, "Quest Giver", q.Description, questData);
        }

        private void HandleQuestProgress(ProtoProgression.QuestProgress progress)
        {
            Debug.Log($"[Quest] Progress: quest={progress.QuestId} obj#{progress.ObjectiveIndex} " +
                      $"{progress.CurrentCount}/{progress.RequiredCount}");

            // Update quest dialog if open
            QuestDialogUI.Instance?.UpdateObjective(
                progress.QuestId,
                (int)progress.ObjectiveIndex,
                (int)progress.CurrentCount,
                (int)progress.RequiredCount);

            // Show notification
            NotificationUI.Instance?.Show("Quest Update",
                $"Objective progress: {progress.CurrentCount}/{progress.RequiredCount}", 0, 2f);
        }

        private void HandleQuestComplete(ProtoProgression.QuestComplete complete)
        {
            Debug.Log($"[Quest] All objectives met: {complete.QuestId}");
            NotificationUI.Instance?.Show("Quest Complete",
                $"Quest ready to turn in!", 0, 5f);
        }

        private void HandleQuestTurnInResponse(ProtoProgression.QuestTurnInResponse response)
        {
            Debug.Log($"[Quest] Turn-in result: quest={response.QuestId} success={response.Success}");

            if (response.Success)
            {
                string rewardText = "";
                if (response.Rewards != null)
                {
                    if (response.Rewards.Xp > 0) rewardText += $"+{response.Rewards.Xp} XP  ";
                    if (response.Rewards.SkillPoints > 0) rewardText += $"+{response.Rewards.SkillPoints} SP  ";
                }

                QuestDialogUI.Instance?.ShowTurnInResult(true, $"Quest complete! {rewardText}");
                NotificationUI.Instance?.Show("Quest Turned In", rewardText, 0, 5f);
            }
            else
            {
                QuestDialogUI.Instance?.ShowTurnInResult(false, response.Error);
            }
        }

        private void HandleQuestLog(ProtoProgression.QuestLog log)
        {
            Debug.Log($"[Quest] Quest log: {log.ActiveQuests.Count} active, {log.CompletedQuestIds.Count} completed, " +
                      $"{log.AvailableQuests.Count} available");
            // TODO: Feed QuestLogUI with full quest log data
        }

        private void HandleXPGain(ProtoProgression.XPGain xp)
        {
            Debug.Log($"[Progression] +{xp.Amount} XP from {xp.SourceType} (total: {xp.NewTotal})");
            NotificationUI.Instance?.Show("XP Gained", $"+{xp.Amount} XP", 0, 2f);
        }

        private void HandleLevelUp(ProtoProgression.LevelUp levelUp)
        {
            Debug.Log($"[Progression] LEVEL UP! Now level {levelUp.NewLevel}");
            NotificationUI.Instance?.Show("Level Up!", $"You are now level {levelUp.NewLevel}!", 0, 8f);
        }

        /// <summary>Convert a proto QuestInfo to QuestDialogUI.QuestData.</summary>
        private QuestDialogUI.QuestData ProtoQuestToData(ProtoProgression.QuestInfo quest)
        {
            var objectives = new List<QuestDialogUI.QuestObjective>();
            if (quest.Objectives != null)
            {
                foreach (var obj in quest.Objectives)
                {
                    objectives.Add(new QuestDialogUI.QuestObjective
                    {
                        Description = obj.Description,
                        Current = (int)obj.CurrentCount,
                        Required = (int)obj.RequiredCount,
                        Complete = obj.CurrentCount >= obj.RequiredCount
                    });
                }
            }

            var rewards = new QuestDialogUI.QuestRewardData();
            if (quest.Rewards != null)
            {
                rewards.XP = quest.Rewards.Xp;
                rewards.SkillPoints = quest.Rewards.SkillPoints;
                rewards.ItemNames = new List<string>();
                foreach (var itemId in quest.Rewards.ItemIds)
                    rewards.ItemNames.Add(itemId); // Display item IDs until we have a lookup table
            }

            var state = quest.State switch
            {
                ProtoProgression.QuestState.Available => QuestDialogUI.QuestDialogState.Offer,
                ProtoProgression.QuestState.Complete => QuestDialogUI.QuestDialogState.ReadyToTurnIn,
                _ => QuestDialogUI.QuestDialogState.InProgress
            };

            return new QuestDialogUI.QuestData
            {
                QuestId = quest.QuestId,
                Name = quest.Name,
                Description = quest.Description,
                State = state,
                Objectives = objectives,
                Rewards = rewards
            };
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
            Debug.Log($"[NPC] {data.NpcName}: {data.Dialogue} (role={data.Role}, items={data.ShopItems.Count}, quests={data.QuestIds.Count})");

            if (data.ShopItems.Count > 0)
            {
                // Vendor NPC — open shop UI
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
            else if (data.QuestIds.Count > 0)
            {
                // Quest NPC — the server will send QuestAvailable packets for each quest
                // For now, log the available quest IDs so we know they are coming
                Debug.Log($"[NPC] Quest giver {data.NpcName} has {data.QuestIds.Count} quests");

                // If only one quest, the QuestAvailable handler will open the dialog.
                // If multiple quests, we could show a selection list (future enhancement).
                // For now, show a notification that quest dialog is incoming.
                NotificationUI.Instance?.Show("NPC", $"{data.NpcName}: \"{data.Dialogue}\"", 0, 4f);
            }
            else
            {
                // Generic NPC — just show dialogue
                NotificationUI.Instance?.Show("NPC", $"{data.NpcName}: \"{data.Dialogue}\"", 0, 4f);
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

        private void HandleCreatureList(Orlo.Proto.Admin.AdminCreatureList list)
        {
            var browser = FindFirstObjectByType<CreatureBrowserUI>();
            if (browser != null)
                browser.SetCreatureList(list);
        }
    }
}
