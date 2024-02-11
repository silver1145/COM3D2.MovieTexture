# COM3D2.MovieTexture

`COM3D2.MovieTexture.Plugin` provides the function of using video as RenderTexture.
For the original texture, just place the video file with the same name in the directory of the same name to load it.

## Install

Download from [Release](https://github.com/silver1145/COM3D2.MovieTexture/releases) and Extract `config` & `plugins` to `BepinEx/`

**Optional Dependencies**:

* COM3D2.NPRShader.Plugin [Sybaris Version]
* COM3D2.SceneCapture.Plugin [Sybaris Version]
* COM3D2.DanceCameraMotion.Plugin [Version>=7.0]

`COM3D2.MovieTexture.Plugin` implements the replacement of the following textures:

1. Textures in Model/Mate of original game
2. Textures in Mate of NPRShader
3. Textures of the item (Mate) loaded by the refresh function of NPRShder.ObjectWindows
4. Textures of the item (Model/Mate) called out by SceneCapture

The texture designation in the Menu is not managed by this plugin.

## Tutorial

Place the video files corresponding to the textures in the same directory. For example, `{TexName}.tex` - `{TexName}.mp4`. Note that the video file format only supports MP4 (H264).
If you need to use a video with a transparent channel, you need to process the video and the file name needs to end with .alphapack, such as `{TexName}.tex` - `{TexName}.alphapack.mp4`.

You can use Adobe AE/PR/PS to create a video with a transparency channel. Use the QuickTime and .mov format when exporting or rendering. Use `Apple ProRes 4444 XQ` or `Apple ProRes 4444` for video encoding, and confirm the transparency channel option (AE : Output_Channel=RGB+Alpha, PR: Video_Depth=8bpc+Alpha, PS (timeline animation): Render_Option->Alpha=Direct).

Then create AlphaPack video using [ffmpeg](https://www.ffmpeg.org/download.html) (add to PATH):
> ffmpeg -i {FileName}.mov -vf "split [a], pad=iw:ih*2 [b], [a] alphaextract, [b] overlay=0:h" -y {FileName}.alphapack.mp4

Or use the batch script `AlphaPackTool.bat`:

```bat
@echo off
echo.
chcp 65001
set movFile=%1
if defined movFile (goto ffmpeg_task) else set /p movFile="Drag in the mov file with a transparent channel and press Enter:"
:ffmpeg_task
for %%f in (%movFile%) do set mp4File="%%~ndpf.alphapack.mp4"
ffmpeg -i %movFile% -vf "split [a], pad=iw:ih*2 [b], [a] alphaextract, [b] overlay=0:h" -y %mp4File%
echo.
echo "Finished."
pause>nul
```

If you need to adjust the playback settings, create an xml with the same name at the video location (`{filename}.xml` for `{filename}.mp4` or `{filename}.alphapack.xml` for `{filename}.alphapack.mp4`).
The example settings (each node is optional):

```xml
<MediaConfig>
    <Loop>True</Loop>
    <Muted>False</Muted>
    <Volume>1.0</Volume>
    <PlaybackRate>1.0</PlaybackRate>
    <WrapMode>Repeat</WrapMode>
    <FilterMode>Bilinear</FilterMode>
    <AnisoLevel>1</AnisoLevel>
</MediaConfig>
```

Option description:

1. Loop:           [bool]
2. Muted:          [bool]
3. Volume:         [float(0 ~ 1)]
4. PlaybackRate:   [float(-4 ~ 4)]
5. WrapMode:       [Repeat|Clamp]
6. FilterMode:     [Point|Bilinear|Trilinear]
7. AnisoLevel:     [int(0 ~ 16)]

**Note**:

* You can add a video as RenderTexture to any Texture Slot. This means that some special dynamic effects can be achieved, such as replacing `_MatcapMap` map to implement transition transformations in different MatCaps, or replacing `_NormalMap`/`_ParallaxShaderToggle` map to achieve normal/parallax animation.
* If `COM3D2.MaidLoader` is installed, the refresh function of MaidLoader will reload all *.mp4
* Use [dcm_sync_anm](https://github.com/silver1145/scripts-com3d2#dcm_sync_anm) to sync video with `COM3D2.DanceCameraMotion.Plugin`.
