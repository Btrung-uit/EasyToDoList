[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Installer,
    [Parameter(Mandatory = $true)][string]$PreviousInstaller,
    [Parameter(Mandatory = $true)][string]$ExpectedVersion,
    [string]$OutputDirectory = 'C:\Evidence'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:USERNAME -ne 'WDAGUtilityAccount') { throw 'Run only in a disposable Windows Sandbox profile, never on personal data.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class ReleaseDesktop {
    [DllImport("user32.dll", SetLastError=true)] public static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
    private delegate bool WindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    public static IntPtr[] WindowsFor(int processId) {
        var windows = new List<IntPtr>();
        EnumWindows(delegate(IntPtr window, IntPtr parameter) {
            uint owner; GetWindowThreadProcessId(window, out owner);
            if (owner == processId) windows.Add(window);
            return true;
        }, IntPtr.Zero);
        return windows.ToArray();
    }
}
'@
$steps = [System.Collections.Generic.List[string]]::new()
$owned = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$installDirectory = Join-Path $env:LOCALAPPDATA 'Programs\EasyToDoList'
$executable = Join-Path $installDirectory 'EasyToDoList.App.exe'
$dataDirectory = Join-Path $env:LOCALAPPDATA 'EasyToDoList'
$database = Join-Path $dataDirectory 'easytodolist.db'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{B8FD5D8B-4472-4ACF-AD13-BBF48BFE1D41}_is1'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$script:primary = $null

function Assert-That($Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Record([string]$Message) { $steps.Add($Message); $Message | Add-Content (Join-Path $OutputDirectory 'steps.txt') }
function Wait-Until([scriptblock]$Check, [string]$Message) {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do { $value = & $Check; if ($value) { return $value }; Start-Sleep -Milliseconds 100 } while ([DateTime]::UtcNow -lt $deadline)
    throw "Timeout: $Message"
}
function Run-Native([string]$File, [string[]]$Arguments) {
    $process = Start-Process -FilePath $File -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    $owned.Add($process)
    if (-not $process.WaitForExit(60000)) { throw "Installer timed out: $File" }
    return $process.ExitCode
}
function Install([string]$File, [string]$Log) {
    $exitCode = Run-Native $File @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/TASKS=desktopicon', ('/LOG="' + (Join-Path $OutputDirectory $Log) + '"'))
    Assert-That ($exitCode -eq 0) "Installer exit code $exitCode"
}
function Get-Window {
    $script:primary.Refresh()
    if ($script:primary.HasExited -or $script:primary.MainWindowHandle -eq [IntPtr]::Zero) { return $null }
    [System.Windows.Automation.AutomationElement]::FromHandle($script:primary.MainWindowHandle)
}
function Find-Name($Root, [string]$Name) {
    $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name))
}
function Find-Button($Root, [string]$Name) {
    Find-Control $Root $Name ([System.Windows.Automation.ControlType]::Button)
}
function Find-Control($Root, [string]$Name, $ControlType) {
    $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.AndCondition]::new(
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $ControlType),
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name)))
}
function Invoke-Button($Root, [string]$Name) {
    $button = Find-Button $Root $Name
    Assert-That ($null -ne $button) "Missing button: $Name"
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}
function Texts($Root) {
    @($Root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)) |
        ForEach-Object { $_.Current.Name })
}
function Start-App {
    $script:primary = Start-Process -FilePath $executable -WindowStyle Hidden -PassThru
    $owned.Add($script:primary)
    $window = Wait-Until { Get-Window } 'application window'
    Assert-That ($window.Current.Name -eq 'EasyToDoList — Hội quán nhiệm vụ') "Unexpected window: $($window.Current.Name)"
    return $window
}
function Add-Task($Window, [string]$Title) {
    $inputField = Find-Control $Window 'Tên nhiệm vụ' ([System.Windows.Automation.ControlType]::Edit)
    Assert-That ($null -ne $inputField) 'Missing quick-add field'
    $inputField.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Title)
    Invoke-Button $Window '+ Thêm nhiệm vụ'
    [void](Wait-Until { Find-Name $Window $Title } 'created quest appears')
}
function End-AppSession {
    $script:primary.Refresh()
    $result = [IntPtr]::Zero
    # WPF handles SessionEnding on its hidden Application HWND, not the main window.
    foreach ($windowHandle in [ReleaseDesktop]::WindowsFor($script:primary.Id)) {
        if ($script:primary.HasExited) { break }
        [void][ReleaseDesktop]::SendMessageTimeout($windowHandle, 0x11, [IntPtr]::Zero, [IntPtr]::Zero, 2, 5000, [ref]$result)
    }
    Assert-That ($script:primary.WaitForExit(15000)) 'App did not exit after simulated session ending'
    Assert-That ($script:primary.ExitCode -eq 0) 'App shutdown failed'
}
function Save-Screenshot([string]$Name) {
    $screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bitmap = [System.Drawing.Bitmap]::new($screen.Width, $screen.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try { $graphics.CopyFromScreen($screen.X, $screen.Y, 0, 0, $bitmap.Size); $bitmap.Save((Join-Path $OutputDirectory $Name), [System.Drawing.Imaging.ImageFormat]::Png) }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
}

try {
    Assert-That ([Environment]::OSVersion.Version.Build -ge 22000 -and [Environment]::Is64BitOperatingSystem) 'Windows 11 x64 is required'
    Assert-That (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) 'Clean sandbox must not have system dotnet'
    Assert-That (-not (Test-Path -LiteralPath $dataDirectory)) 'Sandbox must have no previous user data'
    Assert-That (-not (Test-Path -LiteralPath $uninstallKey)) 'Sandbox must have no previous installation'
    Record 'Clean Windows 11 profile without system dotnet or EasyToDoList data'
    Install $PreviousInstaller 'install-previous.log'
    Assert-That (Test-Path -LiteralPath $executable) 'Installed executable missing'
    $desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'EasyToDoList.lnk'
    $startShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'EasyToDoList.lnk'
    $shell = New-Object -ComObject WScript.Shell
    foreach ($shortcut in @($desktopShortcut, $startShortcut)) {
        Assert-That (Test-Path -LiteralPath $shortcut) 'Installed shortcut missing'
        Assert-That ($shell.CreateShortcut($shortcut).TargetPath -eq $executable) 'Shortcut target incorrect'
    }
    Record 'Fresh per-user install and Start/Desktop shortcuts'
    $window = Start-App
    Add-Task $window 'Hoàn thành để kiểm tra phần thưởng'
    $complete = Find-Name $window 'Hoàn thành: Hoàn thành để kiểm tra phần thưởng'
    $complete.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    $coins = Wait-Until { Texts $window | Where-Object { $_ -match '^Coin:\s*[1-9]\d*' } | Select-Object -First 1 } 'reward balance updates'
    Add-Task $window 'Giữ nhiệm vụ tiếng Việt sau nâng cấp'
    $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    [void](Wait-Until { $script:primary.Refresh(); $script:primary.MainWindowHandle -eq [IntPtr]::Zero -and -not $script:primary.HasExited } 'X hides to tray')
    $secondary = Start-Process -FilePath $executable -WindowStyle Hidden -PassThru
    $owned.Add($secondary)
    Assert-That ($secondary.WaitForExit(15000) -and $secondary.ExitCode -eq 0) 'Second instance did not exit cleanly'
    $window = Wait-Until { Get-Window } 'second instance restores primary'
    Record 'Offline app startup, task create/complete/reward and single-instance X-to-tray round trip'
    Invoke-Button $window 'Bắt đầu'
    [void](Wait-Until { Find-Button $window 'Tạm dừng' } 'Pomodoro starts')
    Start-Sleep -Seconds 2
    $blockedInstall = Run-Native $Installer @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-')
    Assert-That ($blockedInstall -ne 0) 'Upgrade must refuse a running application'
    $blockedUninstall = Run-Native (Join-Path $installDirectory 'unins000.exe') @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
    Assert-That ($blockedUninstall -ne 0) 'Uninstall must refuse a running application'
    Assert-That (-not $script:primary.HasExited) 'Installer must not kill the running timer'
    Record 'Upgrade and uninstall refuse running app without force-closing it'
    End-AppSession
    Assert-That ((Get-Content (Join-Path $dataDirectory 'Logs\app.log') -Raw) -match 'session-ending-saved') 'Final Pomodoro save was not logged'
    $dataHash = (Get-FileHash -LiteralPath $database -Algorithm SHA256).Hash
    Install $Installer 'upgrade.log'
    Assert-That ((Get-FileHash -LiteralPath $database -Algorithm SHA256).Hash -eq $dataHash) 'Upgrade modified user database before startup'
    Assert-That ((Get-ItemProperty $uninstallKey).DisplayVersion -eq $ExpectedVersion) 'Upgrade registration did not update'
    $window = Start-App
    [void](Wait-Until { Find-Name $window 'Giữ nhiệm vụ tiếng Việt sau nâng cấp' } 'task retained after upgrade')
    Assert-That ((Texts $window) -contains $coins) 'Upgrade lost reward balance'
    [void](Wait-Until { Find-Button $window 'Tiếp tục' } 'Pomodoro recovered paused')
    Save-Screenshot 'installed-dashboard.png'
    Record 'Upgrade preserves task, rewards and Paused Pomodoro after simulated WM_QUERYENDSESSION'
    Invoke-Button $window 'Cài đặt'
    $reduced = Wait-Until { Find-Control $window 'Giảm chuyển động' ([System.Windows.Automation.ControlType]::CheckBox) } 'settings page appears'
    $reduced.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    $volume = Find-Name $window 'Âm lượng tổng, phần trăm'
    $volume.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue(0)
    Invoke-Button $window 'Lưu cài đặt'
    [void](Wait-Until { Find-Name $window 'Đã lưu cài đặt.' } 'settings persist')
    End-AppSession
    $dataHash = (Get-FileHash -LiteralPath $database -Algorithm SHA256).Hash
    New-Item -Path $runKey -Force | Out-Null
    New-ItemProperty -Path $runKey -Name EasyToDoList -Value ('"' + $executable + '" --tray') -PropertyType String -Force | Out-Null
    $uninstallExit = Run-Native (Join-Path $installDirectory 'unins000.exe') @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG="' + (Join-Path $OutputDirectory 'uninstall.log') + '"'))
    Assert-That ($uninstallExit -eq 0) "Uninstall failed: $uninstallExit"
    Assert-That (-not (Test-Path -LiteralPath $executable)) 'Uninstall left executable'
    Assert-That (-not (Test-Path -LiteralPath $uninstallKey)) 'Uninstall left registration'
    Assert-That (-not (Test-Path -LiteralPath $desktopShortcut) -and -not (Test-Path -LiteralPath $startShortcut)) 'Uninstall left shortcuts'
    Assert-That (-not (Get-ItemProperty $runKey -Name EasyToDoList -ErrorAction SilentlyContinue)) 'Uninstall left owned startup value'
    Assert-That ((Get-FileHash -LiteralPath $database -Algorithm SHA256).Hash -eq $dataHash) 'Uninstall changed or removed user database'
    Assert-That (Test-Path (Join-Path $dataDirectory 'Logs')) 'Uninstall removed user logs'
    Record 'Uninstall removes executable/shortcuts/owned startup entry and preserves database and logs'
    Install $Installer 'reinstall.log'
    $window = Start-App
    [void](Wait-Until { Find-Name $window 'Giữ nhiệm vụ tiếng Việt sau nâng cấp' } 'reinstall retains task')
    Assert-That ((Texts $window) -contains $coins) 'Reinstall lost reward balance'
    Invoke-Button $window 'Cài đặt'
    Assert-That ((Find-Control $window 'Giảm chuyển động' ([System.Windows.Automation.ControlType]::CheckBox)).GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) 'Reinstall lost Reduced Motion'
    Assert-That ((Find-Name $window 'Âm lượng tổng, phần trăm').GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).Current.Value -eq 0) 'Reinstall lost muted setting'
    Invoke-Button $window 'Hôm nay'
    Invoke-Button $window 'Tiếp tục'
    [void](Wait-Until { Find-Button $window 'Tạm dừng' } 'recovered Pomodoro resumes')
    Start-Sleep -Seconds 3
    Stop-Process -Id $script:primary.Id -Force
    [void]$script:primary.WaitForExit(10000)
    $window = Start-App
    [void](Wait-Until { Find-Button $window 'Tiếp tục' } 'forced termination recovers paused')
    Assert-That ((Texts $window) -contains $coins) 'Crash recovery duplicated reward'
    Record 'Muted/Reduced Motion settings persist; forced process termination recovers Paused without extra reward'
    End-AppSession
    New-ItemProperty -Path $runKey -Name EasyToDoList -Value '"C:\Other\EasyToDoList.App.exe" --tray' -PropertyType String -Force | Out-Null
    Assert-That ((Run-Native (Join-Path $installDirectory 'unins000.exe') @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')) -eq 0) 'Second uninstall failed'
    Assert-That ((Get-ItemProperty $runKey).EasyToDoList -eq '"C:\Other\EasyToDoList.App.exe" --tray') 'Uninstall removed another installation startup command'
    Record 'Reinstall retains user state, timer resumes and uninstall preserves unrelated startup command'
    @{ status = 'passed'; steps = $steps.ToArray(); osBuild = [Environment]::OSVersion.Version.ToString(); utc = [DateTime]::UtcNow.ToString('O') } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $OutputDirectory 'result.json')
}
catch {
    Save-Screenshot 'failed-desktop.png'
    @{ status = 'failed'; steps = $steps.ToArray(); error = $_.Exception.ToString(); scriptStack = $_.ScriptStackTrace; utc = [DateTime]::UtcNow.ToString('O') } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $OutputDirectory 'result.json')
    throw
}
finally {
    foreach ($process in $owned) { $process.Refresh(); if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } }
}
