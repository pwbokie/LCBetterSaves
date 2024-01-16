# LCBetterSaves

This mod expands the save functionality in Lethal Company, allowing you to create multiple saves beyond the original three.

## Features
- **Create Additional Saves:** No longer limited to just three saves, you can create new ones as needed.
- **Rename Saves:** Easily rename your saves for better organization.

## Known Incompatibilities
- **Glowstick** (support not planned)

## Installation
1. **Backup Existing Saves:** Before installing, back up your current saves. They are located at:
C:/Users/(User)/AppData/LocalLow/ZeekerssRBLX/Lethal Company

2. **Install the Mod:** Follow the standard mod installation procedure for Lethal Company.

## How to Use
### Creating New Saves
- Go to the main menu.
- Select **"New Save"** when hosting a game. This allows you to create additional saves.

### Renaming a Save
1. Enter **Online mode**.
2. Select your desired save.
3. Type a new name in the **Lobby Name** field.
4. Click the **"I"** button next to your save to finalize the renaming.

## Reporting Issues
Encountered a bug or an issue? Please create an issue here in the repo detailing the problem.

Thank you so much for using my mod!

## FOR MOD AUTHORS - Adding Compatibility to YOUR Mod!
Does your mod track saves externally, breaking the interaction with this mod? Are your users coming to you about it? No longer!
Lethal Company uses Easy Save 3 (henceforth ES3) to save and load savefiles. You are able to inject custom data into a save file, simplifying things on your end by removing your mod's need for an external file, as well as improving all forward compatibility with any mod that interacts with saves.
- In your mod's .csproj, add this block alongside your other references, replacing the path with the one to your Lethal Company installation:
```
<Reference Include="Assembly-CSharp-firstpass">
    <HintPath>C:\...\SteamLibrary\steamapps\common\Lethal Company\Lethal Company_Data\Managed\Assembly-CSharp-firstpass.dll</HintPath>
</Reference>
```
- You should now have the capacity to reference the ES3 class (https://docs.moodkie.com/easy-save-3/es3-api/es3-class/).
- You can use ES3.Save("Tag_Name", data, filePath) to write your data directly into the savefile, and you can access it with ES3.Load("Tag_Name", filePath, defaultValue). Make sure you use a Tag_Name unique to your mod - for example, for my mod FartLizards, I might do something like "FartLizards_Data".
- For more complicated objects, I would recommend converting to and from a json file and writing that to the save file.
