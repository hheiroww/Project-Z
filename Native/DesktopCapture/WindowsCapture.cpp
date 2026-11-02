// Project-Z desktop capture. Screen pixels stay in memory; no screenshots are written.
#include <windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <shellscalingapi.h>
#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.interop.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>
#include <algorithm>
#include <climits>
#include <cstring>
#include <memory>
#include <mutex>
#include <unordered_map>
#include <vector>
using namespace winrt;
using namespace winrt::Windows::Graphics::Capture;
using namespace winrt::Windows::Graphics::DirectX;
using namespace winrt::Windows::Graphics::DirectX::Direct3D11;

namespace {
struct PhysicalCoordinates {
    DPI_AWARENESS_CONTEXT previous = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    ~PhysicalCoordinates() { if (previous) SetThreadDpiAwarenessContext(previous); }
};
thread_local std::string error;
std::mutex affinitiesGate;
struct Affinity { DWORD original; unsigned users; };
std::unordered_map<HWND, Affinity> affinities;
void exclude(HWND hwnd) {
    std::lock_guard guard(affinitiesGate);
    auto found = affinities.find(hwnd);
    if (found != affinities.end()) { ++found->second.users; return; }
    DWORD original = 0, pid = 0;
    GetWindowThreadProcessId(hwnd, &pid);
    if (pid != GetCurrentProcessId() || !GetWindowDisplayAffinity(hwnd, &original) ||
        !SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE))
        throw hresult_error(HRESULT_FROM_WIN32(GetLastError()), L"Could not exclude the host window from capture.");
    affinities.emplace(hwnd, Affinity{original, 1});
}
void restore(HWND hwnd) {
    std::lock_guard guard(affinitiesGate);
    auto found = affinities.find(hwnd);
    if (found == affinities.end() || --found->second.users) return;
    DWORD current = 0;
    if (IsWindow(hwnd) && GetWindowDisplayAffinity(hwnd, &current) && current == WDA_EXCLUDEFROMCAPTURE)
        SetWindowDisplayAffinity(hwnd, found->second.original);
    affinities.erase(found);
}
struct Display {
    RECT bounds{};
    GraphicsCaptureItem item{nullptr};
    Direct3D11CaptureFramePool pool{nullptr};
    GraphicsCaptureSession session{nullptr};
    Direct3D11CaptureFrame retained{nullptr};
    com_ptr<ID3D11Texture2D> staging;
    std::vector<unsigned char> pixels;
    int width = 0, height = 0;
    RECT crop{};
    int imageX = 0, imageY = 0;
    ~Display() { try { if (retained) retained.Close(); if (session) session.Close(); if (pool) pool.Close(); } catch (...) {} }
};
struct Capture {
    HWND owner;
    com_ptr<ID3D11Device> device;
    com_ptr<ID3D11DeviceContext> context;
    IDirect3DDevice projected{nullptr};
    std::unordered_map<HMONITOR, std::unique_ptr<Display>> displays;
    explicit Capture(HWND hwnd) : owner(hwnd) {
        try { init_apartment(apartment_type::multi_threaded); } catch (hresult_error const& e) { if (e.code() != RPC_E_CHANGED_MODE) throw; }
        if (!GraphicsCaptureSession::IsSupported()) throw hresult_error(E_NOTIMPL, L"Windows Graphics Capture is unavailable.");
        check_hresult(D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            nullptr, 0, D3D11_SDK_VERSION, device.put(), nullptr, context.put()));
        com_ptr<IInspectable> inspectable;
        check_hresult(CreateDirect3D11DeviceFromDXGIDevice(device.as<IDXGIDevice>().get(), inspectable.put()));
        projected = inspectable.as<IDirect3DDevice>();
        exclude(owner);
    }
    ~Capture() { displays.clear(); restore(owner); }
    Display& display(HMONITOR monitor) {
        auto& value = displays[monitor];
        if (value) return *value;
        value = std::make_unique<Display>();
        MONITORINFO info{sizeof(info)};
        check_bool(GetMonitorInfo(monitor, &info));
        value->bounds = info.rcMonitor;
        auto factory = get_activation_factory<GraphicsCaptureItem, IGraphicsCaptureItemInterop>();
        check_hresult(factory->CreateForMonitor(monitor, guid_of<GraphicsCaptureItem>(), put_abi(value->item)));
        value->pool = Direct3D11CaptureFramePool::CreateFreeThreaded(projected,
            DirectXPixelFormat::B8G8R8A8UIntNormalized, 2, value->item.Size());
        value->session = value->pool.CreateCaptureSession(value->item);
        value->session.IsCursorCaptureEnabled(false);
        try { value->session.IsBorderRequired(false); } catch (...) { /* Keep the OS capture indicator if required. */ }
        value->session.StartCapture();
        return *value;
    }
    bool refresh(Display& value, const RECT& request) {
        Direct3D11CaptureFrame newest{nullptr};
        while (auto frame = value.pool.TryGetNextFrame()) {
            if (newest) newest.Close();
            newest = std::move(frame);
        }
        bool changed = bool(newest);
        if (newest) {
            if (value.retained) value.retained.Close();
            value.retained = std::move(newest);
        }
        if (!value.retained) return false;
        auto size = value.retained.ContentSize();
        if (size.Width <= 0 || size.Height <= 0) return false;
        auto access = value.retained.Surface().as<::Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess>();
        com_ptr<ID3D11Texture2D> source;
        check_hresult(access->GetInterface(__uuidof(ID3D11Texture2D), source.put_void()));
        D3D11_TEXTURE2D_DESC desc{}; source->GetDesc(&desc);
        const auto sourceWidth = desc.Width, sourceHeight = desc.Height;
        RECT crop{
            std::clamp(request.left - value.bounds.left, 0L, LONG(sourceWidth) - 1),
            std::clamp(request.top - value.bounds.top, 0L, LONG(sourceHeight) - 1),
            std::clamp(request.right - value.bounds.left, 1L, LONG(sourceWidth)),
            std::clamp(request.bottom - value.bounds.top, 1L, LONG(sourceHeight))};
        const int width = std::max(1L, crop.right - crop.left), height = std::max(1L, crop.bottom - crop.top);
        changed = changed || !EqualRect(&crop, &value.crop);
        value.crop = crop;
        value.imageX = value.bounds.left + crop.left; value.imageY = value.bounds.top + crop.top;
        if (!value.staging || value.width != width || value.height != height) {
            value.staging = nullptr;
            value.width = width; value.height = height;
            desc.Width = width; desc.Height = height;
            desc.BindFlags = 0; desc.MiscFlags = 0; desc.Usage = D3D11_USAGE_STAGING; desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
            check_hresult(device->CreateTexture2D(&desc, nullptr, value.staging.put()));
            value.pixels.resize(size_t(value.width) * value.height * 4);
            changed = true;
        }
        if (!changed) return true;
        // Read only the padded host region, rather than transferring the complete monitor.
        D3D11_BOX box{UINT(crop.left), UINT(crop.top), 0, UINT(crop.right), UINT(crop.bottom), 1};
        context->CopySubresourceRegion(value.staging.get(), 0, 0, 0, 0, source.get(), 0, &box);
        D3D11_MAPPED_SUBRESOURCE mapped{};
        check_hresult(context->Map(value.staging.get(), 0, D3D11_MAP_READ, 0, &mapped));
        for (int y = 0; y < value.height; ++y)
            std::memcpy(value.pixels.data() + size_t(y) * value.width * 4,
                static_cast<unsigned char*>(mapped.pData) + size_t(y) * mapped.RowPitch, size_t(value.width) * 4);
        context->Unmap(value.staging.get(), 0);
        if (size.Width != int(sourceWidth) || size.Height != int(sourceHeight)) {
            value.retained.Close(); value.retained = nullptr;
            value.pool.Recreate(projected, DirectXPixelFormat::B8G8R8A8UIntNormalized, 2, size);
        }
        return true;
    }
};
}
extern "C" __declspec(dllexport) const char* pz_capture_error() { return error.c_str(); }
extern "C" __declspec(dllexport) void* pz_capture_create(void* owner) {
    try { return new Capture(static_cast<HWND>(owner)); }
    catch (hresult_error const& e) { error = to_string(e.message()); return nullptr; }
    catch (std::exception const& e) { error = e.what(); return nullptr; }
}
extern "C" __declspec(dllexport) int pz_capture_bounds(void* capture, int* x, int* y, int* width, int* height, float* scale) {
    PhysicalCoordinates coordinates;
    auto& value = *static_cast<Capture*>(capture);
    RECT rect{}; POINT origin{};
    if (!IsWindow(value.owner) || IsIconic(value.owner) || !GetClientRect(value.owner, &rect) || !ClientToScreen(value.owner, &origin)) return 0;
    *x = origin.x; *y = origin.y; *width = rect.right; *height = rect.bottom;
    DEVICE_SCALE_FACTOR factor = SCALE_100_PERCENT;
    GetScaleFactorForMonitor(MonitorFromWindow(value.owner, MONITOR_DEFAULTTONEAREST), &factor);
    *scale = static_cast<float>(factor) / 100.0f;
    return *width > 0 && *height > 0;
}
extern "C" __declspec(dllexport) int pz_capture_read(void* capture, int x, int y, int width, int height, int downsample, unsigned char* rgba, int capacity) {
    try {
        PhysicalCoordinates coordinates;
        if (!capture || !rgba || downsample < 1 || downsample > 8 || width <= 0 || height <= 0 || width > 16384 || height > 16384) return -1;
        const int ow = (width + downsample - 1) / downsample, oh = (height + downsample - 1) / downsample;
        if (size_t(capacity) < size_t(ow) * oh * 4) return -1;
        auto& value = *static_cast<Capture*>(capture);
        DWORD affinity = 0;
        if (!GetWindowDisplayAffinity(value.owner, &affinity) || affinity != WDA_EXCLUDEFROMCAPTURE)
            throw hresult_error(E_ACCESSDENIED, L"Host-window capture exclusion changed; desktop blur stopped to prevent feedback.");
        std::vector<HMONITOR> monitors;
        RECT request{x, y, x + width, y + height};
        EnumDisplayMonitors(nullptr, &request, [](HMONITOR m, HDC, LPRECT, LPARAM p)->BOOL {
            reinterpret_cast<std::vector<HMONITOR>*>(p)->push_back(m); return TRUE;
        }, reinterpret_cast<LPARAM>(&monitors));
        for (auto it = value.displays.begin(); it != value.displays.end();) {
            if (std::find(monitors.begin(), monitors.end(), it->first) == monitors.end()) it = value.displays.erase(it); else ++it;
        }
        std::vector<Display*> ready;
        for (auto monitor : monitors) {
            auto& display = value.display(monitor);
            MONITORINFO info{sizeof(info)}; if (GetMonitorInfo(monitor, &info)) display.bounds = info.rcMonitor;
            if (value.refresh(display, request)) ready.push_back(&display);
        }
        if (ready.size() != monitors.size() || ready.empty()) return 0;
        for (int dy = 0; dy < oh; ++dy) for (int dx = 0; dx < ow; ++dx) {
            int sx = x + dx * downsample, sy = y + dy * downsample;
            Display* best = nullptr; long long distance = LLONG_MAX;
            for (auto display : ready) {
                int cx = std::clamp(sx, int(display->bounds.left), int(display->bounds.right) - 1);
                int cy = std::clamp(sy, int(display->bounds.top), int(display->bounds.bottom) - 1);
                long long d = (static_cast<long long>(sx) - cx) * (static_cast<long long>(sx) - cx) + (static_cast<long long>(sy) - cy) * (static_cast<long long>(sy) - cy);
                if (d < distance) { distance = d; best = display; }
            }
            int px = std::clamp(sx - best->imageX, 0, best->width - 1), py = std::clamp(sy - best->imageY, 0, best->height - 1);
            unsigned sums[3]{}; unsigned count = 0;
            for (int by = 0; by < downsample; ++by) for (int bx = 0; bx < downsample; ++bx) {
                auto pixel = best->pixels.data() + (size_t(std::min(py + by, best->height - 1)) * best->width + std::min(px + bx, best->width - 1)) * 4;
                for (int c = 0; c < 3; ++c) sums[c] += pixel[2 - c]; ++count;
            }
            auto dest = rgba + (size_t(dy) * ow + dx) * 4;
            for (int c = 0; c < 3; ++c) dest[c] = static_cast<unsigned char>(sums[c] / count);
            dest[3] = 255;
        }
        return 1;
    } catch (hresult_error const& e) { error = to_string(e.message()); return -1; }
      catch (std::exception const& e) { error = e.what(); return -1; }
}
extern "C" __declspec(dllexport) void pz_capture_destroy(void* capture) { delete static_cast<Capture*>(capture); }
