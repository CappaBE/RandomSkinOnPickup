using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Random Skin On Pickup", "Claude", "1.1.0")]
    [Description("Gives items a random skin the first time they enter a player's inventory and keeps it. Includes a one-use-per-item command to reset to the default skin.")]
    class RandomSkinOnPickup : RustPlugin
    {
        #region Fields

        private const string UsePermission = "randomskinonpickup.use";
        private const string ResetPermission = "randomskinonpickup.reset";

        private ConfigData config;

        // itemid -> list of possible skin ids (only non-empty lists are cached)
        private readonly Dictionary<int, ulong[]> skinCache = new Dictionary<int, ulong[]>();

        // Item UIDs that were reset to default. These never get randomized again
        // and can't be reset a second time.
        private HashSet<ulong> resetItems = new HashSet<ulong>();

        #endregion

        #region Config

        private class ConfigData
        {
            [JsonProperty("Require permission for random skins (randomskinonpickup.use)")]
            public bool RequirePermission = false;

            [JsonProperty("Require permission for reset command (randomskinonpickup.reset)")]
            public bool RequireResetPermission = false;

            [JsonProperty("Reset command (chat)")]
            public string ResetCommand = "defaultskin";

            [JsonProperty("Randomize stackable items (can break stacking)")]
            public bool RandomizeStackable = false;

            [JsonProperty("Include workshop / store skins")]
            public bool IncludeWorkshopSkins = true;

            [JsonProperty("Blacklisted item shortnames")]
            public List<string> Blacklist = new List<string>
            {
                "rock",
                "torch"
            };
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null) throw new Exception("Config is null");
            }
            catch
            {
                PrintWarning("Config invalid or missing, creating a default one.");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }

        #endregion

        #region Data

        private void LoadData()
        {
            try
            {
                resetItems = Interface.Oxide.DataFileSystem.ReadObject<HashSet<ulong>>(Name) ?? new HashSet<ulong>();
            }
            catch
            {
                resetItems = new HashSet<ulong>();
            }
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, resetItems);
        }

        #endregion

        #region Oxide hooks

        private void Init()
        {
            permission.RegisterPermission(UsePermission, this);
            permission.RegisterPermission(ResetPermission, this);
            LoadData();

            if (!string.IsNullOrEmpty(config.ResetCommand))
                cmd.AddChatCommand(config.ResetCommand, this, nameof(CmdResetSkin));
        }

        private void OnServerSave() => SaveData();

        private void Unload() => SaveData();

        private void OnNewSave()
        {
            // Item UIDs are reset on wipe, so old entries are meaningless.
            resetItems.Clear();
            SaveData();
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (container == null || item == null || item.info == null) return;

            var player = container.playerOwner;
            if (player == null || player.IsNpc) return;

            // Only items without a skin get one. Items that already have a skin keep it.
            if (item.skin != 0UL) return;

            // Items the player reset to default stay default.
            if (resetItems.Contains(item.uid.Value)) return;

            if (config.RequirePermission && !permission.UserHasPermission(player.UserIDString, UsePermission)) return;
            if (!config.RandomizeStackable && item.info.stackable > 1) return;
            if (config.Blacklist.Contains(item.info.shortname)) return;

            // Wait a tick so the item (and its held entity) is fully set up.
            NextTick(() => ApplyRandomSkin(player, item));
        }

        #endregion

        #region Commands

        private void CmdResetSkin(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            if (config.RequireResetPermission && !permission.UserHasPermission(player.UserIDString, ResetPermission))
            {
                player.ChatMessage("You don't have permission to use this command.");
                return;
            }

            var item = player.GetActiveItem();
            if (item == null)
            {
                player.ChatMessage("Hold the item you want to reset in your hands.");
                return;
            }

            if (item.skin == 0UL)
            {
                player.ChatMessage("This item already has the default skin.");
                return;
            }

            if (resetItems.Contains(item.uid.Value))
            {
                player.ChatMessage("You already reset this item once. It can't be reset again.");
                return;
            }

            resetItems.Add(item.uid.Value);

            item.skin = 0UL;
            item.MarkDirty();

            var held = item.GetHeldEntity();
            if (held != null)
            {
                held.skinID = 0UL;
                held.SendNetworkUpdate();
            }

            SaveData();
            player.ChatMessage("Skin reset to default. This item won't get a random skin again.");
        }

        #endregion

        #region Logic

        private void ApplyRandomSkin(BasePlayer player, Item item)
        {
            if (player == null || item == null || item.info == null) return;
            if (item.skin != 0UL) return;
            if (resetItems.Contains(item.uid.Value)) return;

            // Make sure the item is still in this player's inventory
            var parent = item.parent;
            if (parent == null || parent.playerOwner != player) return;

            var skins = GetSkins(item.info);
            if (skins.Length == 0) return;

            ulong skin = skins[UnityEngine.Random.Range(0, skins.Length)];

            item.skin = skin;
            item.MarkDirty();

            var held = item.GetHeldEntity();
            if (held != null)
            {
                held.skinID = skin;
                held.SendNetworkUpdate();
            }
        }

        private ulong[] GetSkins(ItemDefinition def)
        {
            ulong[] cached;
            if (skinCache.TryGetValue(def.itemid, out cached)) return cached;

            var set = new HashSet<ulong>();

            try
            {
                var directory = ItemSkinDirectory.ForItem(def);
                if (directory != null)
                {
                    foreach (var skin in directory)
                    {
                        if (skin.id != 0) set.Add((ulong)skin.id);
                    }
                }
            }
            catch (Exception ex)
            {
                PrintWarning($"Could not read skin directory for {def.shortname}: {ex.Message}");
            }

            if (config.IncludeWorkshopSkins)
            {
                try
                {
                    if (def.skins2 != null)
                    {
                        foreach (var skin in def.skins2)
                        {
                            if (skin != null && skin.WorkshopId != 0UL) set.Add(skin.WorkshopId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    PrintWarning($"Could not read workshop skins for {def.shortname}: {ex.Message}");
                }
            }

            var result = new ulong[set.Count];
            set.CopyTo(result);

            // Don't cache empty results, the skin lists may not have been loaded yet.
            if (result.Length > 0) skinCache[def.itemid] = result;

            return result;
        }

        #endregion
    }
}
