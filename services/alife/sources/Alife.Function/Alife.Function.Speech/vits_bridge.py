# coding=utf-8
import sys, os, json, traceback, io
import numpy as np
import torch
from torch import no_grad, LongTensor

# Force UTF-8 I/O
sys.stdin = io.TextIOWrapper(sys.stdin.buffer, encoding='utf-8', errors='replace')
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')

# VITS src directory is passed as the first command-line argument by the C# host.
if len(sys.argv) < 2:
    sys.stderr.write('ERROR: VITS src directory not provided as argv[1]\n')
    sys.exit(1)
BASE_DIR = os.path.abspath(sys.argv[1])
sys.path.insert(0, BASE_DIR)

from models import SynthesizerTrn
from text import text_to_sequence
import commons
import utils

# ---------------------------------------------------------------------------
# Globals – populated in main() before READY is printed
# ---------------------------------------------------------------------------
device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
hps_ms = None
net_g_ms = None


def get_text(text, hps):
    """Convert text to token tensor (mirrors app.py)."""
    text_norm, clean_text = text_to_sequence(text, hps.symbols, hps.data.text_cleaners)
    if hps.data.add_blank:
        text_norm = commons.intersperse(text_norm, 0)
    text_norm = LongTensor(text_norm)
    return text_norm, clean_text


def vits(text, language, speaker_id, noise_scale, noise_scale_w, length_scale):
    text = text.replace('\n', ' ').replace('\r', '').replace(' ', '')
    if language == 0:
        text = f'[ZH]{text}[ZH]'
    elif language == 1:
        text = f'[JA]{text}[JA]'
    stn_tst, _ = get_text(text, hps_ms)
    with no_grad():
        x_tst = stn_tst.unsqueeze(0).to(device)
        x_tst_lengths = LongTensor([stn_tst.size(0)]).to(device)
        sid = LongTensor([speaker_id]).to(device)
        audio = net_g_ms.infer(
            x_tst, x_tst_lengths, sid=sid,
            noise_scale=noise_scale,
            noise_scale_w=noise_scale_w,
            length_scale=length_scale
        )[0][0, 0].data.cpu().float().numpy()
    return 22050, audio


def main():
    global hps_ms, net_g_ms, device

    # Redirect stdout → stderr during model loading so PyTorch logs
    # don't pollute the JSON channel read by C#.
    _real_stdout = sys.stdout
    sys.stdout = sys.stderr
    try:
        hps_ms = utils.get_hparams_from_file(f'{BASE_DIR}/model/config.json')
        net_g_ms = SynthesizerTrn(
            len(hps_ms.symbols),
            hps_ms.data.filter_length // 2 + 1,
            hps_ms.train.segment_size // hps_ms.data.hop_length,
            n_speakers=hps_ms.data.n_speakers,
            **hps_ms.model)
        _ = net_g_ms.eval().to(device)
        utils.load_checkpoint(f'{BASE_DIR}/model/G_953000.pth', net_g_ms, None)
    finally:
        sys.stdout = _real_stdout

    print('READY', flush=True)

    for line in sys.stdin:
        if not (line := line.strip()):
            continue
        try:
            req = json.loads(line)
            text        = req.get('text', '')
            output_path = req.get('output_path', '')
            speaker_id  = int(req.get('speaker_id', 0))
            noise_scale   = float(req.get('noise_scale', 0.6))
            noise_scale_w = float(req.get('noise_scale_w', 0.668))
            length_scale  = float(req.get('length_scale', 1.2))
            if not text or not output_path:
                raise ValueError('text and output_path are required')
            sr, audio = vits(text, 0, speaker_id, noise_scale, noise_scale_w, length_scale)
            audio_int16 = (audio * 32767).astype(np.int16)
            import wave
            with wave.open(output_path, 'wb') as wf:
                wf.setnchannels(1)
                wf.setsampwidth(2)
                wf.setframerate(sr)
                wf.writeframes(audio_int16.tobytes())
            response = {'status': 'ok', 'result': output_path}
        except Exception:
            response = {'status': 'error', 'message': traceback.format_exc()}
        print(json.dumps(response, ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()
