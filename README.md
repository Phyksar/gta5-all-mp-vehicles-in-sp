# Grand Theft Auto 5 - All MP Vehicles In SP
This mod adds all the vehicles from GTA Online to GTA 5. The new vehicles can be found in parking lots throughout Los Santos and Blaine County.
- All content from GTA Online is added, including air and water vehicles, except vehicles from previous updates that are available in Single Player.
- Over 100 new vehicle spawn points.
- Parking lot vehicle spawns are constantly changing.
- All military vehicles from GTA Online (including both versions of the Oppressor) will be added to the military base at Fort Zancudo.
- Specific spawn points are chosen for different types of specialized vehicles to justify their presence in the game's lore.

# Fork Purpose
- Fixed performance issues - original code was making too many unnecessary array iterations each frame drastically reducing CPU performance.
- Improved code readability.
- Fixed LockDoors option for parked vehicles, added new AlarmRatePercentage, other non-working settings are removed.
- Added automatic excluding of missing vehicles not presented in the older versions of the game.
- Minor visual tweaks.

# Installation
1. Download and install [ScriptHookV](http://dev-c.com/gtav/scripthookv/) and [ScriptHookVDotNet](https://github.com/scripthookvdotnet/scripthookvdotnet/releases/latest)
2. Move all files from the archive to the `scripts` folder inside the game directory (create new, if it doesn't exist).

# Build
1. Complete first step from the Installation guide.
2. Install [Visual Studio IDE](https://visualstudio.microsoft.com/) with .NET Framework 4.8 components enabled.
3. Open `Command Prompt` and set `GTA_V_PATH` enviroment variable to match the path to game directory.
    ```bat
    set "GTA_V_PATH=C:\Games\Grand Theft Auto V"
    ```
4. Open and build the `AllMpVehiclesInSp.sln` solution. The compiled binary will be copied to the `scripts` folder on successful build.

# Script Settings
You can modify the `AllMpVehiclesInSp.ini` settings file in the `scripts` folder:
### Parking
- LockDoors = `true`/`false`
  > Enable locking doors of parked vehicles. If enabled, the player will have to break a window or lockpick it to enter.
- AlarmRatePercentage = `0`-`100`
  > Controls the rate of parked vehicles that have an alarm set on, `0` means no vehicles will have one and `100` means all of the vehicles will have an alarm. This setting depends on `LockDoors` set to `true`.
- ShowBlips = `true`/`false`
  > Show blips on minimap for parked vehicles.
