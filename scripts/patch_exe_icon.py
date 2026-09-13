# -*- coding: utf-8 -*-
import sys
import os
import struct
import ctypes
import pefile

def patch_exe_icon(exe_path, ico_path=r'assets\branding\app.ico'):
    if not os.path.exists(exe_path):
        print(f'[SKIP] Target exe does not exist: {exe_path}')
        return False
    if not os.path.exists(ico_path):
        print(f'[ERR] Source ico does not exist: {ico_path}')
        return False

    with open(ico_path, 'rb') as f:
        ico_data = f.read()

    reserved, type_, count = struct.unpack_from('<HHH', ico_data, 0)
    if reserved != 0 or type_ != 1:
        raise ValueError(f'Invalid ICO file: {ico_path}')

    entries = []
    offset = 6
    for i in range(count):
        bWidth, bHeight, bColorCount, bReserved, wPlanes, wBitCount, dwBytesInRes, dwImageOffset = struct.unpack_from('<BBBBHHII', ico_data, offset)
        entries.append({
            'bWidth': bWidth,
            'bHeight': bHeight,
            'bColorCount': bColorCount,
            'bReserved': bReserved,
            'wPlanes': wPlanes,
            'wBitCount': wBitCount,
            'dwBytesInRes': dwBytesInRes,
            'dwImageOffset': dwImageOffset,
            'id': i + 1,
            'data': ico_data[dwImageOffset : dwImageOffset + dwBytesInRes]
        })
        offset += 16

    grp_data = bytearray(struct.pack('<HHH', 0, 1, count))
    for entry in entries:
        grp_data.extend(struct.pack('<BBBBHHIH',
            entry['bWidth'], entry['bHeight'], entry['bColorCount'], entry['bReserved'],
            entry['wPlanes'], entry['wBitCount'], entry['dwBytesInRes'], entry['id']))

    group_ids = [1, 101, 102, 103]
    try:
        pe = pefile.PE(exe_path, fast_load=True)
        pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY['IMAGE_DIRECTORY_ENTRY_RESOURCE']])
        if hasattr(pe, 'DIRECTORY_ENTRY_RESOURCE'):
            for res_entry in pe.DIRECTORY_ENTRY_RESOURCE.entries:
                if res_entry.id == 14: # RT_GROUP_ICON
                    detected = [e.id for e in res_entry.directory.entries if isinstance(e.id, int)]
                    if detected:
                        group_ids = detected
        pe.close()
    except Exception:
        pass

    kernel32 = ctypes.windll.kernel32
    kernel32.BeginUpdateResourceW.argtypes = [ctypes.c_wchar_p, ctypes.c_bool]
    kernel32.BeginUpdateResourceW.restype = ctypes.c_void_p

    kernel32.UpdateResourceW.argtypes = [
        ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_ushort, ctypes.c_char_p, ctypes.c_uint
    ]
    kernel32.UpdateResourceW.restype = ctypes.c_bool

    kernel32.EndUpdateResourceW.argtypes = [ctypes.c_void_p, ctypes.c_bool]
    kernel32.EndUpdateResourceW.restype = ctypes.c_bool

    abs_path = os.path.abspath(exe_path)
    hUpdate = kernel32.BeginUpdateResourceW(abs_path, False)
    if not hUpdate:
        err = ctypes.GetLastError()
        print(f'[ERR] BeginUpdateResourceW failed for {exe_path}: error {err}')
        return False

    RT_ICON = ctypes.c_void_p(3)
    RT_GROUP_ICON = ctypes.c_void_p(14)
    LANG_NEUTRAL = 0

    for entry in entries:
        res_id = ctypes.c_void_p(entry['id'])
        data_buf = (ctypes.c_char * len(entry['data'])).from_buffer_copy(entry['data'])
        kernel32.UpdateResourceW(hUpdate, RT_ICON, res_id, LANG_NEUTRAL, data_buf, len(entry['data']))

    grp_buf = (ctypes.c_char * len(grp_data)).from_buffer_copy(grp_data)
    for gid in group_ids:
        kernel32.UpdateResourceW(hUpdate, RT_GROUP_ICON, ctypes.c_void_p(gid), LANG_NEUTRAL, grp_buf, len(grp_data))

    ok = kernel32.EndUpdateResourceW(hUpdate, False)
    if not ok:
        err = ctypes.GetLastError()
        print(f'[ERR] EndUpdateResourceW failed for {exe_path}: error {err}')
        return False

    print(f'[OK] Patched icon in: {exe_path} (groups: {group_ids})')
    return True

if __name__ == '__main__':
    ico = r'assets\branding\app.ico'
    targets = sys.argv[1:]
    if not targets:
        targets = [
            r'runtime\server\flovmp-server.exe',
            r'runtime\server\flovmp-crash-handler.exe',
            r'runtime\client\flovmp.exe',
            r'dist\scaffold\server\flovmp-server.exe',
            r'dist\scaffold\server\flovmp-crash-handler.exe',
            r'C:\TEST-FLOVMP\server\flovmp-server.exe',
            r'C:\TEST-FLOVMP\server\flovmp-crash-handler.exe',
        ]
    for t in targets:
        patch_exe_icon(t, ico)
