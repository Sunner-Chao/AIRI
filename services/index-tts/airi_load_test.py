import traceback
from pathlib import Path

log = Path("tmp/load-test-python.log")
log.parent.mkdir(parents=True, exist_ok=True)

try:
    from indextts.infer import IndexTTS

    log.write_text("class imported\n", encoding="utf-8")
    tts = IndexTTS(cfg_path="checkpoints/config.yaml", model_dir="checkpoints", use_cuda_kernel=False, device="cpu")
    log.write_text("loaded\n", encoding="utf-8")
except Exception:
    log.write_text(traceback.format_exc(), encoding="utf-8")
    raise
