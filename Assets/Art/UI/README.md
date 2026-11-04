# UI artwork import slots

Assign a Japanese-capable TMP font asset to the presenter’s **Japanese Font** field. The repository only includes Liberation Sans, which does not contain Japanese glyphs; the UI shell falls back to it until a suitable font is assigned.

Drop the delivered PNG files in this folder, then assign them to the matching
`ScreenArtworkPresenter` fields on `GameSessionManager` in `Assets/Scenes/Game.unity`.

| Inspector field | File to add |
| --- | --- |
| Title Artwork | `title.png` |
| Normal Character | `character_normal.png` |
| Singing Good Character | `character_singing_good.png` |
| Singing Bad Character | `character_singing_bad.png` |
| Play Wallpaper | `play_wallpaper.png` |

For each PNG, set **Texture Type** to **Sprite (2D and UI)**. The title image and
play wallpaper fill the 1920 × 1080 reference canvas. Character sprites keep
their aspect ratio and sit at the lower right. Singing artwork is selected from
the final achievement rate; the threshold is configurable on the presenter.

These five files were not present in the repository when this integration was
prepared, so the Inspector references are intentionally empty.
