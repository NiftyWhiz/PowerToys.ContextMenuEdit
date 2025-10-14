#include <windows.h>
#include "ExplorerCommandFactory.h"

BOOL APIENTRY DllMain(HMODULE, DWORD, LPVOID) { return TRUE; }

extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID rclsid, REFIID riid, void** ppv)
{
    if (rclsid == CLSID_ContextMenuEdit)
    {
        *ppv = static_cast<IClassFactory*>(new ExplorerCommandFactory());
        reinterpret_cast<IUnknown*>(*ppv)->AddRef();
        return S_OK;
    }
    *ppv = nullptr; return CLASS_E_CLASSNOTAVAILABLE;
}

extern "C" HRESULT __stdcall DllCanUnloadNow(void)
{ return S_FALSE; }