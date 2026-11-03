
import argparse
import json
from pathlib import Path

import librosa
import numpy as np
import soundfile as sf


def find_note_timing(chart, note_id, offset):
  bpm = chart["Bpm"]
  beats_per_measure = (
      chart["TimeSignatureNumerator"]
      * 4 / chart["TimeSignatureDenominator"]
  )

  measures = {}
  for section in chart["Sections"]:
    for note in section["Notes"]:
      measures.setdefault(note["MeasureNumber"], []).append(note)

  target = None

  for measure_number in range(1, max(measures) + 1):
    notes = sorted(
        measures.get(measure_number, []),
        key=lambda n: n["NoteIndexInMeasure"],
    )

    beat_offset = 0.0
    for note in notes:
      start_beat = (
          (measure_number - 1) * beats_per_measure
          + beat_offset
      )

      if note["NoteId"] == note_id:
        if note["IsRest"]:
          raise ValueError("休符は音程変更できません")

        target = (
            offset + start_beat * 60 / bpm,
            note["DurationBeats"] * 60 / bpm,
        )

      beat_offset += note["DurationBeats"]

    if not np.isclose(beat_offset, beats_per_measure):
      raise ValueError(
          f"{measure_number}小節の拍数が不正: {beat_offset}"
      )

  if target is None:
    raise ValueError(f"NoteIdが見つかりません: {note_id}")

  return target

def main():
  parser = argparse.ArgumentParser()
  parser.add_argument("wav", type=Path)
  parser.add_argument("--offset", type=float, default=0)
  parser.add_argument(
      "--shifts", type=float, nargs=2, default=[2, -2]
  )
  args = parser.parse_args()

  chart_path = Path("Assets/Data/Songs/twinkle_easy.json")
  chart = json.loads(chart_path.read_text(encoding="utf-8"))

  audio, sr = sf.read(
      args.wav, dtype="float32", always_2d=True
  )

  result = audio.copy()
  output_dir = Path("audio_prototype_outputs")
  output_dir.mkdir(exist_ok=True)

  note_ids = [
      "TWINKLE_M01_N01",
      "TWINKLE_M01_N02",
  ]

  for note_id, semitones in zip(note_ids, args.shifts):
    start, duration = find_note_timing(
        chart, note_id, args.offset
    )

    start_sample = round(start * sr)
    end_sample = round((start + duration) * sr)

    if start_sample < 0 or end_sample > len(audio):
      raise ValueError(f"音声範囲外: {note_id}")

    original = audio[start_sample:end_sample].copy()

    shifted = np.column_stack([
        librosa.effects.pitch_shift(
            original[:, channel],
            sr=sr,
            n_steps=semitones,
        )
        for channel in range(original.shape[1])
    ])

    # 元音声と変換後の音量を比較
    original_rms = np.sqrt(np.mean(original ** 2))
    shifted_rms = np.sqrt(np.mean(shifted ** 2))

    if original_rms > 1e-5 and shifted_rms > 1e-5:
      gain = original_rms / shifted_rms

      # 過度な音量変化を防ぐ
      gain = np.clip(gain, 0.8, 1.25)

      # クリッピングを避ける
      peak = np.max(np.abs(shifted))
      if peak > 0:
        gain = min(gain, 0.99 / peak)

      shifted *= gain

      print(f"{note_id}: 音量補正 {gain:.3f}倍")

    # 20msのフェードで変換前後を滑らかにつなぐ
    fade_length = min(
        round(sr * 0.02),
        len(original) // 4
    )

    fade = np.ones(
        (len(original), 1), dtype=np.float32
    )

    if fade_length > 0:
      fade[:fade_length, 0] = np.linspace(
          0, 1, fade_length
      )
      fade[-fade_length:, 0] = np.linspace(
          1, 0, fade_length
      )

    result[start_sample:end_sample] = (
        original * (1 - fade) + shifted * fade
    )

    print(
        f"{note_id}: "
        f"{start:.3f}〜{start + duration:.3f}秒 "
        f"({semitones:+g}半音)"
    )

  sf.write(
      output_dir / "full_preview_two_notes.wav",
      np.clip(result, -1, 1),
      sr,
  )

  print(f"元音声: {len(audio) / sr:.3f}秒")
  print(f"変換後: {len(result) / sr:.3f}秒")
  print("2音の変換完了！")

if __name__ == "__main__":
  main()
