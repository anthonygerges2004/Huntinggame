REALISTIC FELLING AXE + PICKAXE FOR UNITY
=========================================
1. Copy the "RealisticAxe" folder into your project:  Assets/RealisticAxe   (exact path)
2. Copy "Editor/RealisticAxeSetup.cs" into Assets/Editor/
3. In Unity run:  Tools > Realistic Axe > Setup Materials + Prefabs
   -> creates Wood + Steel materials and FellingAxe.prefab / Pickaxe.prefab
4. Drag a prefab into your scene or parent it to your player's hand.

Model info
 - 1 unit = 1 m. Handle runs along +Y, the blade (or pick) points +Z.
 - Pivot / origin is at the grip (about 0.25 m from the handle end), so it rotates naturally in a hand.
 - FellingAxe: ~4.5k verts / 8.2k tris, 0.93 m long, 22 cm head, curved bit, hickory handle with swell + knob.
 - Pickaxe:    ~4.8k verts / 9.0k tris, 0.92 m long, 54 cm curved head.
 - Textures: Wood (512x2048 grain, polished grip), Steel (1024: hammered surface, scratches, patina,
   sharpened bevel), Steel_MetalSmooth (R = metallic, A = smoothness), normal maps for both.

Notes
 - Not using the script? Steel material: Albedo/Normal + Metallic map = Steel_MetalSmooth.png
   (uncheck sRGB on it). Wood material: Albedo/Normal, Smoothness ~0.3.
 - To harvest trees you still need gameplay code (raycast hit + damage); this package is the visual asset.
 - Regenerate:  python3 generate_tools.py RealisticAxe   (needs numpy + Pillow)
