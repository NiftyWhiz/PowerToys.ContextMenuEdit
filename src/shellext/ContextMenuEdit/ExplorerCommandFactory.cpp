#include "ExplorerCommandFactory.h"
#include <new>

IFACEMETHODIMP ExplorerCommandFactory::QueryInterface(REFIID riid, void** ppv)
{
    if (!ppv)
    {
        return E_POINTER;
    }

    *ppv = nullptr;

    if (riid == IID_IUnknown || riid == IID_IClassFactory)
    {
        *ppv = static_cast<IClassFactory*>(this);
        AddRef();
        return S_OK;
    }

    return E_NOINTERFACE;
}
IFACEMETHODIMP_(ULONG) ExplorerCommandFactory::AddRef()
{
    return ++_ref;
}

IFACEMETHODIMP_(ULONG) ExplorerCommandFactory::Release()
{
    ULONG count = --_ref;
    if (!count)
    {
        delete this;
    }
    return count;
}
IFACEMETHODIMP ExplorerCommandFactory::CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv)
{
    if (!ppv)
    {
        return E_POINTER;
    }

    *ppv = nullptr;

    if (pUnkOuter) return CLASS_E_NOAGGREGATION;
    auto obj = new (std::nothrow) ExplorerCommand();
    return obj ? obj->QueryInterface(riid, ppv) : E_OUTOFMEMORY;
}
IFACEMETHODIMP ExplorerCommandFactory::LockServer(BOOL){ return S_OK; }