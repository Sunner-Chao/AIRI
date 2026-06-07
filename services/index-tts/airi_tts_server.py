import argparse
import json
import os
import tempfile
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse


MODEL_ID = "IndexTTS-1.5"
MODEL = None
MODEL_LOCK = threading.Lock()
INFER_LOCK = threading.Lock()


def wav_headers(origin: str | None = None) -> dict[str, str]:
    headers = {
        "Access-Control-Allow-Methods": "GET,POST,OPTIONS",
        "Access-Control-Allow-Headers": "content-type,authorization",
    }
    if origin:
        headers["Access-Control-Allow-Origin"] = origin
    else:
        headers["Access-Control-Allow-Origin"] = "*"
    return headers


def load_tts(model_dir: Path, device: str | None):
    global MODEL
    with MODEL_LOCK:
        if MODEL is None:
            from indextts.infer import IndexTTS

            MODEL = IndexTTS(
                cfg_path=str(model_dir / "config.yaml"),
                model_dir=str(model_dir),
                use_cuda_kernel=False,
                device=device,
            )
        return MODEL


def list_voices(voices_dir: Path) -> dict[str, dict[str, str]]:
    voices_dir.mkdir(parents=True, exist_ok=True)
    voices: dict[str, dict[str, str]] = {}
    for path in sorted(voices_dir.glob("*.wav")):
        if path.stat().st_size > 1024:
            voices[path.stem] = {
                "name": path.stem,
                "path": str(path),
            }
    return voices


class Handler(BaseHTTPRequestHandler):
    server_version = "AIRIIndexTTS/1.0"

    def _send_headers(self, status: int, content_type: str) -> None:
        self.send_response(status)
        origin = self.headers.get("origin")
        for key, value in wav_headers(origin).items():
            self.send_header(key, value)
        self.send_header("content-type", content_type)
        self.end_headers()

    def _json(self, status: int, payload: object) -> None:
        self._send_headers(status, "application/json; charset=utf-8")
        self.wfile.write(json.dumps(payload, ensure_ascii=False).encode("utf-8"))

    def do_OPTIONS(self) -> None:
        self._send_headers(204, "text/plain")

    def do_GET(self) -> None:
        path = urlparse(self.path).path.rstrip("/")
        if path == "/tts/audio/voices":
            self._json(200, list_voices(self.server.voices_dir))  # type: ignore[attr-defined]
            return
        if path == "/health":
            self._json(200, {"ok": True})
            return
        self._json(404, {"error": "not found"})

    def do_POST(self) -> None:
        path = urlparse(self.path).path.rstrip("/")
        if path != "/tts/audio/speech":
            self._json(404, {"error": "not found"})
            return

        try:
            length = int(self.headers.get("content-length", "0"))
            payload = json.loads(self.rfile.read(length).decode("utf-8"))
            text = payload.get("input")
            voice = payload.get("voice")
            model = payload.get("model") or MODEL_ID

            if model != MODEL_ID:
                self._json(400, {"error": f"unsupported model: {model}"})
                return
            if not isinstance(text, str) or not text.strip():
                self._json(400, {"error": "input is required"})
                return
            if not isinstance(voice, str) or not voice.strip():
                self._json(400, {"error": "voice is required"})
                return

            voices = list_voices(self.server.voices_dir)  # type: ignore[attr-defined]
            voice_info = voices.get(voice)
            if not voice_info:
                self._json(400, {"error": f"unknown voice: {voice}"})
                return

            with INFER_LOCK:
                tts = load_tts(self.server.model_dir, self.server.device)  # type: ignore[attr-defined]
                with tempfile.NamedTemporaryFile(suffix=".wav", delete=False, dir=self.server.tmp_dir) as tmp:  # type: ignore[attr-defined]
                    output_path = tmp.name

                try:
                    tts.infer(
                        audio_prompt=voice_info["path"],
                        text=text,
                        output_path=output_path,
                        verbose=True,
                    )
                    data = Path(output_path).read_bytes()
                finally:
                    try:
                        os.remove(output_path)
                    except OSError:
                        pass

            self._send_headers(200, "audio/wav")
            self.wfile.write(data)
        except Exception as err:
            self._json(500, {"error": str(err)})


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", default=11996, type=int)
    parser.add_argument("--model-dir", default="checkpoints")
    parser.add_argument("--voices-dir", default="voices")
    parser.add_argument("--tmp-dir", default="tmp")
    parser.add_argument("--device", default="cpu", help="Use cpu to avoid small-GPU OOM, or cuda for GPU inference")
    args = parser.parse_args()

    server = ThreadingHTTPServer((args.host, args.port), Handler)
    server.model_dir = Path(args.model_dir).resolve()
    server.voices_dir = Path(args.voices_dir).resolve()
    server.tmp_dir = Path(args.tmp_dir).resolve()
    server.device = args.device
    server.tmp_dir.mkdir(parents=True, exist_ok=True)

    print(f"AIRI IndexTTS server: http://{args.host}:{args.port}/tts/")
    print(f"Model dir: {server.model_dir}")
    print(f"Voices dir: {server.voices_dir}")
    print(f"Device: {server.device}")
    server.serve_forever()


if __name__ == "__main__":
    main()
