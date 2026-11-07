param([Parameter(Mandatory=$true)][string]$InputWav, [Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$chart = Get-Content -Raw -Encoding UTF8 'Assets/Data/Songs/twinkle_easy.json' | ConvertFrom-Json
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$destination = (Resolve-Path -LiteralPath $OutputDirectory).Path
$source = (Resolve-Path -LiteralPath $InputWav).Path
$secondsPerBeat = 60.0 / $chart.Bpm
$beatsPerMeasure = $chart.TimeSignatureNumerator * 4.0 / $chart.TimeSignatureDenominator
$notes = @()
foreach ($section in $chart.Sections) {
    foreach ($measure in ($section.Notes | Group-Object MeasureNumber)) {
        $beat = 0.0
        foreach ($note in $measure.Group) {
            $start = (([int]$note.MeasureNumber - 1) * $beatsPerMeasure + $beat) * $secondsPerBeat
            $duration = [double]$note.DurationBeats * $secondsPerBeat
            if (-not $note.IsRest) {
                $frames = [int]([Math]::Round(($start + $duration) * 48000) - [Math]::Round($start * 48000))
                $notes += [pscustomobject]@{ NoteId=$note.NoteId; Start=$start; Duration=$duration; Pitch=[int]$note.TargetPitchStep; Frames=$frames }
            }
            $beat += [double]$note.DurationBeats
        }
    }
}
$notes | ForEach-Object -Parallel {
    $note = $_; $fmt = [Globalization.CultureInfo]::InvariantCulture
    $branches = (0..9 | ForEach-Object { "[a$_]" }) -join ''
    $filters = @("[0:a]aformat=sample_rates=48000:channel_layouts=stereo,asplit=10$branches")
    foreach ($i in 0..9) {
        $factor = [Math]::Pow(2.0, ($i - $note.Pitch) / 12.0).ToString('G17', $fmt)
        $pitchFilter = if ($i -eq $note.Pitch) { 'anull' } else { "rubberband=pitch=$factor" }
        $filters += "[a$i]$pitchFilter,apad=whole_len=$($note.Frames),atrim=end_sample=$($note.Frames)[b$i]"
    }
    $argsList = @('-hide_banner','-loglevel','error','-y','-ss',$note.Start.ToString('G17',$fmt),'-t',$note.Duration.ToString('G17',$fmt),'-i',$using:source,'-filter_complex',($filters -join ';'))
    foreach ($i in 0..9) { $argsList += @('-map',"[b$i]",'-c:a','pcm_s16le',(Join-Path $using:destination "$($note.NoteId)_p$i.wav")) }
    & ffmpeg @argsList
    if ($LASTEXITCODE -ne 0) { throw "Failed to generate $($note.NoteId)" }
    Write-Output $note.NoteId
} -ThrottleLimit 3
