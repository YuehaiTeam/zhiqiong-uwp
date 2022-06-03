#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Storage.h>
#include <winrt/Windows.System.h>
#include <Windows.h>

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, PSTR lpCmdLine, INT nCmdShow) {
    winrt::init_apartment();
    winrt::Windows::Storage::ApplicationDataContainer localSettings = winrt::Windows::Storage::ApplicationData::Current().LocalSettings();
    auto values = localSettings.Values();
    winrt::hstring val = winrt::unbox_value<winrt::hstring>(values.Lookup(L"parameters"));
    values.Remove(L"parameters");
    winrt::Windows::Foundation::Uri uri(val);
    winrt::Windows::System::Launcher::LaunchUriAsync(uri).get();
    return 0;
}