# Changelog

All notable changes to this project will be documented in this file.

## [v1.0.2] - 2026.02.11

### Fixed

- **CRITICAL**: Fixed config binding using wrong parameter in `BuildConfigService<T>()` method
  - Changed `.BindConfiguration(fileName)` to `.BindConfiguration(sectionName)`
  - This bug caused all config values to use hardcoded defaults instead of reading from config files
  - Database connection was always using default `"host"` instead of configured `"guilds_database"`
  - Affected all three configs: `guild.json`, `upgrades.json`, `commands.json`
  - **Impact**: Plugin was completely non-functional due to wrong database being used
- Fixed migration cleanup to conditionally delete guild-related tables only if they exist
- Fixed player connection/disconnection event handling in Plugin class

## [v1.0.1]

### Changed

- Refactored config handling to use `IOptionsMonitor<T>` with `CurrentValue` pattern (matching K4-WeaponPurchase pattern)
  - Changed `IOptions<T>.Value` to `IOptionsMonitor<T>.CurrentValue` for hot-reload support
  - Changed `AddOptionsWithValidateOnStart<T>()` to `AddOptions<T>()` for consistent pattern
  - Made config monitors static for better accessibility: `GuildConfig`, `UpgradesConfig`, `CommandsConfig`
  - Added convenience properties `Guild`, `Upgrades`, `Commands` that access `.CurrentValue`

### Technical Notes

- All database models use auto-increment integer primary keys - no `[DatabaseGenerated(DatabaseGeneratedOption.None)]` attribute needed
- GameRules handling not applicable (plugin uses JSON config files instead)
