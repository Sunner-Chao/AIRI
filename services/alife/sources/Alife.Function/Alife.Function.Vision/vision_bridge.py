import sys, json, torch, traceback, io

# 强制全局使用 UTF-8 编码，防止 Windows GBK 干扰
sys.stdin = io.TextIOWrapper(sys.stdin.buffer, encoding='utf-8', errors='replace')
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')

from PIL import Image
from transformers import Qwen2_5_VLForConditionalGeneration, AutoProcessor, BitsAndBytesConfig
from qwen_vl_utils import process_vision_info

# 核心设备选择逻辑
device = None
if torch.cuda.is_available():
    device = torch.device("cuda")
    print(f"USING DEVICE: CUDA ({torch.cuda.get_device_name(0)})", file=sys.stderr)
else:
    print("WARNING: CUDA NOT FOUND. Vision Large Model is disabled.", file=sys.stderr)

"""
Vision Bridge - Qwen2.5-VL-3B-Instruct 稳定版 (4-bit 量化, 兼容 4GB 显存)
"""

def load_model(path):
    if device is None:
        return None, None
        
    try:
        quantization_config = BitsAndBytesConfig(
            load_in_4bit=True,
            bnb_4bit_compute_dtype=torch.bfloat16,
            bnb_4bit_use_double_quant=True,
            bnb_4bit_quant_type="nf4"
        )

        model = Qwen2_5_VLForConditionalGeneration.from_pretrained(
            path,
            dtype="auto",
            quantization_config=quantization_config,
            device_map="auto",
            attn_implementation="sdpa"  # 新增
        )
        
        # 限制图片分辨率，控制显存占用
        processor = AutoProcessor.from_pretrained(
            path,
            min_pixels=256 * 28 * 28,
            max_pixels=512 * 28 * 28
        )
        
        return model, processor
    except Exception as e:
        print(f"Failed to load model: {e}", file=sys.stderr)
        raise e

def query(model, processor, req):
    if model is None:
        return {"status": "ok", "result": "[AI 深度视觉分析已禁用：未检测到兼容的 NVIDIA GPU 或 CUDA 环境]"}

    path = req.get("image_path")
    if not path: return {"status": "error", "message": "image_path is required"}
    
    question = req.get("question", "请详细描述这张图片。")
    max_tokens = req.get("max_new_tokens", 512)
    
    # 用 PIL 显式加载图片，避免 Windows 路径问题
    image = Image.open(path).convert("RGB")
    
    messages = [
        {
            "role": "user",
            "content": [
                {
                    "type": "image",
                    "image": image,
                },
                {"type": "text", "text": question},
            ],
        }
    ]
    
    text = processor.apply_chat_template(
        messages, tokenize=False, add_generation_prompt=True
    )
    
    image_inputs, video_inputs = process_vision_info(messages)
    
    inputs = processor(
        text=[text],
        images=image_inputs,
        videos=video_inputs,
        padding=True,
        return_tensors="pt",
    )
    inputs = inputs.to(device)
    
    with torch.no_grad():
        generated_ids = model.generate(**inputs, max_new_tokens=max_tokens)
        generated_ids_trimmed = [
            out_ids[len(in_ids) :] for in_ids, out_ids in zip(inputs.input_ids, generated_ids)
        ]
        res = processor.batch_decode(
            generated_ids_trimmed, skip_special_tokens=True, clean_up_tokenization_spaces=False
        )
    
    # 推理完成后释放缓存
    del inputs, generated_ids
    torch.cuda.empty_cache()
        
    return {"status": "ok", "result": res[0].strip() if res else ""}

if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument("--model_path", required=True)
    args = parser.parse_args()

    try:
        model, processor = load_model(args.model_path)
        print("READY", flush=True)
    except Exception:
        print(json.dumps({"status": "error", "message": traceback.format_exc()}), flush=True)
        sys.exit(1)

    for line in sys.stdin:
        if not (line := line.strip()): continue
        try:
            req = json.loads(line)
            response = query(model, processor, req)
        except Exception:
            response = {"status": "error", "message": traceback.format_exc()}
        print(json.dumps(response, ensure_ascii=False), flush=True)
