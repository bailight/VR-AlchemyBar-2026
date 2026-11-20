# Hex & Hearth: Figma → Unity

Regenerates `Assets/AlchemyBar/Art/UI/HexHearth` from a `.fig` file (Figma → File → Save local copy).

```sh
npm i fig-kiwi kiwi-schema fzstd pako @resvg/resvg-js
mkdir -p x && unzip -o UI.fig -d x
node parse.mjs x/canvas.fig canvas.json
python3 build.py canvas.json x/images out        # needs Pillow
node render.mjs out/svg out/Sprites
cp out/Sprites/*.png ../../Assets/AlchemyBar/Art/UI/HexHearth/Sprites/
cp out/hexhearth_layout.json ../../Assets/AlchemyBar/Art/UI/HexHearth/
```

Then in Unity: **AlchemyBar → UI → Build Hex & Hearth Menus**. Delete stale sprites by hand if layers were renamed.
Fonts: Cormorant Garamond and Inter, SIL Open Font License 1.1.
