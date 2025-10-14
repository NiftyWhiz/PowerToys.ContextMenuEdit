#include "ExplorerCommandFactory.h"
#include <new>

IFACEMETHODIMP ExplorerCommandFactory::QueryInterface(REFIID riid, void** ppv)
{ return (riid==IID_IUnknown || riid==IID_IClassFactory) ? (*ppv = static_cast<IClassFactory*>(this), AddRef(), S_OK) : E_NOINTERFACE; }
IFACEMETHODIMP_(ULONG) AddRef(){ return ++_ref; }
IFACEMETHODIMP_(ULONG) Release(){ ULONG c=--_ref; if(!c) delete this; return c; }
IFACEMETHODIMP ExplorerCommandFactory::CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv)
{
    if (pUnkOuter) return CLASS_E_NOAGGREGATION;
    auto obj = new (std::nothrow) ExplorerCommand();
    return obj ? obj->QueryInterface(riid, ppv) : E_OUTOFMEMORY;
}
IFACEMETHODIMP ExplorerCommandFactory::LockServer(BOOL){ return S_OK; }