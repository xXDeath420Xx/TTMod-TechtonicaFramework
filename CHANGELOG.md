# Changelog

All notable changes to TechtonicaFramework will be documented in this file.

## [1.2.0] - 2026-01-07

### Added
- **BuildMenu Module** - Adds a dedicated "Modded" tab to the build/crafting menu
  - Keeps modded items separate from vanilla items for clarity
  - All CertiFried mods now use this unified tab
- **CorruptedUnlockCleanup** - Automatically cleans corrupted unlock entries from saves
- **LogFilterPatches** - Suppresses known harmless Unity warnings/errors
- **FMODListenerFix** - Fixes FMOD audio listener warnings

### Changed
- Improved ModdedTabModule stability
- Better integration with EMUAdditions

## [1.1.0] - 2025-01-06

### Added
- ModdedTabModule for dedicated "Modded" tech tree category
- All mods can now use category 7 for their unlocks
- Harmony transpiler patches extend tech tree from 7 to 8 categories

## [1.0.0] - 2025-01-05

### Added
- Initial release
- **Health System**
  - Machine health management
  - Damage handling and processing
  - Repair mechanics support
- **Narrative System**
  - Dialogue triggering API
  - Quest creation helpers
  - Custom speaker registration
- **Equipment System**
  - Base class for custom equipment
  - Vehicle controller framework
  - Movement modifiers
- **Environment System**
  - Hazard zone management
  - Area trigger systems
  - Status effect handling
- Core framework APIs for dependent mods
