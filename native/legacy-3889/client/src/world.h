#pragma once
#include <string>
#include <vector>
#include "ui.h"

// Мир от сервера без клиентских скриптов: объекты карты, метки, маркеры,
// 3D-надписи и NPC (сообщения WOBJ, WBLIP, WMARKER, WLABEL, WNPC, WDEL, WCLEAR),
// интерьеры и части карты (WIPL, WISET).
namespace flov::world
{
    /// Сообщение сервера о мире; false — не про мир.
    bool Handle(const std::vector<std::string>& m);
    /// Каждый кадр: подгрузка объектов и NPC рядом, маркеры.
    void Tick(float x, float y, float z);
    /// 3D-надписи рядом с камерой — к никам игроков.
    void AddLabels(std::vector<ui::Label>& out, float camX, float camY, float camZ);
    /// Убрать всё (отключение от сервера, WCLEAR). Интерьеры не трогает: смена
    /// измерения (WCLEAR + снимок) не должна перезагружать карту.
    void Clear();
    /// Вернуть карту, как была до сервера (отключение).
    void ResetInteriors();
    /// Для консоли: сколько элементов и сколько сейчас создано в игре.
    std::string Summary();
}
