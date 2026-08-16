# Flower Sprite Converter

Converts the flower sprite sheet into C assets used by the ESP32
firmware.

## Usage

Run from the `firmware_esp32c6` directory:

``` powershell
python tools\convert_flower_sprite.py <source> <header> <implementation> --display <display>
```

Parameters:

-   `<source>` --- source RGBA PNG (`160x774`, 9 frames of `160x86`)
-   `<header>` --- output `.h` file
-   `<implementation>` --- output `.c` file
-   `--display` --- target display color format:
    -   `st7789` --- BGR565
    -   `jd9853` --- RGB565

Example:

``` powershell
python tools\convert_flower_sprite.py assets\flower_animation.png main\flower_sprite_asset.h main\flower_sprite_asset.c --display jd9853
```

`st7789` is the default display if `--display` is omitted.

Generated `.c/.h` files should not be edited manually.
