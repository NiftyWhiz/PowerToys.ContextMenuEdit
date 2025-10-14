#include "ExplorerCommand.h"
#include <shlwapi.h>
#include <new>
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
    if (!ppszName)
    {
        return E_POINTER;
    }
    return SHStrDup(kTopLevelLabel, ppszName);
}

IFACEMETHODIMP ExplorerCommand::GetIcon(IShellItemArray*, LPWSTR** ppszIcon)
{
    if (!ppszIcon)
    {
        return E_POINTER;
    }
    *ppszIcon = nullptr; // default
    return S_FALSE;
}

IFACEMETHODIMP ExplorerCommand::GetToolTip(IShellItemArray*, LPWSTR** ppszInfotip)
{
    if (!ppszInfotip)
    {
        return E_POINTER;
    }
    *ppszInfotip = nullptr;
    return E_NOTIMPL;
}

IFACEMETHODIMP ExplorerCommand::GetCanonicalName(GUID* pguidCommandName)
{
    if (!pguidCommandName)
    {
        return E_POINTER;
    }

    *pguidCommandName = kCanonicalGuid;
    return S_OK;
}

IFACEMETHODIMP ExplorerCommand::GetState(IShellItemArray*, BOOL, EXPCMDSTATE* pCmdState)
{
    if (!pCmdState)
    {
        return E_POINTER;
    }

    *pCmdState = ECS_ENABLED;
    return S_OK;
}

IFACEMETHODIMP ExplorerCommand::Invoke(IShellItemArray* psiItemArray, IBindCtx*)
{
    // When cascade is enabled, Invoke is not called; subcommands handle their own Invoke.
    return S_OK;
}

IFACEMETHODIMP ExplorerCommand::GetFlags(EXPCMDFLAGS* pFlags)
{
    if (!pFlags)
    {
        return E_POINTER;
    }

    *pFlags = ECF_HASSUBCOMMANDS; // cascade
    return S_OK;
}

class ActionExplorerCommand final : public IExplorerCommand
{
public:
    ActionExplorerCommand(ExplorerCommand* parent, const ActionItem& action) : _ref(1), _parent(parent), _action(action)
    {
        if (_parent)
        {
            _parent->AddRef();
        }
    }

    ~ActionExplorerCommand()
    {
        if (_parent)
        {
            _parent->Release();
        }
    }

    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv)
    {
        static const QITAB qit[] = {
            QITABENT(ActionExplorerCommand, IExplorerCommand),
            {0},
        };
        return QISearch(this, qit, riid, ppv);
    }

    IFACEMETHODIMP_(ULONG) AddRef()
    {
        return ++_ref;
    }

    IFACEMETHODIMP_(ULONG) Release()
    {
        ULONG count = --_ref;
        if (!count)
        {
            delete this;
        }
        return count;
    }

    // IExplorerCommand
    IFACEMETHODIMP GetTitle(IShellItemArray*, LPWSTR** name) override
    {
        if (!name)
        {
            return E_POINTER;
        }
        return SHStrDup(_action.label.c_str(), name);
    }

    IFACEMETHODIMP GetIcon(IShellItemArray*, LPWSTR** icon) override
    {
        if (!icon)
        {
            return E_POINTER;
        }
        if (_action.icon.empty())
        {
            *icon = nullptr;
            return S_FALSE;
        }
        return SHStrDup(_action.icon.c_str(), icon);
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray*, LPWSTR** tooltip) override
    {
        if (!tooltip)
        {
            return E_POINTER;
        }
        *tooltip = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetCanonicalName(GUID* name) override
    {
        if (!name)
        {
            return E_POINTER;
        }
        *name = GUID_NULL;
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray*, BOOL, EXPCMDSTATE* state) override
    {
        if (!state)
        {
            return E_POINTER;
        }
        bool hide = false;
        if (_action.extendedOnly)
        {
            hide = (GetKeyState(VK_SHIFT) & 0x8000) == 0;
        }

        *state = hide ? ECS_HIDDEN : ECS_ENABLED;
        return S_OK;
    }

    IFACEMETHODIMP Invoke(IShellItemArray*, IBindCtx*) override
    {
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
    {
        if (!flags)
        {
            return E_POINTER;
        }
        *flags = EXPCMDFLAGS(0);
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand**) override
    {
        return E_NOTIMPL;
    }

private:
    std::atomic<ULONG> _ref;
    ExplorerCommand* _parent;
    ActionItem _action;
};

// Simple enumerator of subcommands
class EnumExplorerCommand final : public IEnumExplorerCommand
{
public:
    EnumExplorerCommand(ExplorerCommand* parent, const std::vector<ActionItem>& actions) : _ref(1), _parent(parent), _actions(actions)
    {
        if (_parent)
        {
            _parent->AddRef();
        }
    }

    ~EnumExplorerCommand()
    {
        if (_parent)
        {
            _parent->Release();
        }
    }

    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv)
    {
        return QISearch(this, _qit, riid, ppv);
    }

    IFACEMETHODIMP_(ULONG) AddRef()
    {
        return ++_ref;
    }

    IFACEMETHODIMP_(ULONG) Release()
    {
        ULONG count = --_ref;
        if (!count)
        {
            delete this;
        }
        return count;
    }

    IFACEMETHODIMP Next(ULONG celt, IExplorerCommand** apCmd, ULONG* fetched)
    {
        if (!apCmd)
        {
            return E_POINTER;
        }

        ULONG produced = 0;
        while (produced < celt && _index < _actions.size())
        {
            auto* command = new (std::nothrow) ActionExplorerCommand(_parent, _actions[_index]);
            if (!command)
            {
                if (fetched)
                {
                    *fetched = produced;
                }
                return E_OUTOFMEMORY;
            }

            apCmd[produced] = command;
            ++produced;
            ++_index;
        }

        for (ULONG i = produced; i < celt; ++i)
        {
            apCmd[i] = nullptr;
        }

        if (fetched)
        {
            *fetched = produced;
        }

        return (_index < _actions.size()) ? S_OK : S_FALSE;
    }
    IFACEMETHODIMP Skip(ULONG celt)
    {
        _index = min(_index + celt, static_cast<ULONG>(_actions.size()));
        return S_OK;
    }

    IFACEMETHODIMP Reset()
    {
        _index = 0;
        return S_OK;
    }

    IFACEMETHODIMP Clone(IEnumExplorerCommand** ppEnum)
    {
        if (!ppEnum)
        {
            return E_POINTER;
        }

        *ppEnum = nullptr;

        auto* enumerator = new (std::nothrow) EnumExplorerCommand(_parent, _actions);
        if (!enumerator)
        {
            return E_OUTOFMEMORY;
        }

        enumerator->_index = _index;
        *ppEnum = enumerator;
        return S_OK;
    }
private:
    std::atomic<ULONG> _ref;
    ExplorerCommand* _parent;
    ULONG _index{0};
    const std::vector<ActionItem>& _actions;
    static const QITAB _qit[];
};
const QITAB EnumExplorerCommand::_qit[] = {
    QITABENT(EnumExplorerCommand, IEnumExplorerCommand), {0}
};

IFACEMETHODIMP ExplorerCommand::EnumSubCommands(IEnumExplorerCommand** ppEnum)
{
    if (!ppEnum)
    {
        return E_POINTER;
    }

    *ppEnum = nullptr;

    auto* enumerator = new (std::nothrow) EnumExplorerCommand(this, _actions);
    if (!enumerator)
    {
        return E_OUTOFMEMORY;
    }

    *ppEnum = enumerator;
    return S_OK;
}

// IObjectWithSelection
IFACEMETHODIMP ExplorerCommand::SetSelection(IShellItemArray* psia)
{ _selection = psia; return S_OK; }
IFACEMETHODIMP ExplorerCommand::GetSelection(REFIID riid, void** ppv)
{ return _selection ? _selection.As(riid, ppv) : E_FAIL; }