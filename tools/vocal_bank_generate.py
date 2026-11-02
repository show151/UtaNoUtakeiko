
import argparse
import json
from pathlib import Path

import numpy as np
import soundfile as sf

from vocal_pitch_probe import find_note_timing
from vocal_full_probe import playable_notes, change_pitch


def main():
  parser = argparse.ArgumentParser()
  parser.add_argument("wav", type=Path)
  parser.add_argument("--offset", type=float, default=0)
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
      key=lambda s: s["StartMeasure"]
  )
  by_id = {s["SectionId"]: s for s in sections}
  repeats = {
      m["TargetSectionId"]: m["SourceSectionId"]
      for m in chart["RepeatMappings"]
  }

  out = Path("audio_prototype_outputs/vocal_bank")
  out.mkdir(parents=True, exist_ok=True)

  metadata = {
      "songId": chart["SongId"],
      "sampleRate": sr,
      "channels": audio.shape[1],
      "notes": [],
  }

  for section in sections:
    target_notes = playable_notes(section)

    source_id = repeats.get(
        section["SectionId"],
        section["SectionId"]
    )
    source_notes = playable_notes(by_id[source_id])

    if len(target_notes) != len(source_notes):
      raise ValueError("繰り返し音符数が一致しません")

    for target, source in zip(target_notes, source_notes):
      if target["TargetPitchStep"] != source["TargetPitchStep"]:
        raise ValueError("繰り返し音程が一致しません")

      note_id = target["NoteId"]

      start, duration = find_note_timing(
          chart, note_id, args.offset
      )

      s = round(start * sr)
      e = round((start + duration) * sr)

      if s < 0 or e > len(audio) or s >= e:
        raise ValueError(f"音声範囲外: {note_id}")

      original = audio[s:e].copy()

      entry = {
          "noteId": note_id,
          "sourceNoteId": source["NoteId"],
          "startSample": round(
              (start - args.offset) * sr
          ),
          "sampleCount": e - s,
          "targetPitchStep": target["TargetPitchStep"],
          "variants": {},
      }

      for captured_pitch in range(10):
        delta = (
            captured_pitch
            - target["TargetPitchStep"]
        )

        shifted = change_pitch(
            original, sr, delta
        )

        filename = (
            f"{note_id}_p{captured_pitch}.wav"
        )

        sf.write(
            out / filename,
            np.clip(shifted, -1, 1),
            sr,
            subtype="PCM_16"
        )

        entry["variants"][str(captured_pitch)] = filename

      metadata["notes"].append(entry)
      print(f"生成完了: {note_id}")

  beat_length = 60 / chart["Bpm"]
  beats_per_measure = (
      chart["TimeSignatureNumerator"] * 4
      / chart["TimeSignatureDenominator"]
  )
  metadata["durationSeconds"] = (
      max(s["EndMeasure"] for s in sections)
      * beats_per_measure * beat_length
  )

  (out / "metadata.json").write_text(
      json.dumps(metadata, ensure_ascii=False, indent=2),
      encoding="utf-8"
  )

  print(f"歌唱音符数: {len(metadata['notes'])}")
  print(f"WAV数: {len(metadata['notes']) * 10}")
  print(f"出力先: {out}")


if __name__ == "__main__":
  main()
