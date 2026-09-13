import pefile
from PIL import Image
import io

for exe_name in [r"runtime\client\flovmp.exe", r"launcher\src\FloVMP.Connect\bin\Release\net8.0-windows\FloVMP.Connect.exe"]:
    print("=== Checking:", exe_name)
    pe = pefile.PE(exe_name)
    for entry in pe.DIRECTORY_ENTRY_RESOURCE.entries:
        if entry.id == 3: # RT_ICON
            for idx, icon_entry in enumerate(entry.directory.entries):
                data_rva = icon_entry.directory.entries[0].data.struct.OffsetToData
                size = icon_entry.directory.entries[0].data.struct.Size
                data = pe.get_data(data_rva, size)
                if data.startswith(b"\x89PNG"):
                    im = Image.open(io.BytesIO(data)).convert("RGBA")
                    print(f"Icon {idx}: PNG size={im.size}, corner={im.getpixel((0,0))}")
                else:
                    print(f"Icon {idx}: DIB size={size}")
