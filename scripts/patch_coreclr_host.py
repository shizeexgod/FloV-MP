import os
import sys

def patch_coreclr_module(file_path):
    if not os.path.exists(file_path):
        print(f"[WARN] File not found: {file_path}")
        return False
        
    with open(file_path, "rb") as f:
        data = f.read()
        
    orig_ascii = b"AltV.Net.Host"
    repl_ascii = b"FloV.Net.Host"
    orig_u16 = orig_ascii.decode("ascii").encode("utf-16le")
    repl_u16 = repl_ascii.decode("ascii").encode("utf-16le")
    
    count_ascii = data.count(orig_ascii)
    count_u16 = data.count(orig_u16)
    
    if count_ascii == 0 and count_u16 == 0:
        print(f"[INFO] Already patched or no target strings in {file_path}")
        return True
        
    data = data.replace(orig_ascii, repl_ascii)
    data = data.replace(orig_u16, repl_u16)
    
    with open(file_path, "wb") as f:
        f.write(data)
        
    print(f"[OK] Patched {os.path.basename(file_path)}: {count_ascii} ASCII, {count_u16} UTF-16 references replaced.")
    return True

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: patch_coreclr_host.py <path_to_module>")
        sys.exit(1)
    for arg in sys.argv[1:]:
        patch_coreclr_module(arg)
