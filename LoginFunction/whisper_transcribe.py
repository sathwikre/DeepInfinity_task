"""
whisper_transcribe.py
Usage: python whisper_transcribe.py <audio_file_path> [model_name]

Transcribes the given audio file using OpenAI Whisper (local, no cloud).
Prints ONLY the transcript text to stdout — nothing else.
All Whisper progress/info/warnings are redirected to stderr.
Exit code 0 = success, non-zero = failure (error message on stderr).
"""

import sys
import os
import warnings
import logging


def main():
    if len(sys.argv) < 2:
        print("Usage: whisper_transcribe.py <audio_file_path> [model_name]", file=sys.stderr)
        sys.exit(1)

    audio_path = sys.argv[1]
    model_name = sys.argv[2] if len(sys.argv) >= 3 else "base"

    if not os.path.isfile(audio_path):
        print(f"File not found: {audio_path}", file=sys.stderr)
        sys.exit(2)

    # Suppress all Python warnings (FP16, deprecation, etc.)
    warnings.filterwarnings("ignore")

    # Suppress all logging output from whisper and its dependencies to stdout.
    logging.basicConfig(stream=sys.stderr, level=logging.ERROR)
    for name in ("whisper", "whisper.transcribe", "numba", "torch"):
        logging.getLogger(name).setLevel(logging.ERROR)

    try:
        import whisper

        # Temporarily redirect stdout → stderr while loading and transcribing,
        # so any print() calls inside Whisper (e.g. "Detected language: English")
        # don't end up in the transcript that C# reads.
        real_stdout = sys.stdout
        sys.stdout = sys.stderr

        try:
            model = whisper.load_model(model_name)
            # fp16=False: avoids UserWarning on CPU-only machines.
            # verbose=None: suppresses per-segment printing inside transcribe().
            result = model.transcribe(audio_path, fp16=False, verbose=None)
        finally:
            # Always restore stdout before we print the transcript.
            sys.stdout = real_stdout

        transcript = result.get("text", "").strip()

        # This is the ONLY thing written to stdout — C# reads exactly this line.
        print(transcript)
        sys.exit(0)

    except Exception as e:
        print(f"Whisper error: {e}", file=sys.stderr)
        sys.exit(3)


if __name__ == "__main__":
    main()
