#pragma once
#include <windows.h>
#include <shobjidl_core.h>
#include <wrl.h>
#include <string>
#include <vector>

struct ActionItem {
    std::wstring id;
    std::wstring label;
    std::wstring icon; // optional
    bool extendedOnly{ false };
};

class ExplorerCommand : public IExplorerCommand, public IObjectWithSelection
{
public:
    ExplorerCommand();

    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override;
    IFACEMETHODIMP_(ULONG) AddRef() override;
    IFACEMETHODIMP_(ULONG) Release() override;

    // IExplorerCommand
    IFACEMETHODIMP GetTitle(IShellItemArray* psiItemArray, LPWSTR* *ppszName) override;
    IFACEMETHODIMP GetIcon(IShellItemArray* psiItemArray, LPWSTR* *ppszIcon) override;
    IFACEMETHODIMP GetToolTip(IShellItemArray*, LPWSTR* *ppszInfotip) override;
    IFACEMETHODIMP GetCanonicalName(GUID* pguidCommandName) override;
    IFACEMETHODIMP GetState(IShellItemArray*, BOOL fOkToBeSlow, EXPCMDSTATE* pCmdState) override;
    IFACEMETHODIMP Invoke(IShellItemArray* psiItemArray, IBindCtx* pbc) override;
    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* pFlags) override;
    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** ppEnum) override;

    // IObjectWithSelection
    IFACEMETHODIMP SetSelection(IShellItemArray* psia) override;
    IFACEMETHODIMP GetSelection(REFIID riid, void** ppv) override;

private:
    std::atomic<ULONG> _refCount{1};
    Microsoft::WRL::ComPtr<IShellItemArray> _selection;
    std::vector<ActionItem> _actions; // TODO: load from JSON settings
};