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
                        group_ids = sorted(list(set(detected + [1, 101, 102, 103])))
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

WIN_DEBUG_PAT = b'\x89\xd0\x34\x01\x08\xc8\xc3'
WIN_DEBUG_REP = b'\xb0\x01\xc3\x90\x90\x90\x90' # mov al, 1; ret; nop * 4

LIN_DEBUG_PAT = b'\x89\xf0\x34\x01\x40\x08\xf8\xc3'
LIN_DEBUG_REP = b'\xb0\x01\xc3\x90\x90\x90\x90\x90' # mov al, 1; ret; nop * 5

SENTRY_DSN = b'https://586f9304db234ff7bc949b843d4f92dd@sentry-alt.com/4'

def patch_binary_logic(path):
    if not os.path.exists(path):
        return False
    with open(path, 'rb') as f:
        data = bytearray(f.read())
    
    modified = False
    if SENTRY_DSN in data:
        idx = data.index(SENTRY_DSN)
        data[idx : idx + len(SENTRY_DSN)] = b'\x00' * len(SENTRY_DSN)
        print(f'  [Sentry] Zeroed DSN in {os.path.basename(path)}')
        modified = True
        
    if WIN_DEBUG_PAT in data:
        idx = data.index(WIN_DEBUG_PAT)
        data[idx : idx + len(WIN_DEBUG_REP)] = WIN_DEBUG_REP
        print(f'  [DebugFix] Patched Windows DEBUG_NOT_ALLOWED check in {os.path.basename(path)}')
        modified = True
    elif WIN_DEBUG_REP in data:
        print(f'  [DebugFix] Already patched Windows check in {os.path.basename(path)}')
        
    if LIN_DEBUG_PAT in data:
        idx = data.index(LIN_DEBUG_PAT)
        data[idx : idx + len(LIN_DEBUG_REP)] = LIN_DEBUG_REP
        print(f'  [DebugFix] Patched Linux DEBUG_NOT_ALLOWED check in {os.path.basename(path)}')
        modified = True
    elif LIN_DEBUG_REP in data:
        print(f'  [DebugFix] Already patched Linux check in {os.path.basename(path)}')
        
    if modified:
        with open(path, 'wb') as f:
            f.write(data)
    return True

def flush_shell_cache():
    try:
        ctypes.windll.shell32.SHChangeNotify(0x08000000, 0, None, None)
        print('[Shell] Notified Windows Explorer to refresh icon cache.')
    except Exception as e:
        print(f'[Shell] Warning: {e}')

def patch_all_targets(ico=r'assets\branding\app.ico'):
    targets = [
        r'runtime\server\flovmp-server.exe',
        r'runtime\server\flovmp-crash-handler.exe',
        r'runtime\client\flovmp.exe',
        r'runtime\client\FloVMP.Connect.exe',
        r'launcher\native-dist\FloVMP.Connect.exe',
        r'launcher\electron\native-dist\FloVMP.Connect.exe',
        r'dist\scaffold\server\flovmp-server.exe',
        r'dist\scaffold\server\flovmp-crash-handler.exe',
        r'dist\scaffold\server\flovmp-server',
        r'C:\TEST-FLOVMP\server\flovmp-server.exe',
        r'C:\TEST-FLOVMP\server\flovmp-crash-handler.exe',
    ]
    for t in targets:
        if os.path.exists(t):
            patch_binary_logic(t)
            if t.lower().endswith('.exe'):
                patch_exe_icon(t, ico)
    flush_shell_cache()

if __name__ == '__main__':
    ico = r'assets\branding\app.ico'
    cli_targets = sys.argv[1:]
    if cli_targets:
        for t in cli_targets:
            if os.path.exists(t):
                patch_binary_logic(t)
                if t.lower().endswith('.exe'):
                    patch_exe_icon(t, ico)
        flush_shell_cache()
    else:
        patch_all_targets(ico)
