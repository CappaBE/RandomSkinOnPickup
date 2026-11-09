# Random Skin On Pickup

An [Oxide/uMod](https://umod.org) plugin for Rust that gives items a random skin the first time they enter a player's inventory, and keeps that skin. Players can reset an item to the default skin with a chat command, once per item.

## Features

- **Random skin on inventory entry:** items without a skin get a random one when they enter a player's inventory, whether crafted, looted, picked up or received from a kit.
- **Skin is kept:** the skin is stored on the item itself, so it survives dropping, storing in boxes, moving between players and server restarts.
- **Never overwrites existing skins:** items that already have a skin are left alone.
- **Reset command:** `/defaultskin` sets the held item back to the default skin. This works once per item, and a reset item never gets randomized again.
- **Works with any skin source:** uses the skins the game provides, including workshop and store skins (optional), so new skins are picked up automatically.
- **Configurable:** optional permissions, item blacklist, stackable item handling and a custom command name.

## Installation

1. Make sure your server runs [Oxide/uMod](https://umod.org/games/rust).
2. Copy `RandomSkinOnPickup.cs` into `oxide/plugins/`.
3. Oxide compiles and loads the plugin automatically. A config file is created at `oxide/config/RandomSkinOnPickup.json`.

## Commands

| Command | Description |
|---|---|
| `/defaultskin` | Resets the skin of the item you are holding to the default skin. Works once per item. |

The command name can be changed in the config.

## Permissions

| Permission | Description |
|---|---|
| `randomskinonpickup.use` | Receive random skins. Only checked if `Require permission for random skins` is enabled. |
| `randomskinonpickup.reset` | Use the reset command. Only checked if `Require permission for reset command` is enabled. |

Both permission checks are disabled by default, so everyone gets random skins and can use the reset command.

Grant a permission with:

```
oxide.grant group default randomskinonpickup.use
oxide.grant user <name or steamid> randomskinonpickup.reset
```

## Configuration

`oxide/config/RandomSkinOnPickup.json`:

```json
{
  "Require permission for random skins (randomskinonpickup.use)": false,
  "Require permission for reset command (randomskinonpickup.reset)": false,
  "Reset command (chat)": "defaultskin",
  "Randomize stackable items (can break stacking)": false,
  "Include workshop / store skins": true,
  "Blacklisted item shortnames": [
    "rock",
    "torch"
  ]
}
```

| Option | Default | Description |
|---|---|---|
| Require permission for random skins | `false` | If `true`, only players with `randomskinonpickup.use` get random skins. |
| Require permission for reset command | `false` | If `true`, only players with `randomskinonpickup.reset` can use the reset command. |
| Reset command (chat) | `defaultskin` | Name of the chat command, without the slash. |
| Randomize stackable items | `false` | Items with different skins don't stack together, so this is off by default. |
| Include workshop / store skins | `true` | Include workshop and store skins in the random pool. |
| Blacklisted item shortnames | `rock`, `torch` | Items that never get a random skin. Use item shortnames. |

## How it works

- The plugin hooks `OnItemAddedToContainer`. If the container belongs to a (non-NPC) player and the item has no skin, a random skin is applied on the next tick.
- Possible skins are read per item from the game's skin directory and, optionally, the item's workshop skins. They are cached per item type.
- When `/defaultskin` is used, the item's unique ID is saved to `oxide/data/RandomSkinOnPickup.json`. Items in that list are skipped by the randomizer and can't be reset again.
- The data file is cleared automatically on a map wipe, since item IDs reset then.

## Notes and limitations

- The reset command works on any held item that has a skin, including skins a player chose themselves, not only skins assigned by this plugin.
- Splitting a stack creates a new item with its own ID, so it counts as a separate item for the one-reset limit.
- A player who sets an item to the default skin by other means (for example with another reskin plugin) can get a random skin again when the item re-enters an inventory, unless it was reset with `/defaultskin`.
- Facepunch changes the skin-related APIs from time to time. If the plugin fails to compile after a Rust update, please open an issue and include the error from the server console.
- This plugin may conflict with other plugins that modify crafting or item skins. If skins don't apply, check the load order and the server log.

## Contributing

Issues and pull requests are welcome. When reporting a bug, please include your Rust and Oxide versions, the plugin version, any other crafting or skin plugins you run, and the relevant console or log output.
