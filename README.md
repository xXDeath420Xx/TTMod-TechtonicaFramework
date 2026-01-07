# Techtonica Framework

Core framework providing shared systems for CertiFried mod suite.

## Features

### Health System
- Machine health management
- Damage handling and processing
- Repair mechanics support

### Narrative System
- Dialogue triggering API
- Quest creation helpers
- Custom speaker registration

### Equipment System
- Base class for custom equipment
- Vehicle controller framework
- Movement modifiers

### Environment System
- Hazard zone management
- Area trigger systems
- Status effect handling

## For Mod Developers

This framework provides APIs that other mods can build upon. See the source code for integration examples.

## Requirements

- BepInEx 5.4.21+
- EquinoxsModUtils 6.1.3+
- EMUAdditions 2.0.0+

## Installation

Install via r2modman or manually place the DLL in your BepInEx/plugins folder.

## Modded Tab System

TechtonicaFramework adds a "Modded" tab (category 7) to the tech tree, allowing all mods to register their unlocks in a dedicated section rather than cluttering vanilla categories.

### Usage for Mod Developers
```csharp
using TechtonicaFramework.TechTree;

// Register unlocks to the Modded tab
EMUAdditions.AddNewUnlock(new NewUnlockDetails
{
    category = ModdedTabModule.ModdedCategory,  // Category 7
    // ... other unlock settings
});
```

## Development

This mod was developed with assistance from **Claude Code** (Anthropic's AI coding assistant).
All code has been reviewed and tested by the mod author (CertiFried / xXDeath420Xx).

## Links

- [Thunderstore](https://thunderstore.io/c/techtonica/p/CertiFried/TechtonicaFramework/)
- [GitHub Repository](https://github.com/xXDeath420Xx/TTMod-TechtonicaFramework)
- [Bug Reports](https://github.com/xXDeath420Xx/TTMod-TechtonicaFramework/issues)

## License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.

## Changelog

### [1.2.0] - 2026-01-07
- Added BuildMenu Module - dedicated "Modded" tab in build/crafting menu
- Added CorruptedUnlockCleanup - auto-cleans corrupted unlock entries
- Added log filtering for known harmless warnings
- Fixed FMOD audio listener warnings

### [1.1.0] - 2025-01-06
- Added ModdedTabModule for dedicated "Modded" tech tree category
- All mods can now use category 7 for their unlocks
- Harmony transpiler patches extend tech tree from 7 to 8 categories

### [1.0.0] - 2025-01-05
- Initial release
- Health System module
- Narrative System module
- Equipment System module
- Environment System module
