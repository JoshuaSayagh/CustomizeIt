# Custom Tourism

A **Cities: Skylines II** mod that lets you control the number of tourists in your city and customize building attractiveness.

## What it does

### Tourism Control
- Set a **target tourist count** for your city from the settings panel
- The mod spawns and manages tourist households to reach your target
- **0 = disabled** (vanilla behavior)
- Works independently from the game's built-in tourism formula
- Make sure you have enough hotels to accommodate your tourists

### Attractiveness Editor
- Select any building with attractiveness (landmarks, parks, tourist attractions) and an **editor panel** appears
- **Slider** to set attractiveness from 0 to 500
- One-click **Restore Default** to revert to vanilla values
- Changes apply to **all buildings of the same type**
- Overrides are **saved automatically** and persist across game restarts
- To close the panel, click anywhere else

## Languages
- English, French

## How it works

### Tourism Control
- Go to **Options → Custom Tourism → Tourism**
- Set the **Target Tourist Count** slider (0–20000)
- The mod will gradually spawn tourists until the target is reached
- Lowering the slider will gradually remove excess tourists

### Attractiveness Editor
- Click on any building with attractiveness in-game
- The Custom Tourism panel appears on the right side of the screen
- Drag the slider or type a value, then hit **Apply**
- The override is saved automatically and reloaded next time you play

## Note
- Attractiveness changes take about **5 to 20 seconds** to show up in the tourism menu. The override is applied immediately, but the game's tourism stats take a moment to recalculate.

## Compatibility
- Safe to remove anytime. Restore all overrides to default before uninstalling for clean vanilla values.
- Does not use Harmony

## Settings file
`ModsSettings/CustomTourism/CustomTourism.coc`

## Features

| Feature | Description |
|---------|-------------|
| Target tourist count | Set a custom tourist target for your city (0–20000). |
| Attractiveness slider | Set any building's attractiveness between **0 and 500**. |
| Restore Default | Reverts the building back to its vanilla attractiveness value. |
| Persistent overrides | Your changes are saved and automatically reapplied on game load. |
| Per-prefab changes | Changing one building affects all placed buildings of the same type. |

## Credits

Thanks to **Honu / River-Mochi** from the Cities: Skylines modding discord for their help!

## License

[MIT License](LICENSE)
