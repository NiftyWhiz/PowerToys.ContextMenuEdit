#pragma once
#include <wrl.h>
#include <shobjidl_core.h>
#include <atomic>
#include "ExplorerCommand.h"

// {E5B37D79-4DDA-4A78-B2C9-7B1E1FB1E4A4}
static const CLSID CLSID_ContextMenuEdit = {0xE5B37D79,0x4DDA,0x4A78,{0xB2,0xC9,0x7B,0x1E,0x1F,0xB1,0xE4,0xA4}};

class ExplorerCommandFactory : public IClassFactory
{
    std::atomic<ULONG> _ref{1};
public:
    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv);
    IFACEMETHODIMP_(ULONG) AddRef();
    IFACEMETHODIMP_(ULONG) Release();

    // IClassFactory
    IFACEMETHODIMP CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv);
    IFACEMETHODIMP LockServer(BOOL fLock);
};