import os
import struct
import io
import numpy as np
from PIL import Image

def generate_brand_logos():
    src_path = r"web\public\branding\5230943968516841121_121.jpg"
    if not os.path.exists(src_path):
        src_path = r"archive\FloridaV.Launcher-WPF\Assets\florida_logo.png"
        
    print(f"Loading base logo from {src_path}...")
    im = Image.open(src_path).convert("RGB")
    arr = np.array(im, dtype=float)
    
    # Background is black. Calculate alpha mask
    max_c = arr.max(axis=2)
    alpha = np.clip((max_c - 4.0) / (28.0 - 4.0), 0.0, 1.0)
    
    # De-multiply RGB to eliminate black border/fringe
    rgb = arr.copy()
    mask = alpha > 0.001
    for c in range(3):
        rgb[mask, c] = np.clip(rgb[mask, c] / np.maximum(alpha[mask], 0.3), 0, 255)
        
    rgba = np.dstack([rgb.astype(np.uint8), (alpha * 255).astype(np.uint8)])
    base_cutout = Image.fromarray(rgba, "RGBA")
    
    # Crop to content and place on square canvas with 6% margin
    bbox = base_cutout.getbbox()
    cropped = base_cutout.crop(bbox)
    w, h = cropped.size
    dim = max(w, h) + int(max(w, h) * 0.06)
    square_im = Image.new("RGBA", (dim, dim), (0, 0, 0, 0))
    offset = ((dim - w) // 2, (dim - h) // 2)
    square_im.paste(cropped, offset, cropped)
    
    # Helper to save resized PNG
    def save_png(path, size):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        res = square_im.resize((size, size), Image.Resampling.LANCZOS)
        res.save(path, format="PNG", optimize=True)
        print(f"  [PNG] Saved {path} ({size}x{size})")
        
    # Helper to create PNG-encoded ICO (100% transparent on Windows 10/11 taskbar)
    def save_png_ico(path, sizes):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        images_data = []
        for s in sizes:
            res = square_im.resize((s, s), Image.Resampling.LANCZOS)
            buf = io.BytesIO()
            res.save(buf, format="PNG", optimize=True)
            images_data.append((s, buf.getvalue()))
            
        ico_bytes = bytearray(struct.pack("<HHH", 0, 1, len(sizes)))
        offset = 6 + 16 * len(sizes)
        for s, data in images_data:
            w_b = s if s < 256 else 0
            h_b = s if s < 256 else 0
            entry = struct.pack("<BBBBHHII", w_b, h_b, 0, 0, 1, 32, len(data), offset)
            ico_bytes.extend(entry)
            offset += len(data)
            
        for _, data in images_data:
            ico_bytes.extend(data)
            
        with open(path, "wb") as f:
            f.write(ico_bytes)
        print(f"  [ICO] Saved {path} (PNG-frames: {sizes})")

    png_targets = {
        r"assets\branding\flovmp_logo.png": 1024,
        r"assets\branding\flovmp_logo_512.png": 512,
        r"assets\branding\flovmp_logo_256.png": 256,
        r"assets\branding\flovmp_logo_128.png": 128,
        r"assets\branding\flovmp_logo_64.png": 64,
        r"assets\branding\flovmp_logo_32.png": 32,
        r"launcher\electron\build\icon.png": 512,
        r"launcher\electron\build\icon-256.png": 256,
        r"launcher\electron\src\renderer\assets\logo.png": 512,
        r"launcher\connect-ui\logo.png": 256,
        r"runtime\client\ui\logo.png": 256,
        r"runtime\client\ui\favicon.png": 64,
        r"web\public\branding\logo.png": 512,
        r"web\public\branding\avatar-logo.png": 256,
        r"web\public\branding\logo-codex.png": 1024,
        r"archive\FloridaV.Launcher-WPF\Assets\flovmp_logo.png": 1024,
        r"archive\FloridaV.Launcher-WPF\Assets\florida_logo.png": 1024,
    }
    
    print("Writing PNG logos...")
    for p, s in png_targets.items():
        save_png(p, s)
        
    ico_targets = [
        r"assets\branding\app.ico",
        r"launcher\src\FloVMP.Connect\app.ico",
        r"server\src\FloVMP.ServerLauncher\app.ico",
        r"launcher\electron\build\icon.ico",
        r"launcher\electron\dist\.icon-ico\icon.ico",
        r"runtime\client\ui\favicon.ico",
    ]
    
    print("Writing ICO files...")
    ico_sizes = [256, 128, 64, 48, 32, 24, 16]
    for p in ico_targets:
        save_png_ico(p, ico_sizes)
        
    print("\nAll brand logos updated with 100% transparent PNG/ICO!")

if __name__ == "__main__":
    generate_brand_logos()
