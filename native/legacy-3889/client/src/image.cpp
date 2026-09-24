#include "image.h"
#include "common.h"

#include <d3d11.h>
#include <wincodec.h>
#include <cstdint>
#include <string>
#include <vector>

namespace flov::image
{
    namespace
    {
        // Больше 4096 по стороне экранам не нужно (и 4K-фон в это укладывается),
        // а больше 100 Мп — заведомо не картинка для загрузочного экрана.
        constexpr UINT kMaxSide = 4096;
        constexpr uint64_t kMaxSourcePixels = 100ull * 1000 * 1000;

        IWICImagingFactory* Factory()
        {
            static IWICImagingFactory* factory = nullptr;
            if (factory) return factory;
            // COM уже инициализирован игрой; свой вызов делаем на случай
            // запуска вне игры (средство предпросмотра) и не жалуемся, если
            // модель потока уже выбрана кем-то другим.
            CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
            CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER,
                             IID_PPV_ARGS(&factory));
            return factory;
        }

        /// Кадр WIC → BGRA → текстура DirectX. Один путь для файла и ресурса.
        Texture FromDecoder(ID3D11Device* device, IWICBitmapDecoder* decoder)
        {
            Texture texture;
            if (!device || !decoder) return texture;

            IWICBitmapFrameDecode* frame = nullptr;
            if (FAILED(decoder->GetFrame(0, &frame)) || !frame) return texture;

            IWICFormatConverter* converter = nullptr;
            IWICBitmapScaler* scaler = nullptr;
            IWICBitmapSource* source = frame;
            UINT width = 0, height = 0;
            if (FAILED(frame->GetSize(&width, &height)) || !width || !height ||
                (uint64_t)width * height > kMaxSourcePixels)
            {
                // Картинку присылает сервер (loading.background_url): маленький
                // PNG может раскрыться в десятки гигабайт и уронить игру.
                Log("ui: картинка " + std::to_string(width) + "x" + std::to_string(height) + " слишком большая — пропущена");
                frame->Release();
                return texture;
            }
            if (width > kMaxSide || height > kMaxSide)
            {
                // Больше стороны текстуры не нужно ни одному экрану — уменьшаем
                // при чтении, не держа исходник в памяти целиком.
                const double k = (double)kMaxSide / (width > height ? width : height);
                const UINT w = (UINT)(width * k) ? (UINT)(width * k) : 1, h = (UINT)(height * k) ? (UINT)(height * k) : 1;
                if (SUCCEEDED(Factory()->CreateBitmapScaler(&scaler)) && scaler &&
                    SUCCEEDED(scaler->Initialize(frame, w, h, WICBitmapInterpolationModeFant)))
                {
                    source = scaler;
                    width = w;
                    height = h;
                }
                else
                {
                    if (scaler) scaler->Release();
                    frame->Release();
                    return texture;
                }
            }
            if (SUCCEEDED(Factory()->CreateFormatConverter(&converter)) && converter &&
                SUCCEEDED(converter->Initialize(source, GUID_WICPixelFormat32bppBGRA,
                                                WICBitmapDitherTypeNone, nullptr, 0.0,
                                                WICBitmapPaletteTypeCustom)))
            {
                const UINT stride = width * 4;
                std::vector<uint8_t> pixels((size_t)stride * height);
                if (SUCCEEDED(converter->CopyPixels(nullptr, stride, (UINT)pixels.size(), pixels.data())))
                {
                    D3D11_TEXTURE2D_DESC desc{};
                    desc.Width = width;
                    desc.Height = height;
                    desc.MipLevels = 1;
                    desc.ArraySize = 1;
                    desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
                    desc.SampleDesc.Count = 1;
                    desc.Usage = D3D11_USAGE_IMMUTABLE;
                    desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;

                    D3D11_SUBRESOURCE_DATA data{};
                    data.pSysMem = pixels.data();
                    data.SysMemPitch = stride;

                    ID3D11Texture2D* surface = nullptr;
                    if (SUCCEEDED(device->CreateTexture2D(&desc, &data, &surface)) && surface)
                    {
                        D3D11_SHADER_RESOURCE_VIEW_DESC view{};
                        view.Format = desc.Format;
                        view.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D;
                        view.Texture2D.MipLevels = 1;
                        if (SUCCEEDED(device->CreateShaderResourceView(surface, &view, &texture.view)))
                        {
                            texture.width = (int)width;
                            texture.height = (int)height;
                        }
                        surface->Release();
                    }
                }
            }
            if (converter) converter->Release();
            if (scaler) scaler->Release();
            frame->Release();
            return texture;
        }
    }

    Texture LoadFile(ID3D11Device* device, const std::wstring& path)
    {
        Texture texture;
        auto* factory = Factory();
        if (!factory || path.empty()) return texture;

        IWICBitmapDecoder* decoder = nullptr;
        if (SUCCEEDED(factory->CreateDecoderFromFilename(path.c_str(), nullptr, GENERIC_READ,
                                                         WICDecodeMetadataCacheOnLoad, &decoder)) && decoder)
        {
            texture = FromDecoder(device, decoder);
            decoder->Release();
        }
        return texture;
    }

    Texture LoadResource(ID3D11Device* device, int resourceId)
    {
        Texture texture;
        auto* factory = Factory();
        if (!factory) return texture;

        HMODULE self = nullptr;
        GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                           reinterpret_cast<LPCWSTR>(&LoadFile), &self);
        if (!self) return texture;

        HRSRC found = FindResourceW(self, MAKEINTRESOURCEW(resourceId), RT_RCDATA);
        if (!found) return texture;
        const DWORD size = SizeofResource(self, found);
        HGLOBAL handle = ::LoadResource(self, found);
        const void* bytes = handle ? LockResource(handle) : nullptr;
        if (!bytes || !size) return texture;

        IWICStream* stream = nullptr;
        IWICBitmapDecoder* decoder = nullptr;
        if (SUCCEEDED(factory->CreateStream(&stream)) && stream &&
            SUCCEEDED(stream->InitializeFromMemory(const_cast<BYTE*>(static_cast<const BYTE*>(bytes)), size)) &&
            SUCCEEDED(factory->CreateDecoderFromStream(stream, nullptr, WICDecodeMetadataCacheOnLoad, &decoder)) && decoder)
        {
            texture = FromDecoder(device, decoder);
        }
        if (decoder) decoder->Release();
        if (stream) stream->Release();
        return texture;
    }

    void Release(Texture& texture)
    {
        if (texture.view) texture.view->Release();
        texture.view = nullptr;
        texture.width = texture.height = 0;
    }
}
