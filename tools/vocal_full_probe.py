
import argparse
import json
from pathlib import Path

import librosa
import numpy as np
import soundfile as sf

from vocal_pitch_probe import find_note_timing


def playable_notes(section):
  return sorted(
      [n for n in section["Notes"] if not n["IsRest"]],
      key=lambda n: (
          n["MeasureNumber"],
          n["NoteIndexInMeasure"],
      ),
  )


def change_pitch(original, sr, semitones):
  if semitones == 0:
    return original.copy()

  shifted = np.column_stack([
      librosa.effects.pitch_shift(
          original[:, ch],
          sr=sr,
          n_steps=semitones,
      )
      for ch in range(original.shape[1])
  ])

  # 音量補正
  original_rms = np.sqrt(np.mean(original ** 2))
  shifted_rms = np.sqrt(np.mean(shifted ** 2))

  if original_rms > 1e-5 and shifted_rms > 1e-5:
    gain = np.clip(
        original_rms / shifted_rms, 0.8, 1.25
    )
    peak = np.max(np.abs(shifted))
    if peak > 0:
      gain = min(gain, 0.99 / peak)
    shifted *= gain

  # 各音符の両端20msは元音声に滑らかに接続
  fade_n = min(round(sr * 0.02), len(original) // 4)

  if fade_n:
    fade = np.ones((len(original), 1), dtype=np.float32)
    fade[:fade_n, 0] = np.linspace(0, 1, fade_n)
    fade[-fade_n:, 0] = np.linspace(1, 0, fade_n)

    shifted = original * (1 - fade) + shifted * fade

  return shifted


def main():
  parser = argparse.ArgumentParser()
  parser.add_argument("wav", type=Path)
  parser.add_argument("--offset", type=float, default=0)
  parser.add_argument(
      "--input-count",
      type=int,
      default=None,
  )
  args = parser.parse_args()

  chart = json.loads(
      Path("Assets/Data/Songs/twinkle_easy.json")
      .read_text(encoding="utf-8")
  )

  audio, sr = sf.read(
      args.wav, dtype="float32", always_2d=True
  )

  sections = sorted(
      chart["Sections"],
      key=lambda s: s["StartMeasure"],
  )
  by_id = {s["SectionId"]: s for s in sections}

  repeat_sources = {
      m["TargetSectionId"]: m["SourceSectionId"]
      for m in chart["RepeatMappings"]
  }

  # 入力対象は21音符。繰り返し先は含めない
  input_notes = [
      note
      for section in sections
      if section["SectionId"] not in repeat_sources
      for note in playable_notes(section)
  ]

  assert len(input_notes) == 21

  input_count = (
      len(input_notes)
      if args.input_count is None
      else args.input_count
  )

  if not 0 <= input_count <= len(input_notes):
    raise ValueError("入力音符数が不正です")

  pattern = [2, -2, 1, 0, -1]

  shifts = {
      note["NoteId"]: pattern[i % len(pattern)]
      for i, note in enumerate(input_notes[:input_count])
  }

  bpm = chart["Bpm"]
  beats_per_measure = (
      chart["TimeSignatureNumerator"]
      * 4 / chart["TimeSignatureDenominator"]
  )
  duration = (
      max(s["EndMeasure"] for s in sections)
      * beats_per_measure * 60 / bpm
  )

  result = np.zeros(
      (round(duration * sr), audio.shape[1]),
      dtype=np.float32,
  )

  for section in sections:
    source_id = repeat_sources.get(
        section["SectionId"],
        section["SectionId"],
    )

    target_notes = playable_notes(section)
    source_notes = playable_notes(by_id[source_id])

    if len(target_notes) != len(source_notes):
      raise ValueError("繰り返し音符数が一致しません")

    for target, source in zip(target_notes, source_notes):
      if target["TargetPitchStep"] != source["TargetPitchStep"]:
        raise ValueError("繰り返し音程が一致しません")

      # 入力されなかった音符は歌唱しない
      if source["NoteId"] not in shifts:
        continue

      # 入力音程だけを繰り返し元から取得
      semitones = shifts[source["NoteId"]]

      # 音声は常に再生先の小節から取得
      # これにより歌詞の違いを維持する
      output_start, note_duration = find_note_timing(
          chart, target["NoteId"], 0
      )

      recording_start = output_start + args.offset

      src_s = round(recording_start * sr)
      src_e = round(
          (recording_start + note_duration) * sr
      )

      dst_s = round(output_start * sr)
      dst_e = round(
          (output_start + note_duration) * sr
      )

      if (
          src_s < 0
          or src_e > len(audio)
          or src_e <= src_s
          or dst_e > len(result)
          or dst_e <= dst_s
      ):
        raise ValueError(
            f"音声範囲外: {target['NoteId']}"
        )

      original = audio[src_s:src_e].copy()

      # 各小節の実際の歌詞に音程差を適用
      piece = change_pitch(
          original, sr, semitones
      )

      required = dst_e - dst_s
      if len(piece) != required:
        if abs(len(piece) - required) > 1:
          raise ValueError("音声の長さが一致しません")
        piece = piece[:required]
        if len(piece) < required:
          piece = np.pad(
              piece,
              ((0, required - len(piece)), (0, 0)),
          )

      result[dst_s:dst_e] = piece

    print(
        f"{section['SectionId']}: "
        f"歌詞は自区間、入力音程は{source_id}から取得"
    )

  output_dir = Path("audio_prototype_outputs")
  output_dir.mkdir(exist_ok=True)

  sf.write(
      output_dir / f"full_12bars_input_{input_count}.wav",
      np.clip(result, -1, 1),
      sr,
  )

  print(f"入力音符: {len(input_notes)}")
  print(f"再生時間: {len(result) / sr:.3f}秒")
  print("歌詞を維持した12小節の生成完了！")


if __name__ == "__main__":
  main()
