# UI artwork

The five supplied PNGs are imported as sprites and assigned in
`Assets/Scenes/Game.unity` to the matching `ScreenArtworkPresenter` fields.

| Presenter field | Repository asset | Source file |
| --- | --- | --- |
| Title Artwork | `title.png` | `背景イラスト2.png` |
| Normal Character | `character_normal.png` | `ウタの立ち絵.png` |
| Singing Good Character | `character_singing_good.png` | `ウタの立ち絵2.png` |
| Singing Bad Character | `character_singing_bad.png` | `ウタの立ち絵3.png` |
| Play Wallpaper | `play_wallpaper.png` | `背景イラスト.png` |

Title, difficulty select, and song select currently share the title artwork.
The play wallpaper uses a world-space SpriteRenderer behind the existing game
layers during play and playback. The presenter adds no UI text; the existing
gameplay UI remains in place. The result screen shows the normal character, and
the singing screen selects its good or bad artwork using the configurable
achievement-rate threshold.

The title and wallpaper are 3840 × 2160 and fill a 1920 × 1080 reference view.
Character PNGs retain transparency and aspect ratio.
