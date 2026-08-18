# Flower Sprite Converter

Converts the flower sprite sheet into C assets used by the ESP32
firmware.

The generated asset contains color palettes for both supported displays.
The correct palette is selected at compile time using
`APP_DISPLAY_TYPE`.

## Usage

Run from the `firmware_esp32c6` directory:

``` powershell
python tools\convert_flower_sprite.py <source> <header> <implementation>
```

Parameters:

-   `<source>` --- source RGBA PNG (`160x774`, 9 frames of `160x86`)
-   `<header>` --- output `.h` file
-   `<implementation>` --- output `.c` file

Example:

``` powershell
python tools\convert_flower_sprite.py assets\flower_animation.png main\flower_sprite_asset.h main\flower_sprite_asset.c
```

The converter generates:

-   BGR565 palette for ST7789
-   RGB565 palette for JD9853
-   shared indexed sprite frames for both displays

The required palette is selected automatically during firmware
compilation.

ST7789 is the default firmware target. To build the default version:

``` powershell
idf.py build
```

To build the JD9853 version into a separate directory:

``` powershell
idf.py -B build_jd9853 -D APP_DISPLAY_TYPE=2 build
```

Generated `.c/.h` files should not be edited manually.
