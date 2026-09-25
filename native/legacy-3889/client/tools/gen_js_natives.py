"""Генератор src/js_natives.inc — таблица нативных функций GTA V для mp.game.*.

    python tools/gen_js_natives.py <путь к ScriptHookV_SDK/inc/natives.h>

Клиентский JS вызывает нативы так же, как в RAGE:MP:

    mp.game.graphics.drawRect(0.5, 0.5, 0.1, 0.1, 255, 0, 0, 200);
    const pos = mp.game.entity.getEntityCoords(ped, true);   // {x, y, z}
    const r = mp.game.gameplay.getGroundZFor3dCoord(x, y, z, false); // {result, groundZ}

Имя пространства — имя из SDK строчными буквами, имя функции — camelCase от
имени натива без ведущего подчёркивания. Безымянные нативы (_0x1234ABCD)
остаются как есть. Параметры-указатели в JS не передаются: такой натив
возвращает объект { result, <имя параметра>: значение }.

В таблицу попадают только факты — имя, хэш, типы и имена выходных
параметров. Кода и текста SDK ScriptHookV в ней нет, и распространять сам SDK
не нужно.
"""
import pathlib
import re
import sys

# Типы, которые в стеке аргументов занимают одно 64-битное целое.
INT_TYPES = {"int", "Any", "BOOL", "Ped", "Vehicle", "Entity", "Hash", "Player", "Object", "Blip", "Cam",
             "ScrHandle", "Pickup", "Interior", "FireId", "Train", "Weapon"}
LINE = re.compile(r"static\s+(?P<ret>[A-Za-z0-9_]+\s*\*?)\s+(?P<name>[A-Za-z0-9_]+)\s*\((?P<params>[^)]*)\)\s*\{.*?"
                  r"invoke<[^>]*>\(\s*(?P<hash>0x[0-9A-Fa-f]+)")


def arg_code(typ):
    typ = typ.replace(" ", "")
    if typ == "float":
        return "f"
    if typ in ("char*", "constchar*"):
        return "s"
    if typ == "BOOL":
        return "b"
    if typ == "Vector3*":
        return "V"          # выход: {x, y, z}
    if typ == "float*":
        return "F"          # выход: число с плавающей точкой
    if typ.endswith("*"):
        return "I"          # выход: целое
    if typ in INT_TYPES:
        return "i"
    return None


def ret_code(typ):
    typ = typ.replace(" ", "")
    return {"void": "v", "float": "f", "BOOL": "b", "char*": "s", "Vector3": "V"}.get(
        typ, "i" if (typ in INT_TYPES or typ.endswith("*")) else None)


def camel(name):
    if re.fullmatch(r"_0x[0-9A-Fa-f]+", name):
        return name
    parts = [p for p in name.lstrip("_").split("_") if p]
    if not parts:
        return name
    return parts[0].lower() + "".join(p[:1].upper() + p[1:].lower() for p in parts[1:])


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    src = pathlib.Path(sys.argv[1]).read_text(encoding="utf-8", errors="replace")
    namespace = None
    rows, skipped, seen = [], 0, set()
    for line in src.splitlines():
        m = re.match(r"\s*namespace\s+([A-Z0-9_]+)", line)
        if m:
            namespace = m.group(1).lower()
            continue
        m = LINE.search(line)
        if not m or not namespace:
            continue
        ret = ret_code(m.group("ret"))
        codes, outs = [], []
        ok = ret is not None
        params = m.group("params").strip()
        for raw in ([p.strip() for p in params.split(",")] if params else []):
            pm = re.match(r"(?P<type>.+?)\s*(?P<pname>[A-Za-z0-9_]+)$", raw)
            if not pm:
                ok = False
                break
            code = arg_code(pm.group("type"))
            if code is None:
                ok = False
                break
            codes.append(code)
            if code in "VFI":
                outs.append(pm.group("pname"))
        if not ok:
            skipped += 1
            continue
        js = camel(m.group("name"))
        key = (namespace, js)
        if key in seen:
            skipped += 1
            continue
        seen.add(key)
        rows.append((namespace, js, int(m.group("hash"), 16), "".join(codes), ret, ",".join(outs)))

    out = [
        "// Сгенерировано tools/gen_js_natives.py — не править руками.",
        "// Пространство, имя, хэш, типы аргументов, тип результата, имена выходных параметров.",
        "// Аргументы: i целое, b флаг, f float, s строка; выход: I целое, F float, V {x,y,z}.",
        "// Результат: v ничего, i целое, b флаг, f float, s строка, V {x,y,z}.",
    ]
    for ns, js, h, codes, ret, outs in rows:
        out.append('{{ "{}", "{}", 0x{:016X}ull, "{}", \'{}\', "{}" }},'.format(ns, js, h, codes, ret, outs))
    target = pathlib.Path(__file__).resolve().parent.parent / "src" / "js_natives.inc"
    target.write_text("\n".join(out) + "\n", encoding="utf-8")
    print("нативов: {}, пропущено: {}, файл: {}".format(len(rows), skipped, target))


if __name__ == "__main__":
    main()
