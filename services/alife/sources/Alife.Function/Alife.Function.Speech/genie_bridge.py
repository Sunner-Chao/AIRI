import os
import sys
import json
import traceback
import io
import glob
import argparse

# Set global standard streams to UTF-8
sys.stdin = io.TextIOWrapper(sys.stdin.buffer, encoding='utf-8', errors='replace')
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')

# Configure GenieData directory path before importing genie_tts (only if not already set)
if "GENIE_DATA_DIR" not in os.environ:
    script_dir = os.path.dirname(os.path.abspath(__file__))
    workspace_root = os.path.abspath(os.path.join(script_dir, "..", ".."))
    runtime_genie_data = os.path.join(workspace_root, "Runtime", "GenieData")
    workspace_genie_data = os.path.join(workspace_root, "GenieData")

    if os.path.exists(runtime_genie_data):
        os.environ["GENIE_DATA_DIR"] = runtime_genie_data
    elif os.path.exists(workspace_genie_data):
        os.environ["GENIE_DATA_DIR"] = workspace_genie_data
    else:
        os.environ["GENIE_DATA_DIR"] = runtime_genie_data

# Monkey-patch jieba_fast to fallback to pure-python jieba if compiled binaries are missing
try:
    import jieba_fast
except ImportError:
    try:
        import jieba
        sys.modules['jieba_fast'] = jieba
    except ImportError:
        pass

import genie_tts as genie
import onnxruntime as ort
from genie_tts.ModelManager import model_manager

available_providers = ort.get_available_providers()
if "CUDAExecutionProvider" in available_providers:
    model_manager.providers = ["CUDAExecutionProvider", "CPUExecutionProvider"]
    print("Genie Bridge: GPU execution enabled (CUDA)!", file=sys.stderr)
elif "DmlExecutionProvider" in available_providers:
    model_manager.providers = ["DmlExecutionProvider", "CPUExecutionProvider"]
    print("Genie Bridge: GPU execution enabled (DirectML)!", file=sys.stderr)
else:
    model_manager.providers = ["CPUExecutionProvider"]

import wave
import numpy as np
from genie_tts.Core.TTSPlayer import TTSPlayer

def trim_silence(audio, threshold=0.01, sample_rate=32000, keep_margin_seconds=0.05):
    if audio is None or len(audio) == 0:
        return audio
    abs_audio = np.abs(audio)
    if abs_audio.ndim > 1:
        abs_audio = abs_audio.max(axis=1)
    indices = np.where(abs_audio > threshold)[0]
    if len(indices) == 0:
        return np.zeros((0, audio.shape[1])) if audio.ndim > 1 else np.zeros((0,))
    start_idx = indices[0]
    end_idx = indices[-1]
    margin = int(sample_rate * keep_margin_seconds)
    start_idx = max(0, start_idx - margin)
    end_idx = min(len(audio), end_idx + margin)
    return audio[start_idx:end_idx]

def patched_save_session_audio(self):
    try:
        trimmed_chunks = []
        for i, chunk in enumerate(self._session_audio_chunks):
            trimmed = trim_silence(chunk, threshold=0.01, sample_rate=self.sample_rate, keep_margin_seconds=0.05)
            if len(trimmed) > 0:
                trimmed_chunks.append(trimmed)
                # Add a small natural pause (150ms of silence) between sentences
                if i < len(self._session_audio_chunks) - 1:
                    pause_samples = int(self.sample_rate * 0.15)
                    pause = np.zeros((pause_samples, chunk.shape[1])) if chunk.ndim > 1 else np.zeros((pause_samples,))
                    trimmed_chunks.append(pause)
        if trimmed_chunks:
            full_audio = np.concatenate(trimmed_chunks, axis=0)
        else:
            full_audio = np.zeros((0, self.channels))
        with wave.open(self._current_save_path, 'wb') as wf:
            wf.setnchannels(self.channels)
            wf.setsampwidth(self.bytes_per_sample)
            wf.setframerate(self.sample_rate)
            wf.writeframes(self._preprocess_for_playback(full_audio))
        print(f"Genie Bridge: Audio successfully saved with silence-trimming to {self._current_save_path}", file=sys.stderr)
    except Exception as e:
        print(f"Genie Bridge: Failed to save audio in patched method: {e}", file=sys.stderr)
        traceback.print_exc(file=sys.stderr)
    finally:
        self._session_audio_chunks = []
        self._current_save_path = None

TTSPlayer._save_session_audio = patched_save_session_audio

import logging
logging.getLogger().setLevel(logging.WARNING)
logging.getLogger("genie_tts").setLevel(logging.WARNING)

def find_reference_audio(model_dir):
    # 1. Check standard prompt_wav.json format
    json_path = os.path.join(model_dir, "prompt_wav.json")
    if os.path.exists(json_path):
        try:
            with open(json_path, "r", encoding="utf-8") as f:
                prompt_wav_dict = json.load(f)
            key = "Normal" if "Normal" in prompt_wav_dict else list(prompt_wav_dict.keys())[0]
            wav_name = prompt_wav_dict[key]["wav"]
            text = prompt_wav_dict[key]["text"]
            
            # Try prompt_wav folder first, then root folder
            wav_path = os.path.join(model_dir, "prompt_wav", wav_name)
            if not os.path.exists(wav_path):
                wav_path = os.path.join(model_dir, wav_name)
            if os.path.exists(wav_path):
                return wav_path, text
        except Exception as e:
            print(f"Error parsing prompt_wav.json: {e}", file=sys.stderr)

    # 2. Check for refer.wav / refer.txt
    wav_path = os.path.join(model_dir, "refer.wav")
    txt_path = os.path.join(model_dir, "refer.txt")
    if os.path.exists(wav_path) and os.path.exists(txt_path):
        with open(txt_path, "r", encoding="utf-8") as f:
            text = f.read().strip()
        return wav_path, text

    # 3. Scan for any .wav and .txt
    wavs = glob.glob(os.path.join(model_dir, "*.wav"))
    txts = glob.glob(os.path.join(model_dir, "*.txt"))
    if wavs and txts:
        wavs.sort()
        txts.sort()
        with open(txts[0], "r", encoding="utf-8") as f:
            text = f.read().strip()
        return wavs[0], text

    # 4. Fallback search inside prompt_wav if exists
    prompt_wav_dir = os.path.join(model_dir, "prompt_wav")
    if os.path.isdir(prompt_wav_dir):
        wavs = glob.glob(os.path.join(prompt_wav_dir, "*.wav"))
        if wavs and txts:
            wavs.sort()
            with open(txts[0], "r", encoding="utf-8") as f:
                text = f.read().strip()
            return wavs[0], text

    raise ValueError(f"Could not find any reference audio wav/text files in {model_dir}")

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--model_dir", required=True)
    args = parser.parse_args()

    model_dir = os.path.abspath(args.model_dir)
    print(f"Genie Bridge: Loading models from {model_dir}", file=sys.stderr)

    # Check if tts_models subfolder exists
    tts_models_dir = os.path.join(model_dir, 'tts_models')
    actual_model_dir = tts_models_dir if os.path.isdir(tts_models_dir) else model_dir

    active_character = 'default'
    try:
        # Check if user model directory has any .onnx files
        has_onnx = False
        if os.path.exists(model_dir):
            for root, dirs, files in os.walk(model_dir):
                if any(f.endswith('.onnx') for f in files):
                    has_onnx = True
                    break

        if has_onnx:
            # Load character ONNX model files
            genie.load_character(
                character_name='default',
                onnx_model_dir=actual_model_dir,
                language='Chinese'
            )

            # Find reference audio and text
            ref_wav, ref_text = find_reference_audio(model_dir)
            print(f"Genie Bridge: Reference Audio: {ref_wav}", file=sys.stderr)
            print(f"Genie Bridge: Reference Text: {ref_text}", file=sys.stderr)

            # Set reference audio
            genie.set_reference_audio(
                character_name='default',
                audio_path=ref_wav,
                audio_text=ref_text,
                language='Chinese'
            )
        else:
            print("Genie Bridge: No custom model found in Runtime/Genie. Loading predefined character 'feibi'...", file=sys.stderr)
            genie.load_predefined_character('feibi')
            active_character = 'feibi'

        print("READY", flush=True)

    except Exception as e:
        print(json.dumps({"status": "error", "message": traceback.format_exc()}), flush=True)
        sys.exit(1)

    for line in sys.stdin:
        if not (line := line.strip()):
            continue
        try:
            req = json.loads(line)
            text = req.get("text", "")
            output_path = req.get("output_path", "")

            if not text or not output_path:
                response = {"status": "error", "message": "text and output_path are required"}
            else:
                # Run TTS synthesis with split_sentence=True
                genie.tts(
                    character_name=active_character,
                    text=text,
                    play=False,
                    split_sentence=True,
                    save_path=output_path
                )
                
                # Check if generated successfully
                if os.path.exists(output_path):
                    response = {"status": "ok", "result": output_path}
                else:
                    response = {"status": "error", "message": "Audio file was not generated"}

        except Exception:
            response = {"status": "error", "message": traceback.format_exc()}

        print(json.dumps(response, ensure_ascii=False), flush=True)

if __name__ == "__main__":
    main()
