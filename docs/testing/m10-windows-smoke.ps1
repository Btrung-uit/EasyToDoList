param(
    [string]$Executable = (Join-Path $PSScriptRoot '../../src/EasyToDoList.App/bin/Release/net10.0-windows/EasyToDoList.App.exe')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class M10Desktop {
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
}
'@

function Assert-That($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Wait-Until([scriptblock]$Check, [string]$Message) {
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $value = & $Check
        if ($value) { return $value }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Timeout: $Message"
}

function Find-Name($Root, [string]$Name) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Invoke-Element($Element) {
    Assert-That ($null -ne $Element) 'UI control not found.'
    $pattern = $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}

function Get-AppWindow {
    $script:primary.Refresh()
    if ($script:primary.HasExited -or $script:primary.MainWindowHandle -eq [IntPtr]::Zero) { return $null }
    [System.Windows.Automation.AutomationElement]::FromHandle($script:primary.MainWindowHandle)
}

function Start-App([string[]]$Arguments = @()) {
    $script:primary = Start-Process -FilePath $script:Executable -ArgumentList ($Arguments + ' ') -PassThru -WindowStyle Hidden
    $script:ownedProcesses.Add($script:primary)
    if ($Arguments -notcontains '--tray') {
        $window = Wait-Until { Get-AppWindow } 'Dashboard window appears'
        # Give the app real user input before testing Windows foreground activation.
        $rectangle = $window.Current.BoundingRectangle
        [void][M10Desktop]::SetCursorPos([int]($rectangle.X + 50), [int]($rectangle.Y + 16))
        [M10Desktop]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
        [M10Desktop]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
        return $window
    }
}

function Get-TrayIcon {
    $desktop = [System.Windows.Automation.AutomationElement]::RootElement
    $taskbar = $desktop.FindFirst([System.Windows.Automation.TreeScope]::Children,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ClassNameProperty, 'Shell_TrayWnd'))
    $icon = Wait-Until {
        $candidate = $taskbar.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition) | Where-Object { $_.Current.Name.Trim() -eq 'EasyToDoList' } | Select-Object -First 1
        if ($candidate) { return $candidate }
        $popup = $desktop.FindFirst([System.Windows.Automation.TreeScope]::Children,
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ClassNameProperty, 'TopLevelWindowForOverflowXamlIsland'))
        if ($popup) {
            return $popup.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                [System.Windows.Automation.Condition]::TrueCondition) | Where-Object { $_.Current.Name.Trim() -eq 'EasyToDoList' } | Select-Object -First 1
        }
        $overflow = $taskbar.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition) | Where-Object { $_.Current.AutomationId -eq 'SystemTrayIcon' -and $_.Current.Name -match 'hidden|overflow' } | Select-Object -First 1
        Invoke-Element $overflow
    } 'tray overflow icon appears'
    return $icon
}

function Open-TrayMenu {
    $desktop = [System.Windows.Automation.AutomationElement]::RootElement
    $icon = Get-TrayIcon
    $rectangle = $icon.Current.BoundingRectangle
    Assert-That (-not $rectangle.IsEmpty) 'Tray icon has no clickable bounds.'
    [void][M10Desktop]::SetCursorPos([int]($rectangle.X + $rectangle.Width / 2), [int]($rectangle.Y + $rectangle.Height / 2))
    [M10Desktop]::mouse_event(0x0008, 0, 0, 0, [UIntPtr]::Zero)
    [M10Desktop]::mouse_event(0x0010, 0, 0, 0, [UIntPtr]::Zero)
    Wait-Until {
        $desktop.FindFirst([System.Windows.Automation.TreeScope]::Children,
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Menu))
    } 'tray context menu opens'
}

function Record-Pass([string]$Name) {
    $script:passedCount++
    Write-Output "PASS: $Name"
}

function Get-PomodoroRows {
    $json = node -e @'
const {DatabaseSync}=require('node:sqlite');
const db=new DatabaseSync(require('node:path').join(process.env.LOCALAPPDATA,'EasyToDoList','easytodolist.db'),{readOnly:true});
console.log(JSON.stringify(db.prepare('SELECT Id,Status,RemainingSeconds FROM PomodoroSessions').all()));
db.close();
'@
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect Pomodoro sessions safely.' }
    foreach ($row in ($json | ConvertFrom-Json)) { $row }
}

Assert-That (Test-Path -LiteralPath $Executable) 'Build the Release executable before running this check.'
Assert-That (-not (Get-Process EasyToDoList.App -ErrorAction SilentlyContinue)) 'Exit the existing app before testing.'
$originalSessions = @(Get-PomodoroRows)
Assert-That (@($originalSessions | Where-Object { $_.Status -in 0, 1, 2 }).Count -eq 0) 'Finish the active Pomodoro before testing Focus Exit.'
$testSessionId = $null
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$ownedProcesses = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$passedCount = 0
$runPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$originalRun = Get-ItemProperty -LiteralPath $runPath -ErrorAction SilentlyContinue
$hadRun = $null -ne $originalRun -and $null -ne $originalRun.PSObject.Properties['EasyToDoList']
$originalRunValue = if ($hadRun) { $originalRun.EasyToDoList } else { $null }
$originalAutoStart = $null
$settingsNeedRestore = $false
$primary = $null
try {
    $window = Start-App
    Assert-That ($window.Current.Name -like '*Hội quán*') 'App must start on Dashboard.'
    Invoke-Element (Find-Name $window 'Thu gọn thành Overlay')
    $window = Wait-Until { $w = Get-AppWindow; if ($w -and $w.Current.Name -like '*Overlay*') { $w } } 'Overlay mode'
    Record-Pass 'Dashboard to Overlay'

    $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    [void](Wait-Until { $primary.Refresh(); -not $primary.HasExited -and $primary.MainWindowHandle -eq [IntPtr]::Zero } 'X hides app without exiting')
    Record-Pass 'X hides to tray and process survives'

    foreach ($mode in @('hidden', 'minimized')) {
        if ($mode -eq 'minimized') {
            $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Minimized)
        }
        $secondary = Start-Process -FilePath $Executable -PassThru -WindowStyle Hidden
        $ownedProcesses.Add($secondary)
        Assert-That ($secondary.WaitForExit(15000)) 'Second process must exit.'
        Assert-That ($secondary.ExitCode -eq 0) 'Second process must exit successfully.'
        $window = Wait-Until {
            $w = Get-AppWindow
            if ($w -and $w.Current.Name -like '*Hội quán*' -and $w.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Current.WindowVisualState -eq [System.Windows.Automation.WindowVisualState]::Normal) { $w }
        } "second launch restores $mode primary"
        [void](Wait-Until {
            $foregroundProcessId = [uint32]0
            [void][M10Desktop]::GetWindowThreadProcessId([M10Desktop]::GetForegroundWindow(), [ref]$foregroundProcessId)
            $foregroundProcessId -eq $primary.Id
        } 'primary is the foreground window')
        Record-Pass "Single instance restores $mode primary and secondary exits"
    }

    $menu = Open-TrayMenu
    foreach ($name in @('Overlay', 'Dashboard', 'Exit')) { Assert-That ($null -ne (Find-Name $menu $name)) "Tray entry missing: $name" }
    Invoke-Element (Find-Name $menu 'Overlay')
    [void](Wait-Until { $w = Get-AppWindow; $w -and $w.Current.Name -like '*Overlay*' } 'tray Overlay action')
    $menu = Open-TrayMenu
    Invoke-Element (Find-Name $menu 'Dashboard')
    $window = Wait-Until { $w = Get-AppWindow; if ($w -and $w.Current.Name -like '*Hội quán*') { $w } } 'tray Dashboard action'
    Record-Pass 'Real tray menu Overlay and Dashboard actions'

    $menu = Open-TrayMenu
    Invoke-Element (Find-Name $menu 'Overlay')
    $icon = Get-TrayIcon
    $rectangle = $icon.Current.BoundingRectangle
    [void][M10Desktop]::SetCursorPos([int]($rectangle.X + $rectangle.Width / 2), [int]($rectangle.Y + $rectangle.Height / 2))
    foreach ($click in 1..2) {
        [M10Desktop]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
        [M10Desktop]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 75
    }
    $window = Wait-Until { $w = Get-AppWindow; if ($w -and $w.Current.Name -like '*Hội quán*') { $w } } 'tray double-click opens Dashboard'
    Record-Pass 'Real tray double-click opens Dashboard'

    Invoke-Element (Find-Name $window 'Cài đặt')
    $autoStart = Wait-Until { Find-Name (Get-AppWindow) 'Khởi động cùng Windows' } 'auto-start checkbox'
    $toggle = $autoStart.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    $originalAutoStart = $toggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On
    $settingsNeedRestore = $true
    foreach ($enabled in @($true, $false)) {
        if (($toggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) -ne $enabled) { $toggle.Toggle() }
        Invoke-Element (Find-Name (Get-AppWindow) 'Lưu cài đặt')
        [void](Wait-Until {
            $value = (Get-ItemProperty -LiteralPath $runPath -Name EasyToDoList -ErrorAction SilentlyContinue).EasyToDoList
            if ($enabled) { $value -eq ('"' + $Executable + '" --tray') } else { $null -eq $value }
        } 'Save synchronizes startup registry')
    }
    Record-Pass 'Auto-start Save creates quoted executable --tray and removes value'
    $exitEnabled = -not $originalAutoStart
    if (($toggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) -ne $exitEnabled) { $toggle.Toggle() }
    $menu = Open-TrayMenu
    Invoke-Element (Find-Name $menu 'Exit')
    Assert-That ($primary.WaitForExit(15000)) 'Tray Exit must terminate the process.'
    Assert-That ($primary.ExitCode -eq 0) 'Tray Exit must return zero.'
    $runValue = (Get-ItemProperty -LiteralPath $runPath -Name EasyToDoList -ErrorAction SilentlyContinue).EasyToDoList
    Assert-That (($exitEnabled -and $runValue -eq ('"' + $Executable + '" --tray')) -or (-not $exitEnabled -and $null -eq $runValue)) 'Exit must synchronize unsaved auto-start change.'
    Record-Pass 'Tray Exit terminates the executable cleanly'
    Record-Pass 'Tray Exit persists unsaved auto-start change'

    [void](Start-App @('--tray'))
    [void](Wait-Until {
        $primary.Refresh()
        -not $primary.HasExited -and $primary.MainWindowHandle -eq [IntPtr]::Zero -and $primary.TotalProcessorTime.TotalMilliseconds -gt 0
    } '--tray process starts')
    # Require a real tray menu before concluding startup completed in hidden mode.
    $menu = Wait-Until { Open-TrayMenu } '--tray icon ready'
    Invoke-Element (Find-Name $menu 'Dashboard')
    $window = Wait-Until { Get-AppWindow } '--tray Dashboard opens'
    Record-Pass '--tray launch stays alive and opens from tray'
    Invoke-Element (Find-Name $window 'Cài đặt')
    $box = Wait-Until { Find-Name (Get-AppWindow) 'Khởi động cùng Windows' } 'restored settings view'
    $toggle = $box.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    Assert-That (($toggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) -eq $exitEnabled) 'Auto-start must persist across restart.'
    $toggle.Toggle()
    Invoke-Element (Find-Name (Get-AppWindow) 'Thoát')
    Assert-That ($primary.WaitForExit(15000)) 'Dashboard Exit must terminate the process.'
    $runValue = (Get-ItemProperty -LiteralPath $runPath -Name EasyToDoList -ErrorAction SilentlyContinue).EasyToDoList
    Assert-That (($originalAutoStart -and $runValue -eq ('"' + $Executable + '" --tray')) -or (-not $originalAutoStart -and $null -eq $runValue)) 'Dashboard Exit must restore original auto-start setting.'
    $settingsNeedRestore = $false
    Record-Pass 'Dashboard Exit terminates the executable'
    Record-Pass 'Dashboard Exit persists opposite auto-start change and restores original setting'

    $window = Start-App
    Invoke-Element (Find-Name $window 'Mở đồng hồ')
    $window = Wait-Until { $w = Get-AppWindow; if ($w -and $w.Current.Name -like '*Overlay*') { $w } } 'Pomodoro Overlay'
    Invoke-Element (Find-Name $window 'Bắt đầu')
    [void](Wait-Until { Find-Name (Get-AppWindow) 'Đang chạy' } 'Focus is running')
    $newRows = @(Get-PomodoroRows | Where-Object { $_.Id -notin $originalSessions.Id })
    Assert-That ($newRows.Count -eq 1) 'Exactly one test session must have been created.'
    Assert-That ($newRows[0].Id -is [string]) 'Test session must have one scalar identifier.'
    $testSessionId = $newRows[0].Id
    Assert-That ($newRows[0].Status -eq 0) 'Test session must be Running.'
    $menu = Open-TrayMenu
    Invoke-Element (Find-Name $menu 'Exit')
    Assert-That ($primary.WaitForExit(15000)) 'Focus Exit must terminate the process.'
    $paused = Get-PomodoroRows | Where-Object { $_.Id -eq $testSessionId }
    Assert-That ($paused.Status -eq 1 -and $paused.RemainingSeconds -gt 0) 'Focus Exit must persist Paused with remaining time.'
    Record-Pass 'Exit while Focus is running pauses and terminates the executable'
    $window = Start-App
    Invoke-Element (Find-Name $window 'Mở đồng hồ')
    [void](Wait-Until { Find-Name (Get-AppWindow) 'Đã tạm dừng' } 'restart shows Paused')
    $reopened = Get-PomodoroRows | Where-Object { $_.Id -eq $testSessionId }
    Assert-That ($reopened.RemainingSeconds -eq $paused.RemainingSeconds) 'Restart must preserve paused time.'
    $menu = Open-TrayMenu
    Invoke-Element (Find-Name $menu 'Exit')
    Assert-That ($primary.WaitForExit(15000)) 'Restarted app must exit.'
    Record-Pass 'Restart preserves Paused Focus and remaining time'
}
catch {
    Write-Host "SMOKE FAILURE: $($_.Exception.Message)"
    throw
}
finally {
    try {
    if ($settingsNeedRestore) {
        $primary.Refresh()
        if ($primary.HasExited) { [void](Start-App) }
        elseif (-not (Get-AppWindow)) {
            $activator = Start-Process -FilePath $Executable -PassThru -WindowStyle Hidden
            $ownedProcesses.Add($activator)
            [void]$activator.WaitForExit(15000)
        }
    }
    # Only close processes started by this script. Never terminate an existing instance.
    foreach ($owned in $ownedProcesses) {
        try {
        $owned.Refresh()
        if (-not $owned.HasExited) {
            if ($owned.Id -eq $primary.Id) {
                $window = Get-AppWindow
                if ($window) {
                    if ($window.Current.Name -like '*Overlay*') {
                        Invoke-Element (Find-Name $window 'Mở Dashboard')
                        $window = Get-AppWindow
                    }
                    if ($settingsNeedRestore) {
                    Invoke-Element (Find-Name $window 'Cài đặt')
                    $box = Find-Name $window 'Khởi động cùng Windows'
                    if ($box) {
                        $toggle = $box.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
                        if (($toggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) -ne $originalAutoStart) { $toggle.Toggle() }
                        Invoke-Element (Find-Name $window 'Lưu cài đặt')
                    }
                    }
                    Invoke-Element (Find-Name $window 'Thoát')
                    [void]$owned.WaitForExit(5000)
                }
            }
        }
        }
        finally {
            $owned.Refresh()
            if (-not $owned.HasExited) { $owned.Kill(); $owned.WaitForExit() }
            $owned.Dispose()
        }
    }
    }
    finally {
        if ($hadRun) { Set-ItemProperty -LiteralPath $runPath -Name EasyToDoList -Value $originalRunValue }
        else { Remove-ItemProperty -LiteralPath $runPath -Name EasyToDoList -ErrorAction SilentlyContinue }
        if ($testSessionId) {
            Assert-That (-not (Get-Process EasyToDoList.App -ErrorAction SilentlyContinue)) 'Stop app before cleaning up its test session.'
            node -e @'
const assert=require('node:assert/strict');
const {DatabaseSync}=require('node:sqlite');
const id=process.argv[1]; assert.match(id,/^[0-9a-f-]{36}$/i);
const db=new DatabaseSync(require('node:path').join(process.env.LOCALAPPDATA,'EasyToDoList','easytodolist.db'));
db.exec('BEGIN IMMEDIATE');
try {
 const row=db.prepare('SELECT Status,RemainingSeconds FROM PomodoroSessions WHERE Id=?').get(id);
 assert(row && [0,1].includes(row.Status) && row.RemainingSeconds>0,'Only the unfinished test session can be removed');
 assert.equal(db.prepare('SELECT COUNT(*) AS n FROM RewardTransactions WHERE SourceId=?').get(id).n,0,'Test session must have no reward');
 assert.equal(db.prepare('DELETE FROM PomodoroSessions WHERE Id=?').run(id).changes,1);
 db.exec('COMMIT');
} catch(error) { db.exec('ROLLBACK'); throw error; }
finally { db.close(); }
'@ $testSessionId
            if ($LASTEXITCODE -ne 0) { throw "Could not clean up test session $testSessionId; do not remove any other data." }
        }
    }
}
Write-Output "Summary: $passedCount checks passed."
