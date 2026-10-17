#include <Windows.h>
#include <exception>
// The same documented MSVC exception layout used by MonoGame 3.8.5.1.
namespace DX {
class com_exception : public std::exception {
    HRESULT result;
public:
    explicit com_exception(HRESULT value) : result(value) {}
};
}
extern "C" __declspec(dllexport) void ThrowGraphicsError() {
    throw DX::com_exception(static_cast<HRESULT>(0x887A0006));
}
