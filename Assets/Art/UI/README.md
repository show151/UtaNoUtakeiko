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

The title image and play wallpaper use the 1920 × 1080 reference canvas.
Character sprites preserve their aspect ratio and transparency. Singing artwork
switches at the configurable achievement-rate threshold on the presenter.

The repository does not include a Japanese-capable TMP font yet. Assign one to
the presenter's **Japanese Font** field before relying on the Japanese UI text.
