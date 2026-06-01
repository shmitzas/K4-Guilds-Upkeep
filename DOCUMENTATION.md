# K4-Guilds — Full Documentation

**Plugin ID:** `k4.guilds` | **Version:** 1.0.3 | **Framework:** SwiftlyS2 | **Author:** K4ryuu

---

## Table of Contents

1. [Overview](#1-overview)
2. [Architecture](#2-architecture)
3. [Installation & Requirements](#3-installation--requirements)
4. [Configuration](#4-configuration)
   - 4.1 [guild.json](#41-guildjson)
   - 4.2 [upgrades.json](#42-upgradesjson)
   - 4.3 [commands.json](#43-commandsjson)
5. [Database Schema](#5-database-schema)
6. [Rank System](#6-rank-system)
7. [Command Reference](#7-command-reference)
8. [Bank System](#8-bank-system)
9. [Upgrade System](#9-upgrade-system)
10. [Perk System](#10-perk-system)
11. [Stats System](#11-stats-system)
12. [Invite System](#12-invite-system)
13. [Scoreboard Tag System](#13-scoreboard-tag-system)
14. [Caching](#14-caching)
15. [Developer API](#15-developer-api)
16. [Plugin Events](#16-plugin-events)
17. [Writing Perk Plugins](#17-writing-perk-plugins)
18. [Translations](#18-translations)
19. [Validation Rules Reference](#19-validation-rules-reference)

---

## 1. Overview

K4-Guilds is a full-featured guild system for Counter-Strike 2 servers running SwiftlyS2. Players can form persistent guilds, manage members through a rank hierarchy, operate a shared bank, purchase upgrades and perks, communicate via guild chat, and compete on a kills-based leaderboard. Plugin developers can integrate via the `IK4GuildsApi` shared interface.

**Feature summary:**
- Guild creation, disbanding, and renaming
- Member invites, acceptance, and leaving/kicking
- Fully configurable rank hierarchy with permission flags
- Shared guild bank — deposit, withdraw, bank interest
- Four purchasable upgrade types (slots, bank capacity, XP boost, bank interest rate)
- Extensible perk system — `Purchasable` (on/off) and `Upgradeable` (leveled) perks
- Automatic kill/death/headshot/assist stat tracking per guild
- Top-guilds leaderboard sorted by kills
- Real-time scoreboard clan tag injection
- In-memory invite system with expiry
- Full set of events for external plugin integration
- Hot-reloadable JSON config
- MySQL, PostgreSQL, and SQLite support

---

## 2. Architecture

### Class Structure

The plugin is a partial class `Plugin` extending `BasePlugin(ISwiftlyCore)`. Business logic is split across partial files and services:

| Component | Responsibility |
|---|---|
| `Plugin.cs` | Load/unload, event hooks, timers, config wiring |
| `DatabaseService` | All DB I/O via Dommel ORM + raw Dapper queries |
| `GuildService` | Guild & member CRUD, rank logic, 5-minute memory cache |
| `InviteService` | In-memory invite store with 60-second TTL |
| `UpgradeService` | Upgrade level queries, cost calculation, purchase flow |
| `PerkService` | Perk registry, buy/upgrade/toggle flows |
| `GuildCommands` | SwiftlyS2 command registration and dispatch |

### Startup Sequence

```
Load()
  ├── InitializeConfigs()         — bind guild.json, upgrades.json, commands.json via IOptionsMonitor
  ├── InitializeDatabase()        — create DatabaseService, run FluentMigrator migrations
  ├── InitializeServices()        — create all service instances
  ├── RegisterEvents()            — hook EventPlayerActivate, EventPlayerDeath, OnClientDisconnected
  ├── RegisterCommands()          — register !guild and !gc with SwiftlyS2 command host
  ├── StartInterestTimer()        — fires ProcessBankInterestAsync every N minutes (if enabled)
  └── StartScoreboardRefreshTimer() — fires every N seconds to refresh clan tags (if enabled)
```

### Game Events Hooked

| Event | Behavior |
|---|---|
| `EventPlayerActivate` | Async guild lookup → set `controller.Clan` = guild tag or `""` |
| `EventPlayerDeath` | Parallel guild lookups for victim/attacker/assister, coalesced DB upserts |
| `OnClientDisconnected` | Evicts player's entry from guild cache |

---

## 3. Installation & Requirements

**Requirements:**
- SwiftlyS2 server framework
- A supported database: MySQL 5.7+, MariaDB 10.2+, PostgreSQL 10+, or SQLite 3.35+
- (Optional) An Economy plugin exposing `Economy.API.v1` for creation costs, perk costs, and bank deposits/withdrawals

**Installation:**
1. Drop `K4-Guilds.dll` into your SwiftlyS2 plugins folder.
2. Drop `K4-GuildsApi.dll` into your plugins `../exports/` folder (for other plugins to reference).
3. Start the server — config files are generated automatically on first run in the plugin's `configs/` folder.
4. Edit `guild.json`, `upgrades.json`, and `commands.json` as needed.
5. All database tables are created automatically via migrations.

**Perk plugins** (e.g. `K4-Guilds-Health`, `K4-Guilds-Speed`) are installed the same way — they depend on `K4-GuildsApi.dll` being present.

---

## 4. Configuration

All three config files support **hot-reload** — changes take effect at the next read without restarting the server.

### 4.1 `guild.json`

**Section name:** `K4GuildsGuild`

| Property | Type | Default | Description |
|---|---|---|---|
| `DatabaseConnection` | `string` | `"default"` | Connection name from SwiftlyS2's `database.jsonc` |
| `WalletKind` | `string` | `"credits"` | Economy wallet used for all guild transactions (creation, perks, rename) |
| `DefaultSlots` | `int` | `5` | Member slots on guild creation |
| `MaxSlots` | `int` | `20` | Absolute hard cap on member slots (overrides upgrade math) |
| `CreationCost` | `long` | `5000` | Economy cost deducted from the creator's personal wallet on `!guild create`. Set to `0` for free. |
| `MinNameLength` | `int` | `3` | Minimum guild name length in characters |
| `MaxNameLength` | `int` | `32` | Maximum guild name length in characters |
| `MaxTagLength` | `int` | `4` | Maximum guild tag length in **Unicode text elements** (emoji-aware) |
| `ShowTagOnScoreboard` | `bool` | `true` | Injects guild tag into CS2 scoreboard `Clan` field |
| `ScoreboardRefreshIntervalSeconds` | `int` | `60` | Timer interval for refreshing all scoreboard tags. Set to `0` to disable the timer (event-only updates). |
| `GuildRanks` | `List<GuildRankSettings>` | _(see §6)_ | Global rank definitions |

#### `GuildRankSettings`

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Display name (supports translation keys) |
| `Priority` | `int` | Higher = more senior rank. Used for promotion/demotion ordering. |
| `IsDefault` | `bool` | Rank assigned when a member joins via accept. Exactly one rank should be default. |
| `Permissions` | `int` | Bitfield of `GuildPermission` flags. `-1` = all permissions. |

**Default ranks (shipped in default config):**

| Name | Priority | IsDefault | Permissions |
|---|---|---|---|
| Leader | 100 | false | -1 (All) |
| Officer | 50 | false | Chat + Invite + Kick + Promote + Demote + Withdraw + ManagePerks |
| Member | 0 | true | Chat only |

---

### 4.2 `upgrades.json`

**Section name:** `K4GuildsUpgrades`

The config contains four upgrade blocks. Each block shares a common structure with type-specific extras.

**Common fields (all upgrade types):**

| Property | Type | Default | Description |
|---|---|---|---|
| `Enabled` | `bool` | `true` | Whether this upgrade can be purchased |
| `MaxLevel` | `int` | varies | Maximum purchasable level |
| `BaseCost` | `long` | varies | Cost in guild bank currency to buy level 1 |
| `CostMultiplier` | `double` | varies | Exponential cost scaling: `Cost(currentLevel) = BaseCost × CostMultiplier^currentLevel` |

**Per-type extras:**

| Block | Extra Property | Default | Effect |
|---|---|---|---|
| `SlotUpgrade` | `SlotsPerLevel` | `2` | Each level adds this many member slots |
| `BankCapacity` | `BaseCapacity` | `10000` | Starting bank capacity before any upgrades |
| `BankCapacity` | `CapacityPerLevel` | `5000` | Extra capacity added per level |
| `XPBoost` | `BoostPerLevel` | `5` | XP boost percentage added per level (consumed externally) |
| `BankInterest` | `InterestPerLevel` | `1.0` | Interest rate % per level applied to current balance each tick |
| `BankInterest` | `IntervalMinutes` | `60` | How often interest ticks; balance capped at `MaxBankCapacity` |

**Default cost table:**

| Upgrade | MaxLevel | BaseCost | CostMultiplier |
|---|---|---|---|
| SlotUpgrade | 5 | 1000 | 1.5 |
| BankCapacity | 10 | 500 | 1.3 |
| XPBoost | 5 | 2000 | 2.0 |
| BankInterest | 5 | 3000 | 2.5 |

---

### 4.3 `commands.json`

**Section name:** `K4GuildsCommands`

All subcommands share this structure (`SubCommandSettings`):

| Property | Type | Description |
|---|---|---|
| `Enabled` | `bool` | If `false`, the subcommand is disabled and not registered in the command map |
| `Name` | `string` | Primary subcommand keyword |
| `Aliases` | `List<string>` | Additional accepted keywords |
| `Permission` | `string` | SwiftlyS2 permission flag required; `""` = no restriction |

**Main command (`MainCommandSettings`):**

| Property | Default |
|---|---|
| `Command` | `"guild"` |
| `Aliases` | `["g", "guilds", "clan", "clans"]` |
| `Permission` | `""` |

**Guild chat standalone (`GuildChatCommandSettings`):**

| Property | Default |
|---|---|
| `Enabled` | `true` |
| `Command` | `"gc"` |
| `Aliases` | `["guildchat"]` |
| `Permission` | `""` |

**Rename command (`RenameCommandSettings`):** extends `SubCommandSettings` with:

| Extra Property | Default | Description |
|---|---|---|
| `Cost` | `1000` | Personal wallet cost charged to the leader for a rename |

**Default subcommand names and aliases:**

| Subcommand | Name | Aliases |
|---|---|---|
| Create | `create` | `c` |
| Disband | `disband` | `delete` |
| Invite | `invite` | `inv`, `i` |
| Accept | `accept` | `a` |
| Decline | `decline` | `deny`, `d` |
| Leave | `leave` | `quit`, `l` |
| Kick | `kick` | `k` |
| Promote | `promote` | `prom` |
| Demote | `demote` | `dem` |
| Info | `info` | `status` |
| Members | `members` | `list`, `m` |
| Deposit | `deposit` | `dep` |
| Withdraw | `withdraw` | `wd` |
| Upgrade | `upgrade` | `up` |
| Chat | `chat` | `gc`, `c` |
| Perks | `perks` | `perk`, `p` |
| Rename | `rename` | `rn` |
| Top | `top` | `leaderboard`, `lb` |
| Help | `help` | `h`, `?` |

---

## 5. Database Schema

Tables are created and migrated automatically on plugin load using FluentMigrator. All cascade deletes propagate from `k4_guilds.id`.

### `k4_guilds`

| Column | Type | Constraints |
|---|---|---|
| `id` | INT | PK, auto-increment |
| `name` | VARCHAR(128) | NOT NULL, UNIQUE |
| `tag` | VARCHAR(32) | NOT NULL |
| `leader_steam_id` | BIGINT UNSIGNED | NOT NULL, indexed |
| `bank_balance` | BIGINT | NOT NULL, DEFAULT 0 |
| `created_at` | DATETIME | NOT NULL |
| `updated_at` | DATETIME | NOT NULL |

### `k4_guild_members`

| Column | Type | Constraints |
|---|---|---|
| `id` | INT | PK, auto-increment |
| `guild_id` | INT | FK → `k4_guilds.id` ON DELETE CASCADE, indexed |
| `steam_id` | BIGINT UNSIGNED | NOT NULL, UNIQUE, indexed |
| `player_name` | VARCHAR(128) | NOT NULL |
| `rank_priority` | INT | NOT NULL, DEFAULT 0 |
| `joined_at` | DATETIME | NOT NULL |
| `last_seen` | DATETIME | NOT NULL |

### `k4_guild_upgrades`

| Column | Type | Constraints |
|---|---|---|
| `id` | INT | PK, auto-increment |
| `guild_id` | INT | FK → `k4_guilds.id` ON DELETE CASCADE, indexed |
| `upgrade_type` | INT | NOT NULL (0=Slots, 1=BankCapacity, 2=XPBoost, 3=BankInterest) |
| `level` | INT | NOT NULL, DEFAULT 0 |
| `purchased_at` | DATETIME | NOT NULL |

UNIQUE constraint on `(guild_id, upgrade_type)`.

### `k4_guild_perks`

| Column | Type | Constraints |
|---|---|---|
| `id` | INT | PK, auto-increment |
| `guild_id` | INT | FK → `k4_guilds.id` ON DELETE CASCADE, indexed |
| `perk_id` | VARCHAR(128) | NOT NULL |
| `level` | INT | NOT NULL, DEFAULT 1 |
| `enabled` | BOOL | NOT NULL, DEFAULT true |
| `purchased_at` | DATETIME | NOT NULL |
| `updated_at` | DATETIME | NOT NULL |

UNIQUE constraint on `(guild_id, perk_id)`.

### `k4_guild_stats`

| Column | Type | Constraints |
|---|---|---|
| `guild_id` | INT | PK, FK → `k4_guilds.id` ON DELETE CASCADE |
| `kills` | INT | NOT NULL, DEFAULT 0 |
| `deaths` | INT | NOT NULL, DEFAULT 0 |
| `headshots` | INT | NOT NULL, DEFAULT 0 |
| `assists` | INT | NOT NULL, DEFAULT 0 |

---

## 6. Rank System

Ranks are **global** — defined once in `guild.json` and shared across all guilds. Each guild member stores their current `rank_priority`, which maps back to the matching `GuildRankSettings` entry.

### `GuildPermission` Flags

| Flag | Value | Allows |
|---|---|---|
| `None` | `0` | Nothing |
| `Chat` | `1` | Use guild chat |
| `Invite` | `2` | Invite players to the guild |
| `Kick` | `4` | Kick members |
| `Promote` | `8` | Promote members |
| `Demote` | `16` | Demote members |
| `Withdraw` | `32` | Withdraw from the guild bank |
| `Upgrade` | `64` | Purchase guild upgrades (paid from guild bank) |
| `ManagePerks` | `128` | Buy, upgrade, and toggle perks (paid from personal wallet) |
| `All` | `-1` | All of the above |

Permissions are stored as a bitfield integer in the rank config, e.g. `Chat + Invite + Kick = 1 + 2 + 4 = 7`.

### Hierarchy Rules

- **Promote**: can only promote to a rank with `Priority > target.Priority` AND `Priority < promoter.Priority`. You cannot promote someone to your own or a higher rank.
- **Demote**: can only demote to a rank with `Priority < target.Priority`. The demoter must outrank the target.
- **Kick**: target's priority must be strictly less than the kicker's priority.
- The **leader** (identified by `guild.LeaderSteamId`) can never be kicked.
- The leader cannot use `!guild leave` — they must `!guild disband` or transfer leadership is not implemented (disband only).

### Special Rank Resolution

`GetLeaderRank()` first searches for a rank with `Permissions == -1`. If none exists, it returns the highest-priority rank. Used internally to determine the rank assigned on guild creation.

---

## 7. Command Reference

All commands are subcommands of `!guild` (configurable). Arguments in `<>` are required; `[...]` are optional.

Commands that **do not require the player to be in a guild:** `create`, `accept`, `decline`, `help`, `top`.  
All other commands require the player to be a member of a guild.

---

### `!guild create <name> <tag>`

Creates a new guild.

**Arguments:** `name` (all tokens up to the last), `tag` (last token)  
**Validation:**
- Player must not already be in a guild
- Name must pass all name validation rules (see §19)
- Tag must pass all tag validation rules (see §19)
- Name must not already exist in the database
- If `CreationCost > 0` and Economy API is available: player must have sufficient funds (deducted on success)

**On success:** Guild row created, creator added as a member with the leader rank, `GuildCreated` event fired, scoreboard tag updated.

---

### `!guild disband`

Permanently deletes the guild and all associated data (members, upgrades, perks, stats — cascade deleted by FK).

**Restriction:** Player must be the guild leader (`LeaderSteamId`).  
**On success:** All pending invites for this guild are removed, `GuildDisbanded` event fired, all guild/member cache entries invalidated.

---

### `!guild invite <player>`

Invites an online player to join the guild.

**Arguments:** Player name (joined if multiple tokens).  
**Target resolution:** Online players only via SwiftlyS2 `FindTargettedPlayers` — must match exactly one player.  
**Validation:**
- Sender must have `GuildPermission.Invite`
- Target must not already be in any guild
- Target must not already have a pending invite
- Guild must not be full (`Members.Count < CalculateMaxSlots()`)

**On success:** Invite created with 60-second expiry, target receives two chat messages (invite notification + usage hint), `InviteSent` event fired.

---

### `!guild accept`

Accepts a pending guild invite.

**Validation:**
- Player must have a pending, non-expired invite
- The invited guild must still exist
- Player must not already be in a guild
- Guild must not be full at the time of acceptance
- A default rank must be configured

**On success:** Member added at the default rank, invite cleared, caches invalidated, scoreboard tag updated, `MemberJoined` event fired.

---

### `!guild decline`

Declines a pending guild invite.

**Validation:** Player must have a pending invite.  
**On success:** Invite record removed.

---

### `!guild leave`

Leave the current guild.

**Restriction:** The guild leader cannot leave — they must use `!guild disband` instead.

**On success:** Member removed from DB, caches invalidated, scoreboard tag cleared, `MemberLeft` event fired.

---

### `!guild kick <player>`

Kicks a member from the guild.

**Arguments:** Player name or SteamID.  
**Target resolution:** Tries online match → SteamID parse → offline name substring match. Must resolve to exactly one guild member.  
**Validation:**
- Sender must have `GuildPermission.Kick`
- Target must be in the guild
- Cannot kick the guild leader
- Target's rank priority must be strictly lower than the kicker's

**On success:** Member removed from DB, caches invalidated, scoreboard tag cleared, `MemberKicked` event fired.

---

### `!guild promote <player>`

Promotes a guild member to the next rank.

**Arguments:** Player name or SteamID.  
**Validation:**
- Sender must have `GuildPermission.Promote`
- Target must be in the guild
- A rank must exist with `Priority > target.Priority` AND `Priority < promoter.Priority`

**On success:** `rank_priority` updated in DB, in-memory guild state updated, `MemberPromoted` event fired.

---

### `!guild demote <player>`

Demotes a guild member to the previous rank.

**Arguments:** Player name or SteamID.  
**Validation:**
- Sender must have `GuildPermission.Demote`
- Target's rank priority must be strictly less than the demoter's
- A rank must exist with `Priority < target.Priority`

**On success:** `rank_priority` updated in DB, in-memory guild state updated, `MemberDemoted` event fired.

---

### `!guild info`

Displays a summary of the current guild.

**Output:**
```
=== GuildName [TAG]
> Members: X/Y
> Bank: X/Y
> XP Boost: +X%
===
```
All figures computed from the cached guild state — no additional DB queries.

---

### `!guild members`

Lists all guild members with their rank names.

**Output:**
```
=== Guild Members
> PlayerName (RankName)
> ...
===
```

---

### `!guild deposit <amount>`

Deposits from the sender's personal wallet to the guild bank.

**Arguments:** Positive integer amount.  
**Validation:**
- Any guild member can deposit (no permission required)
- Economy API must be available
- Player must have sufficient personal balance
- Deposit must not exceed remaining bank capacity

**On success:** Personal wallet deducted, `bank_balance` atomically incremented, guild cache invalidated.

---

### `!guild withdraw <amount>`

Withdraws from the guild bank to the sender's personal wallet.

**Arguments:** Positive integer amount.  
**Validation:**
- Sender must have `GuildPermission.Withdraw`
- Economy API must be available
- Guild bank must have sufficient balance

**On success:** `bank_balance` atomically decremented, personal wallet credited, guild cache invalidated.

---

### `!guild upgrade [type]`

With no argument: lists all upgrade types with current level, max level, and cost of next level.

With argument: purchases the next level of the specified upgrade type.

**Type arguments (case-insensitive):**

| Argument | Upgrade |
|---|---|
| `slots` or `slot` | Member slot expansion |
| `bank` | Bank capacity expansion |
| `xp` | XP boost % |
| `interest` | Bank interest rate |

**Validation (purchase):**
- Sender must have `GuildPermission.Upgrade`
- Upgrade type must be enabled in config
- Current level must be less than `MaxLevel`
- Guild bank must have sufficient funds (deducted atomically)

**On success:** Level incremented in `k4_guild_upgrades`, guild cache invalidated.

---

### `!guild chat <message>` / `!gc <message>`

Sends a message to all online guild members who have `GuildPermission.Chat`.

**Validation:**
- Message must not be empty
- Sender must have `GuildPermission.Chat`

**Format:** `[TAG] SenderName: message` — sent using each recipient's own localizer.

---

### `!guild perks [subcommand]`

Manages guild perks.

**With no argument:** Displays all registered perks with status, level, and cost information.

| Sub-argument | Aliases | Effect |
|---|---|---|
| `buy <id>` | `upgrade`, `b`, `u` | Purchase or upgrade a perk by ID or name |
| `toggle <id>` | `t` | Toggle a `Purchasable` perk on or off |
| `info <id>` | `i` | Show detailed info for a specific perk |

**Perk matching:** by `Id` or `Name`, case-insensitive.

**Buy validation:**
- Sender must have `GuildPermission.ManagePerks`
- Perk must be registered
- `Purchasable` perks: must not already be at level 1
- `Upgradeable` perks: current level must be below `MaxLevel`
- Sender must have sufficient personal wallet funds

**Toggle validation:**
- Sender must have `GuildPermission.ManagePerks`
- Perk must be `Purchasable` type
- Perk must already be purchased (level > 0)

---

### `!guild top [count]`

Lists the top guilds sorted by total kills in descending order.

**Arguments:** Optional integer count (1–25). Default = 5.  
**No guild required.**

**Output:**
```
=== Top Guilds by Kills
> #1 GuildName [TAG] - K: 500 D: 200 HS: 150 A: 75
> #2 ...
===
```

---

### `!guild rename <new name>`

Renames the guild.

**Arguments:** New name (all remaining args joined as one string).  
**Restriction:** Player must be the guild leader.  
**Validation:**
- New name must pass all name validation rules (see §19)
- New name must be unique (a different guild does not have it)
- If `RenameCommandSettings.Cost > 0` and Economy API is available: cost charged from the leader's personal wallet

**On success:** `name` updated in DB, guild cache invalidated.

---

### `!guild help`

Displays all available commands. Also shown automatically when an unrecognized subcommand is entered.

---

## 8. Bank System

Every guild has a single `bank_balance` (BIGINT) stored in `k4_guilds`.

### Capacity

```
MaxBankCapacity = BankCapacity.BaseCapacity + (Level × BankCapacity.CapacityPerLevel)
```

Default at level 0: `10,000`. At max level (10): `10,000 + 10 × 5,000 = 60,000`.

### Atomic Operations

All balance modifications use database-level atomicity to prevent race conditions from concurrent deposits or withdrawals:
- **Deposit / Interest:** `INSERT ... ON DUPLICATE KEY UPDATE` style upserts or locked SELECTs within a transaction.
- **Withdraw / Upgrade purchase:** atomic subtract with balance floor check inside a transaction. If the guild has insufficient funds, the transaction rolls back and the operation fails cleanly.

### Bank Interest Timer

When `BankInterest.Enabled = true`, a timer fires every `IntervalMinutes` minutes:

```
Interest = floor(bank_balance × (interestLevel × InterestPerLevel%) / 100)
NewBalance = min(bank_balance + Interest, MaxBankCapacity)
```

If `interest <= 0` or `bank_balance <= 0`, that guild is skipped. Balance is always capped at max capacity.

### Who Pays For What

| Action | Paid From |
|---|---|
| Guild creation | Creator's personal wallet |
| Guild rename | Leader's personal wallet |
| Bank deposit | Member's personal wallet → guild bank |
| Bank withdrawal | Guild bank → member's personal wallet |
| Upgrade purchase | Guild bank |
| Perk purchase / upgrade | Buyer's personal wallet |

---

## 9. Upgrade System

### Upgrade Types

| `GuildUpgradeType` | Int | What It Affects |
|---|---|---|
| `Slots` | `0` | `MaxSlots = DefaultSlots + level × SlotsPerLevel` (capped by `config.MaxSlots`) |
| `BankCapacity` | `1` | `MaxBank = BaseCapacity + level × CapacityPerLevel` |
| `XPBoost` | `2` | `XPBoost% = level × BoostPerLevel` — informational, consumed by external plugins |
| `BankInterest` | `3` | `InterestRate% = level × InterestPerLevel` applied on each interest tick |

### Cost Formula

$$\text{Cost}(\text{currentLevel}) = \text{BaseCost} \times \text{CostMultiplier}^{\text{currentLevel}}$$

Returns `-1` if `currentLevel >= MaxLevel` (already maxed — cannot be purchased).

---

## 10. Perk System

The perk system is designed for extensibility. The base plugin provides the infrastructure; actual perks are implemented in separate plugin packages or by server owners.

### Perk Types

| Type | Behavior |
|---|---|
| `Purchasable` | One-time buy (level becomes 1). Can be toggled on/off at any time by an authorized member. |
| `Upgradeable` | Multiple levels from 1 to `MaxLevel`. Stronger effect at higher levels. Cannot be toggled. |

### `IGuildPerk` Interface

Implement this interface to create a perk:

```csharp
public interface IGuildPerk
{
    string Id { get; }                        // Unique identifier, e.g. "health_boost"
    string Name { get; }                      // Display name, e.g. "Health Boost"
    string Description { get; }              // Base description
    GuildPerkType PerkType { get; }           // Purchasable or Upgradeable
    long BaseCost { get; }                    // Cost for level 1
    int MaxLevel { get; }                     // Ignored for Purchasable perks
    double CostMultiplier { get; }           // Per-level cost scaling

    long GetCostForLevel(int level);          // Default: BaseCost × CostMultiplier^(level-1)
    string GetLevelDescription(int level);   // Per-level description text

    void OnMemberSpawn(IPerkContext context);
    void OnMemberDeath(IPerkContext context);
    void OnMemberKill(IPerkContext context, IPlayer victim);
    void OnRoundStart(IPerkContext context);
    void OnRoundEnd(IPerkContext context, int winnerTeam);
}
```

### `IPerkContext`

| Property | Type | Description |
|---|---|---|
| `Guild` | `IGuild` | The guild that owns the perk |
| `Member` | `IGuildMember` | The member whose event was triggered |
| `Player` | `IPlayer` | The in-game player |
| `PerkLevel` | `int` | Current perk level for this guild |

### Perk Purchase / Upgrade Flow

1. Perk must be registered in the `PerkService` registry.
2. For `Purchasable`: blocked if already at level 1.
3. For `Upgradeable`: blocked if already at `MaxLevel`.
4. Cost calculated via `GetCostForLevel(nextLevel)`.
5. Cost deducted from the buyer's **personal wallet** (not the guild bank).
6. `k4_guild_perks` row upserted atomically.

### Perks List Display

- `●` icon = `Purchasable` perk.
- `▲` icon = `Upgradeable` perk.
- Status shown as `[ON]` / `[OFF]` for `Purchasable` perks.
- `[MAX]` shown for maxed upgradeable perks.

---

## 11. Stats System

Guild stats are tracked automatically without any manual configuration.

### What Is Tracked

Every `player_death` game event updates `k4_guild_stats`:

| Event Role | Guild Gets |
|---|---|
| Attacker (killer) | +1 kill, +1 headshot (if headshot) |
| Victim (died) | +1 death |
| Assister | +1 assist |
| Suicide attacker | No kill credit — suicides are excluded |

### Performance Design

All three guild lookups (victim, attacker, assister) run in parallel via `Task.WhenAll`. If two of the roles belong to the **same guild** (e.g. victim and attacker are both in the same guild), their stat deltas are **coalesced** into a single atomic upsert instead of two separate calls.

Stats rows are created on first record (upsert — `ON DUPLICATE KEY UPDATE` / `ON CONFLICT DO UPDATE`). A guild with zero stats simply has no row in `k4_guild_stats`.

### `!guild top`

Queries `k4_guild_stats JOIN k4_guilds ORDER BY kills DESC LIMIT N` — no full table scan.

---

## 12. Invite System

Invites are stored entirely in-memory by `InviteService` — they do not survive server restarts.

| Property | Value |
|---|---|
| Expiry | 60 seconds from creation |
| Max pending per player | 1 |
| Persistence | In-memory only |

Invite data stored per invitee SteamID:
- `GuildId` — which guild the invite is for
- `InviterSteamId` — who sent it
- `ExpiresAt` — absolute expiry timestamp

Expired invites are lazily purged on every read or write. When a guild is disbanded, all its pending invites are explicitly removed.

---

## 13. Scoreboard Tag System

When `ShowTagOnScoreboard = true`, the plugin sets the CS2 scoreboard `Clan` field to the player's guild tag.

### Update Triggers

| Trigger | Affected Players |
|---|---|
| `EventPlayerActivate` | The connecting player |
| `!guild accept` (member joined) | The new member |
| `!guild leave` / kick (member left) | The removed member (tag cleared) |
| Guild disbanded | All former members (tags cleared) |
| `ScoreboardRefreshIntervalSeconds` timer | All online players |
| `RefreshGuildScoreboardTags(guildId)` | All online members of that guild |

All tag updates are applied on `NextWorldUpdate` to avoid threading issues with the game engine.

---

## 14. Caching

`GuildService` maintains two `ConcurrentDictionary`-based caches with a **5-minute TTL**.

| Cache | Key | Stores |
|---|---|---|
| `_playerGuildCache` | `ulong` SteamID | `Guild?` — `null` explicitly cached for non-members to avoid repeated DB lookups |
| `_guildCache` | `int` GuildId | `Guild` including all relations |

### Invalidation

| Method | What It Clears |
|---|---|
| `InvalidatePlayerCache(steamId)` | That player's cache entry |
| `InvalidateGuildCache(guildId)` | The guild entry AND all player entries whose cached guild ID matches |
| `InvalidateMemberCache(steamId, guildId)` | Both of the above |

Invalidation is called after every mutation: create, disband, join, leave, kick, rename, upgrade, perk change.

Client disconnect also calls `InvalidatePlayerCache` to free memory.

---

## 15. Developer API

Other plugins can consume the K4-Guilds API using the SwiftlyS2 shared interface system.

### Getting the API

```csharp
public override void UseSharedInterface(IInterfaceManager interfaceManager)
{
    var api = interfaceManager.GetSharedInterface<IK4GuildsApi>("K4Guilds.Api.v1");
    // use api...
}
```

`K4-GuildsApi.dll` must be in the exports folder for compilation. At runtime it is provided by the K4-Guilds plugin.

---

### Guild Queries

```csharp
Task<IGuild?>                    GetGuildAsync(int guildId)
Task<IGuild?>                    GetGuildByNameAsync(string name)
Task<IGuild?>                    GetPlayerGuildAsync(ulong steamId)
Task<IGuildMember?>              GetGuildMemberAsync(ulong steamId)
Task<bool>                       IsInGuildAsync(ulong steamId)
Task<bool>                       HasPermissionAsync(ulong steamId, GuildPermission permission)
Task<IReadOnlyList<IGuild>>      GetAllGuildsAsync(int page = 0, int pageSize = 20)
```

### Bank API

```csharp
Task<long>  GetBankBalanceAsync(int guildId)
Task<bool>  AddToBankAsync(int guildId, long amount, string reason)
Task<bool>  RemoveFromBankAsync(int guildId, long amount, string reason)
```

### Perk API

```csharp
void                             RegisterPerk(IGuildPerk perk)
void                             UnregisterPerk(string perkId)
IReadOnlyList<IGuildPerk>        GetRegisteredPerks()

Task<int>                        GetPerkLevelAsync(int guildId, string perkId)
Task<bool>                       IsPerkActiveAsync(int guildId, string perkId)
Task<(bool, int, string)>        PurchaseOrUpgradePerkAsync(int guildId, string perkId, ulong buyerSteamId)
Task<bool>                       TogglePerkAsync(int guildId, string perkId)
```

### Upgrade API

```csharp
Task<int>   GetUpgradeLevelAsync(int guildId, GuildUpgradeType upgradeType)
Task<long>  GetUpgradeCostAsync(int guildId, GuildUpgradeType upgradeType)
```

### IGuild Properties

| Property | Type |
|---|---|
| `Id` | `int` |
| `Name` | `string` |
| `Tag` | `string` |
| `LeaderSteamId` | `ulong` |
| `MemberCount` | `int` |
| `MaxSlots` | `int` |
| `BankBalance` | `long` |
| `MaxBankCapacity` | `long` |
| `XPBoostPercent` | `int` |
| `CreatedAt` | `DateTime` |
| `Members` | `IReadOnlyList<IGuildMember>` |
| `Ranks` | `IReadOnlyList<IGuildRank>` |

### IGuildMember Properties

| Property | Type |
|---|---|
| `SteamId` | `ulong` |
| `PlayerName` | `string` |
| `RankId` | `int` |
| `RankName` | `string` |
| `Permissions` | `GuildPermission` |
| `JoinedAt` | `DateTime` |

### IGuildRank Properties

| Property | Type |
|---|---|
| `Id` | `int` |
| `Name` | `string` |
| `Permissions` | `GuildPermission` |
| `Priority` | `int` |
| `IsDefault` | `bool` |

---

## 16. Plugin Events

These events are raised on the `Plugin` instance and exposed through `IK4GuildsApi`:

| Event | Args Type | When Fired |
|---|---|---|
| `GuildCreated` | `GuildEventArgs` | After `!guild create` succeeds |
| `GuildDisbanded` | `GuildEventArgs` | After `!guild disband` succeeds (after invites purged) |
| `MemberJoined` | `GuildMemberEventArgs` | After `!guild accept` succeeds |
| `MemberLeft` | `GuildMemberEventArgs` | After `!guild leave` succeeds |
| `MemberKicked` | `GuildMemberEventArgs` | After `!guild kick` succeeds |
| `MemberPromoted` | `GuildMemberEventArgs` | After `!guild promote` succeeds |
| `MemberDemoted` | `GuildMemberEventArgs` | After `!guild demote` succeeds |
| `InviteSent` | `GuildInviteEventArgs` | After `!guild invite` succeeds |

**`GuildEventArgs`:**
- `Guild` — `IGuild` wrapper

**`GuildMemberEventArgs`:**
- `Guild` — `IGuild` wrapper
- `Member` — `IGuildMember` wrapper
- `Player` — `IPlayer?` (null for offline operations)

**`GuildInviteEventArgs`:**
- `Guild` — `IGuild` wrapper
- `InviterSteamId` — `ulong`
- `InviteeSteamId` — `ulong`

---

## 17. Writing Perk Plugins

This section describes how to create a standalone plugin that adds a perk (e.g. bonus health on spawn).

### Project Setup

1. Create a new class library project.
2. Reference `K4-GuildsApi.dll` from the exports folder.
3. Reference `SwiftlyS2.Shared.dll`.

### Implementing IGuildPerk

```csharp
public sealed class HealthBoostPerk : IGuildPerk
{
    public string Id => "health_boost";
    public string Name => "Health Boost";
    public string Description => "Guild members spawn with extra HP.";
    public GuildPerkType PerkType => GuildPerkType.Upgradeable;
    public long BaseCost => 2000;
    public int MaxLevel => 3;
    public double CostMultiplier => 1.5;

    public long GetCostForLevel(int level) =>
        level == 1 ? BaseCost : (long)(BaseCost * Math.Pow(CostMultiplier, level - 1));

    public string GetLevelDescription(int level) => $"+{level * 25} HP on spawn";

    public void OnMemberSpawn(IPerkContext ctx)
    {
        var bonus = ctx.PerkLevel * 25;
        ctx.Player.Controller.PlayerPawn.Health += bonus;
    }

    public void OnMemberDeath(IPerkContext ctx) { }
    public void OnMemberKill(IPerkContext ctx, IPlayer victim) { }
    public void OnRoundStart(IPerkContext ctx) { }
    public void OnRoundEnd(IPerkContext ctx, int winnerTeam) { }
}
```

### Registering the Perk

```csharp
[PluginMetadata(Id = "my.health_boost", ...)]
public sealed class Plugin(ISwiftlyCore core) : BasePlugin(core)
{
    private IK4GuildsApi? _guildsApi;

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        if (!interfaceManager.HasSharedInterface("K4Guilds.Api.v1")) return;
        _guildsApi = interfaceManager.GetSharedInterface<IK4GuildsApi>("K4Guilds.Api.v1");
        _guildsApi.RegisterPerk(new HealthBoostPerk());
    }

    public override void Unload()
    {
        _guildsApi?.UnregisterPerk("health_boost");
    }
}
```

The perk is immediately available to guilds once registered. `PerkService` calls the appropriate `OnMember*` / `OnRound*` methods from within K4-Guilds' own event hooks.

---

## 18. Translations

Translation files are located at `resources/translations/<langCode>.jsonc`. The plugin selects each player's file based on their preferred language. A missing key falls back to the raw key string.

**To add a language:** copy `en.jsonc` to `fr.jsonc` (or any ISO language code) and translate all values.

### Key Reference

| Key | Args | Description |
|---|---|---|
| `k4.general.prefix` | — | Prepended to all guild messages |
| `k4.rank.leader` / `.officer` / `.member` | — | Default rank names |
| `k4.command.help.*` | — | One key per command in the help list |
| `k4.command.info.header` | `{0}` name, `{1}` tag | Info command header |
| `k4.command.info.members` | `{0}` count, `{1}` max | Info member line |
| `k4.command.info.bank` | `{0}` balance, `{1}` capacity | Info bank line |
| `k4.command.info.xpboost` | `{0}` % | Info XP boost line |
| `k4.command.members.item` | `{0}` name, `{1}` rank | Single member list entry |
| `k4.command.top.item` | `{0}` rank, `{1}` name, `{2}` tag, `{3}` kills, `{4}` deaths, `{5}` hs, `{6}` assists | Single top entry |
| `k4.command.upgrades.item` | `{0}` type, `{1}` desc, `{2}` level, `{3}` max, `{4}` next cost | Upgrade list entry |
| `k4.command.upgrades.item_maxed` | `{0}` type, `{1}` desc, `{2}` level | Maxed upgrade entry |
| `k4.guild.error.name_too_short` | `{0}` min | Name too short |
| `k4.guild.error.name_too_long` | `{0}` max | Name too long |
| `k4.guild.error.tag_too_long` | `{0}` max | Tag too long |
| `k4.guild.error.not_enough_currency` | `{0}` required, `{1}` have | Insufficient personal funds |
| `k4.bank.error.insufficient_balance` | `{0}` current | Insufficient personal balance |
| `k4.bank.error.exceed_capacity` | `{0}` capacity, `{1}` current | Deposit would exceed bank cap |
| `k4.upgrade.error.insufficient_funds` | `{0}` cost, `{1}` balance | Insufficient bank funds for upgrade |
| `k4.chat.format` | `{0}` tag, `{1}` sender, `{2}` message | Guild chat message format |
| `k4.chat.invite_received` | `{0}` inviter name | Invite notification |

---

## 19. Validation Rules Reference

### Guild Name Rules (applied on `create` and `rename`)

| Rule | Condition for Failure | Error Key |
|---|---|---|
| Non-empty | Null or whitespace-only | `k4.guild.error.name_empty` |
| No leading/trailing whitespace | `name.Trim() != name` | `k4.guild.error.name_whitespace` |
| Character whitelist | Does not match `^[\p{L}\p{N}\s\-_\.]+$` | `k4.guild.error.name_invalid_chars` |
| Minimum length | `name.Length < MinNameLength` | `k4.guild.error.name_too_short` |
| Maximum length | `name.Length > MaxNameLength` | `k4.guild.error.name_too_long` |
| Uniqueness | Name already exists in DB | `k4.guild.error.name_taken` |

Allowed characters: Unicode letters (`\p{L}`), digits (`\p{N}`), spaces, `-`, `_`, `.`

### Guild Tag Rules (applied on `create` only)

| Rule | Condition for Failure | Error Key |
|---|---|---|
| Non-empty | Null or whitespace-only | `k4.guild.error.tag_empty` |
| Character whitelist | Does not match `^[\p{L}\p{N}\p{S}\p{M}\-_]+$` | `k4.guild.error.tag_invalid_chars` |
| Maximum length | `StringInfo(tag).LengthInTextElements > MaxTagLength` | `k4.guild.error.tag_too_long` |

Allowed: Unicode letters, digits, symbols (`\p{S}`), combining marks (`\p{M}`), `-`, `_`. No spaces. Length counted as Unicode text elements (emoji = 1 element regardless of byte length).

### Bank Validation

| Check | Condition | Error Key |
|---|---|---|
| Valid amount | `amount <= 0` | `k4.bank.error.invalid_amount` |
| Sufficient personal funds (deposit) | Player wallet < amount | `k4.bank.error.insufficient_balance` |
| Bank capacity (deposit) | `balance + amount > MaxBankCapacity` | `k4.bank.error.exceed_capacity` |
| Sufficient bank funds (withdraw/upgrade) | Bank balance < amount | `k4.bank.error.insufficient_bank` OR `k4.upgrade.error.insufficient_funds` |
| Economy API present | API is null | `k4.economy.error.unavailable` |

### Rank Hierarchy Enforcement

| Operation | Block Condition |
|---|---|
| Kick | `targetPriority >= kickerPriority` |
| Promote | No rank `r` with `targetPriority < r.Priority < promoterPriority` |
| Demote | `targetPriority >= demoterPriority`, or no rank below target |
| Kick / Demote the leader | Target is `LeaderSteamId` — always blocked |
| Leader leaving | Always blocked — must `disband` |

### Target Resolution (`FindGuildMember`)

Resolution priority:
1. **Online by name** — SwiftlyS2 `FindTargettedPlayers` with `NoMultipleTargets` mode. If one result found and is a guild member → resolved.
2. **Multiple online matches** → error `multiple_matches`.
3. **SteamID parse** — if input is a numeric ID (via `CSteamID`), look up in guild members.
4. **Offline name substring** — case-insensitive `Contains` search across all guild members' `PlayerName`. Exactly one match required.
5. Zero matches → `k4.command.error.member_not_found`.
6. Multiple name matches → `k4.command.error.multiple_matches`.

### Top Command Limits

| Constraint | Value |
|---|---|
| Minimum count | 1 |
| Maximum count | 25 |
| Default count | 5 |

---

*Documentation generated for K4-Guilds v1.0.3. Last updated: April 2026.*
