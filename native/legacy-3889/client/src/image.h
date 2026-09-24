#pragma once
// Картинки для интерфейса: логотип и фон загрузочного экрана.
//
// Декодер — WIC, он входит в Windows. Отдельная библиотека ради двух PNG не
// нужна, а лишний сторонний код в клиенте — лишняя поверхность для ошибок.

#include <windows.h>
#include <string>

struct ID3D11Device;
struct ID3D11ShaderResourceView;

namespace flov::image
{
    struct Texture
    {
        ID3D11ShaderResourceView* view = nullptr;
        int width = 0, height = 0;
        explicit operator bool() const { return view != nullptr; }
    };

    /// Файл с диска. Формат любой, какой понимает Windows: PNG, JPEG, BMP.
    Texture LoadFile(ID3D11Device* device, const std::wstring& path);
    /// Ресурс RCDATA из нашего модуля (логотип зашит в FloVMP.asi).
    Texture LoadResource(ID3D11Device* device, int resourceId);
    void Release(Texture& texture);
}
