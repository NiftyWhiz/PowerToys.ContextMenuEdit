#include "ExplorerCommand.h"
#include <shlwapi.h>
#pragma comment(lib, "Shlwapi.lib")

static const wchar_t* kTopLevelLabel = L"Context Menu Edit";
static const GUID kCanonicalGuid = {0x6b6f26f1,0x9b3f,0x4f5f,{0xa5,0x37,0x13,0x56,0x7b,0x1b,0x33,0xa1}}; // unique

ExplorerCommand::ExplorerCommand()
{
    // Sample static actions; replace with parsed JSON later.
    _actions.push_back({L"open_ps_here", L"Open PowerShell here", L"", false});
    _actions.push_back({L"copy_path", L"Copy full path", L"", true});
}

// IUnknown
IFACEMETHODIMP ExplorerCommand::QueryInterface(REFIID riid, void** ppv)
{
    static const QITAB qit[] = {
        QITABENT(ExplorerCommand, IExplorerCommand),
        QITABENT(ExplorerCommand, IObjectWithSelection),
        {0}
    };
    return QISearch(this, qit, riid, ppv);
}
IFACEMETHODIMP_(ULONG) ExplorerCommand::AddRef(){ return ++_refCount; }
IFACEMETHODIMP_(ULONG) ExplorerCommand::Release(){ ULONG c = --_refCount; if(!c) delete this; return c; }

// IExplorerCommand
IFACEMETHODIMP ExplorerCommand::GetTitle(IShellItemArray*, LPWSTR** ppszName)
{
    return SHStrDup(kTopLevelLabel, ppszName);
}

IFACEMETHODIMP ExplorerCommand::GetIcon(IShellItemArray*, LPWSTR** ppszIcon)
{
    *ppszIcon = nullptr; // default
    return S_OK;
}

IFACEMETHODIMP ExplorerCommand::GetToolTip(IShellItemArray*, LPWSTR** ppszInfotip)
{
    *ppszInfotip = nullptr; return E_NOTIMPL;
}

IFACEMETHODIMP ExplorerCommand::GetCanonicalName(GUID* pguidCommandName)
{
    *pguidCommandName = kCanonicalGuid; return S_OK;
}

IFACEMETHODIMP ExplorerCommand::GetState(IShellItemArray*, BOOL, EXPCMDSTATE* pCmdState)
{
    *pCmdState = ECS_ENABLED; return S_OK;
}

IFACEMETHODIMP ExplorerCommand::Invoke(IShellItemArray* psiItemArray, IBindCtx*)
{
    // When cascade is enabled, Invoke is not called; subcommands handle their own Invoke.
    return S_OK;
}

IFACEMETHODIMP ExplorerCommand::GetFlags(EXPCMDFLAGS* pFlags)
{
    *pFlags = ECF_HASSUBCOMMANDS; // cascade
    return S_OK;
}

// Simple enumerator of subcommands
class EnumExplorerCommand : public IEnumExplorerCommand
{
public:
    EnumExplorerCommand(const std::vector<ActionItem>& actions) : _ref(1), _actions(actions) {}
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv){ return QISearch(this, _qit, riid, ppv);} 
    IFACEMETHODIMP_(ULONG) AddRef(){ return ++_ref; }
    IFACEMETHODIMP_(ULONG) Release(){ ULONG c=--_ref; if(!c) delete this; return c; }
    IFACEMETHODIMP Next(ULONG celt, IExplorerCommand** apCmd, ULONG* fetched)
    {
        ULONG i=0;
        for(; i<celt && _index<_actions.size(); ++i, ++_index)
        {
            *apCmd++ = nullptr; // placeholder for per-action command
        }
        if(fetched) *fetched = i;
        return (_index < _actions.size()) ? S_OK : S_FALSE;
    }
    IFACEMETHODIMP Skip(ULONG celt){ _index = min(_index + celt, (ULONG)_actions.size()); return S_OK; }
    IFACEMETHODIMP Reset(){ _index = 0; return S_OK; }
    IFACEMETHODIMP Clone(IEnumExplorerCommand** ppEnum){ *ppEnum = new EnumExplorerCommand(_actions); (*ppEnum)->AddRef(); return S_OK; }
private:
    std::atomic<ULONG> _ref; ULONG _index{0};
    const std::vector<ActionItem>& _actions;
    static const QITAB _qit[];
};
const QITAB EnumExplorerCommand::_qit[] = {
    QITABENT(EnumExplorerCommand, IEnumExplorerCommand), {0}
};

IFACEMETHODIMP ExplorerCommand::EnumSubCommands(IEnumExplorerCommand** ppEnum)
{
    *ppEnum = new EnumExplorerCommand(_actions); (*ppEnum)->AddRef(); return S_OK;
}

// IObjectWithSelection
IFACEMETHODIMP ExplorerCommand::SetSelection(IShellItemArray* psia)
{ _selection = psia; return S_OK; }
IFACEMETHODIMP ExplorerCommand::GetSelection(REFIID riid, void** ppv)
{ return _selection ? _selection.As(riid, ppv) : E_FAIL; }