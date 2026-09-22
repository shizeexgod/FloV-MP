#pragma once
#include <string>
#include <vector>
#include "ui.h"

// Мир от сервера без клиентских скриптов: объекты карты, метки, маркеры,
// 3D-надписи и NPC (сообщения WOBJ, WBLIP, WMARKER, WLABEL, WNPC, WDEL, WCLEAR).
namespace flov::world
{
    /// Сообщение сервера о мире; false — не про мир.
    bool Handle(const std::vector<std::string>& m);
    /// Каждый кадр: подгрузка объектов и NPC рядом, маркеры.
    void Tick(float x, float y, float z);
    /// 3D-надписи рядом с камерой — к никам игроков.
    void AddLabels(std::vector<ui::Label>& out, float camX, float camY, float camZ);
    /// Убрать всё (отключение от сервера, WCLEAR).
    void Clear();
    /// Для консоли: сколько элементов и сколько сейчас создано в игре.
    std::string Summary();
}
