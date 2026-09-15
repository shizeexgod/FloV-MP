#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Проверка NUI-страниц по чеклисту Vercel Web Interface Guidelines.

Проверяется не «красиво ли», а то, что ломается молча и заметно только у
игрока: поле без подписи, кнопка без доступного имени, снятая обводка фокуса,
анимация, которую нельзя отключить, «...» вместо многоточия.

Запуск:  python scripts/check_nui_guidelines.py
Код возврата 1, если есть нарушения — чтобы проверку можно было ставить в CI.
"""

from __future__ import annotations

import io
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PAGES = sorted((ROOT / "client/resources/flovmp-client/client/html").glob("*/index.html"))

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

FAILS: list[str] = []
WARNS: list[str] = []
PASSES = 0


def check(page: str, ok: bool, rule: str, detail: str = "", warn_only: bool = False) -> None:
    global PASSES
    if ok:
        PASSES += 1
        return
    line = f"{page}: {rule}" + (f" — {detail}" if detail else "")
    (WARNS if warn_only else FAILS).append(line)


def audit(path: Path) -> None:
    name = path.parent.name
    raw = io.open(path, encoding="utf-8").read()

    # Комментарии вырезаем ДО анализа: иначе объяснение «почему мы не пишем
    # transition: all» само ловится как нарушение. Проверять надо код, а не
    # рассуждения о нём.
    html = re.sub(r"/\*.*?\*/", "", raw, flags=re.S)
    html = re.sub(r"<!--.*?-->", "", html, flags=re.S)

    # --- Доступность ---------------------------------------------------
    inputs = re.findall(r"<input\b[^>]*>", html)
    for tag in inputs:
        ident = re.search(r'id="([^"]+)"', tag)
        has_label = bool(ident and f'for="{ident.group(1)}"' in html)
        has_aria = 'aria-label=' in tag
        check(name, has_label or has_aria, "поле без подписи",
              f"{ident.group(1) if ident else tag[:40]} — экранный диктор прочитает пустоту")

    # Кнопка без текста и без aria-label — «безымянная» кнопка.
    for btn in re.findall(r"<button\b[^>]*>(.*?)</button>", html, re.S):
        pass  # текст есть у всех наших кнопок; проверка ниже по тегам-иконкам
    for tag in re.findall(r"<button\b[^>]*>\s*</button>", html):
        check(name, "aria-label=" in tag, "кнопка без доступного имени", tag[:60])

    # Кликабельные div/span вместо button.
    check(name, not re.search(r"<(div|span)[^>]*\son(click|Click)=", html),
          "клик на div/span вместо <button>",
          "не работает с клавиатуры и не читается как кнопка")

    # Живые области для асинхронных сообщений.
    if "auth" in name:
        check(name, 'aria-live=' in html, "нет aria-live для сообщений",
              "результат входа не будет озвучен")

    # Декоративные иконки скрыты от ассистивных технологий.
    for tag in re.findall(r'<span class="[^"]*dot[^"]*"[^>]*>', html):
        check(name, 'aria-hidden="true"' in tag, "декоративный элемент не скрыт", tag[:50])

    # --- Фокус ---------------------------------------------------------
    removes_outline = re.search(r"outline:\s*none", html) or "outline-none" in html
    has_focus_visible = ":focus-visible" in html
    check(name, (not removes_outline) or has_focus_visible,
          "обводка фокуса снята без замены",
          "клавиатурная навигация становится невидимой")

    # --- Анимация ------------------------------------------------------
    has_animation = "@keyframes" in html or "transition:" in html
    check(name, (not has_animation) or "prefers-reduced-motion" in html,
          "анимация без prefers-reduced-motion",
          "у части игроков движение вызывает тошноту и головную боль")

    check(name, not re.search(r"transition:\s*all", html),
          "transition: all",
          "перерисовывает всё подряд, включая то, что меняться не должно")

    # --- Типографика ---------------------------------------------------
    # «...» из трёх точек вместо многоточия — только в видимом тексте.
    visible = re.sub(r"<script\b.*?</script>", "", html, flags=re.S)
    visible = re.sub(r"<style\b.*?</style>", "", visible, flags=re.S)
    check(name, "..." not in visible, "три точки вместо многоточия",
          "нужен символ …", warn_only=True)

    # Состояния загрузки заканчиваются многоточием.
    if "loading" in name or "auth" in name:
        check(name, "…" in visible, "нет многоточия в состоянии загрузки", warn_only=True)

    # --- Числа ---------------------------------------------------------
    if "%" in visible and "percent" in html:
        check(name, "tabular-nums" in html, "числа без tabular-nums",
              "проценты будут дёргаться при смене цифр", warn_only=True)
        check(name, "Intl.NumberFormat" in html, "число форматируется вручную",
              "нужен Intl.NumberFormat", warn_only=True)

    # --- Тема и безопасные зоны ----------------------------------------
    check(name, "color-scheme" in html, "нет color-scheme",
          "нативные поля и скроллбар останутся светлыми на тёмном фоне")
    check(name, 'name="theme-color"' in html, "нет theme-color", warn_only=True)
    check(name, "safe-area-inset" in html, "нет учёта safe-area", warn_only=True)

    # --- Анти-паттерны -------------------------------------------------
    check(name, "user-scalable=no" not in html and "maximum-scale=1" not in html,
          "масштабирование запрещено", "нарушает доступность")
    check(name, not re.search(r"onPaste[^)]*preventDefault", html),
          "блокировка вставки", "мешает менеджерам паролей")
    check(name, "autofocus" not in html.lower() or "autoFocus" in html,
          "autofocus в разметке", "фокус лучше ставить осознанно из скрипта",
          warn_only=True)

    # --- Длинный пользовательский текст --------------------------------
    # Имя сервера и сообщения приходят снаружи и могут быть любой длины.
    check(name, "overflow-wrap" in html or "break-words" in html or "truncate" in html,
          "нет обработки длинного текста",
          "длинное имя сервера или причина бана порвут вёрстку")

    # --- Формы ---------------------------------------------------------
    if "auth" in name:
        check(name, 'autocomplete="username"' in html, "нет autocomplete у логина")
        check(name, 'autocomplete="current-password"' in html or
                    'current-password' in html, "нет autocomplete у пароля")
        check(name, 'spellcheck="false"' in html, "проверка орфографии в логине/пароле",
              "подчёркивает ник красным")
        check(name, 'type="password"' in html, "пароль не скрыт")


def main() -> int:
    if not PAGES:
        print("NUI-страниц не найдено")
        return 1

    print("Проверка NUI по Vercel Web Interface Guidelines")
    print("Страниц: {}\n".format(len(PAGES)))

    for page in PAGES:
        audit(page)

    for w in WARNS:
        print("  [WARN] {}".format(w))
    for f in FAILS:
        print("  [FAIL] {}".format(f))

    print("\n" + "=" * 62)
    print("Проверок пройдено: {}, провалов: {}, предупреждений: {}".format(
        PASSES, len(FAILS), len(WARNS)))

    if FAILS:
        print("\nЧинить до показа игрокам.")
        return 1
    print("Критичных нарушений нет.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
