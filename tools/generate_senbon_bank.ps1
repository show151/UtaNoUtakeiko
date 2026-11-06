param(
    [Parameter(Mandatory = $true)][string]$InputWav,
    [int]$Parallelism = 3,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$chart = Get-Content -Raw -Encoding UTF8 (Join-Path $repo 'Assets/Resources/SongCharts/senbon_hard.json') | ConvertFrom-Json
$output = if ($OutputDirectory) { $OutputDirectory } else { Join-Path $repo 'Assets/Resources/VocalBank/SENBONZAKURA' }
New-Item -ItemType Directory -Force -Path $output | Out-Null
$source = (Resolve-Path -LiteralPath $InputWav).Path
$secondsPerBeat = 60.0 / $chart.Bpm
$beatsPerMeasure = $chart.TimeSignatureNumerator * 4.0 / $chart.TimeSignatureDenominator
$leadBeats = $beatsPerMeasure - $chart.PickupBeats
$entries = @()
foreach ($measure in ($chart.Sections[0].Notes | Group-Object MeasureNumber)) {
    $elapsed = 0.0
    foreach ($note in $measure.Group) {
        $relative = ([int]$note.MeasureNumber - 1) * $beatsPerMeasure + $elapsed
        if ([int]$note.MeasureNumber -gt 1) { $relative -= $leadBeats }
        $entries += [pscustomobject]@{
            NoteId = $note.NoteId
            Pitch = [int]$note.TargetPitchStep
            Start = ($relative + $leadBeats) * $secondsPerBeat
            Duration = [double]$note.DurationBeats * $secondsPerBeat
        }
        $elapsed += [double]$note.DurationBeats
    }
}

$entries | ForEach-Object -Parallel {
    $note = $_
    $fmt = [Globalization.CultureInfo]::InvariantCulture
    $branches = (0..14 | ForEach-Object { "[a$_]" }) -join ''
    $filters = @("[0:a]aformat=sample_rates=24000:channel_layouts=mono,asplit=15$branches")
    $frames = [int][Math]::Round($note.Duration * 24000)
    foreach ($i in 0..14) {
        $factor = [Math]::Pow(2.0, ($i - $note.Pitch) / 12.0).ToString('G17', $fmt)
        $filters += "[a$i]rubberband=pitch=$factor,apad=whole_len=$frames,atrim=end_sample=$frames[b$i]"
    }
    $arguments = @('-hide_banner','-loglevel','error','-y','-ss', $note.Start.ToString('G17', $fmt),
        '-t', $note.Duration.ToString('G17', $fmt), '-i', $using:source, '-filter_complex', ($filters -join ';'))
    foreach ($i in 0..14) {
        $path = Join-Path $using:output "$($note.NoteId)_p$i.ogg"
        $arguments += @('-map', "[b$i]", '-c:a', 'libvorbis', '-q:a', '3', $path)
    }
    & ffmpeg @arguments
    if ($LASTEXITCODE -ne 0) { throw "Audio generation failed for $($note.NoteId)" }
    foreach ($i in 0..14) {
        $path = Join-Path $using:output "$($note.NoteId)_p$i.ogg.meta"
        if (-not (Test-Path -LiteralPath $path)) {
            $guid = [guid]::NewGuid().ToString('N')
            $meta = @(
                'fileFormatVersion: 2', "guid: $guid", 'AudioImporter:',
                '  externalObjects: {}', '  serializedVersion: 8', '  defaultSettings:',
                '    serializedVersion: 2', '    loadType: 0', '    sampleRateSetting: 0',
                '    sampleRateOverride: 24000', '    compressionFormat: 0', '    quality: 1',
                '    conversionMode: 0', '    preloadAudioData: 1', '  platformSettingOverrides: {}',
                '  forceToMono: 0', '  normalize: 0', '  loadInBackground: 0', '  ambisonic: 0',
                '  3D: 0', '  userData:', '  assetBundleName:', '  assetBundleVariant:'
            ) -join "`n"
            [IO.File]::WriteAllText($path, $meta + "`n")
        }
    }
    Write-Output $note.NoteId
} -ThrottleLimit $Parallelism
