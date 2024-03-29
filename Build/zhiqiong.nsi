﻿!define UWPVER 1.1.1
Name "志琼·原神地图"
OutFile "..\Dist\zhiqiong-${UWPVER}.exe"
Unicode True
InstallDir "$TEMP"
RequestExecutionLevel admin
ManifestSupportedOS {8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}
ManifestDPIAware true
ShowInstDetails show
BrandingText " "
SetCompressor lzma
Icon "map.ico"
InstallColors 0 4008636142
LoadLanguageFile "${NSISDIR}\Contrib\Language files\SimpChinese.nlf"
XPStyle on
!include x64.nsh
!include WinVer.nsh
!include LogicLib.nsh
!include WinMessages.nsh
!packhdr "$%TEMP%\exehead.tmp" 'upx.exe "$%TEMP%\exehead.tmp"'
!finalize "sign.bat ..\Dist\zhiqiong-${UWPVER}.exe"
Page instfiles "" LogFont
Function LogFont
    Push $R0
    Push $R1
    FindWindow $R0 "#32770" "" $HWNDPARENT
    CreateFont $R1 "Microsoft Yahei" "8" "400"
    GetDlgItem $R0 $R0 1016
    SendMessage $R0 ${WM_SETFONT} $R1 0
    FindWindow $R0 "#32770" "" $HWNDPARENT
    CreateFont $R1 "Microsoft Yahei" "8" "400"
    GetDlgItem $R0 $R0 1006
    SendMessage $R0 ${WM_SETFONT} $R1 0
    Pop $R1
    Pop $R0
FunctionEnd
Function installWebView2
	# If this key exists and is not empty then webview2 is already installed
	ReadRegStr $0 HKLM \
        	"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" "pv"
	${If} ${Errors} 
	${OrIf} $0 == ""
		DetailPrint "    Webview2未安装。"
		File /oname=$PLUGINSDIR\MicrosoftEdgeWebview2Setup.exe "MicrosoftEdgeWebview2Setup.exe"
		nsExec::ExecToLog '"$PLUGINSDIR\MicrosoftEdgeWebview2Setup.exe" /install'
		SetDetailsPrint both
    ${Else}
		DetailPrint "    Webview2已安装。"
	${EndIf}
FunctionEnd
Function checkXboxGameBar
    nsExec::Exec "powershell -Command if((Get-AppxPackage Microsoft.XboxGamingOverlay).Version -gt [System.Version]'5.420.9252.0'){ exit 0 }else{ exit 1 }"
    Pop $0
    ${If} $0 != 0 
        MessageBox MB_OK|MB_ICONSTOP "当前系统未安装Xbox Game Bar或版本过低，请手动安装或升级后继续。"
        DetailPrint "> 请访问以下链接或到应用商店安装或升级Xbox Game Bar："
        DetailPrint "https://apps.microsoft.com/store/detail/xbox-game-bar/9NZKPSTSNW4P"
        Abort
    ${Else}
		DetailPrint "    Xbox Game Bar已安装。"
    ${EndIf}
FunctionEnd
Function checkVcRuntime
    nsExec::Exec "reg query HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"
    Pop $0
    ${If} $0 != 0 
        MessageBox MB_OK|MB_ICONSTOP "当前系统未安装微软基础运行库，请手动安装后继续。"
        DetailPrint "> 请访问以下链接或安装或升级微软基础运行库："
        DetailPrint "https://aka.ms/vs/17/release/vc_redist.x64.exe"
        Abort
    ${Else}
        DetailPrint "    微软基础运行库已安装。"
    ${EndIf}
FunctionEnd
Section "Dummy Section" SecDummy
    DetailPrint "> 检查系统运行环境..."
    ${IfNot} ${AtLeastWin10}
        MessageBox MB_OK|MB_ICONSTOP "本程序仅支持Windows 10及以上版本"
        Quit
    ${EndIf}
    ${IfNot} ${RunningX64}
        MessageBox MB_OK|MB_ICONSTOP "本程序仅支持64位系统"
        Quit
    ${EndIf} 
    nsExec::Exec "powershell -Command echo 1"
    Pop $0
    ${If} $0 != 0 
        MessageBox MB_OK|MB_ICONSTOP "当前系统的PowerShell运行环境存在问题，请修复后重试。"
        Quit
    ${EndIf}
    DetailPrint "    完成。"

    DetailPrint "> 检查Xbox Game Bar..."
    Call checkXboxGameBar

    DetailPrint "> 检查微软基础运行库..."
    Call checkVcRuntime

    DetailPrint "> 检查Webview2运行环境..."
    Call installWebView2

    File /oname=$PLUGINSDIR\app.cer "zhiqiong.cer"
    DetailPrint "> 检查签名证书..."
    nsExec::Exec "powershell -Command if(Get-ChildItem -Path Cert:\LocalMachine\TrustedPeople | Where Thumbprint -eq (Get-PfxCertificate '$PLUGINSDIR\app.cer').Thumbprint) {exit 0}else{exit 1}"
    Pop $0
    ${If} $0 != 0 
        DetailPrint "    - 证书未安装，申请授权..."
        MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION "本次安装需要在您的系统中信任该软件的签名证书，请确认您信任该软件的发布者！" IDOK OK IDCANCEL CANCEL
    ${Else}
        DetailPrint "    - 证书已安装，尝试更新..."
        goto UNOK
    ${EndIf}
    CANCEL:
        Quit
    OK:
        DetailPrint "    - 安装新证书..."
        nsExec::ExecToLog "powershell.exe -Command certutil -addstore 'TrustedPeople' '$PLUGINSDIR\app.cer'"
    UNOK:
        DetailPrint "    - 删除旧证书..."
        nsExec::ExecToLog "powershell.exe -Command certutil -delstore 'Root' (Get-PfxCertificate '$PLUGINSDIR\app.cer').Thumbprint"
        DetailPrint "> 解压依赖文件..."
        File /oname=$PLUGINSDIR\Microsoft.NET.Native.Framework.2.2.appx "..\AppPackages\zhiqiong_${UWPVER}.0_x64_Test\Dependencies\x64\Microsoft.NET.Native.Framework.2.2.appx"
        File /oname=$PLUGINSDIR\Microsoft.NET.Native.Runtime.2.2.appx "..\AppPackages\zhiqiong_${UWPVER}.0_x64_Test\Dependencies\x64\Microsoft.NET.Native.Runtime.2.2.appx"
        File /oname=$PLUGINSDIR\Microsoft.VCLibs.x64.14.00.appx "..\AppPackages\zhiqiong_${UWPVER}.0_x64_Test\Dependencies\x64\Microsoft.VCLibs.x64.14.00.appx"
        File /oname=$PLUGINSDIR\Microsoft.VCLibs.x64.14.00.Desktop.appx "..\AppPackages\zhiqiong_${UWPVER}.0_x64_Test\Dependencies\x64\Microsoft.VCLibs.x64.14.00.Desktop.appx"
        File /oname=$PLUGINSDIR\app.msix "..\AppPackages\zhiqiong_${UWPVER}.0_x64_Test\zhiqiong_${UWPVER}.0_x64.msix"
        DetailPrint "> 安装程序组件..."
        nsExec::ExecToLog "powershell.exe -Command Add-AppxPackage -Path '$PLUGINSDIR\app.msix' -ForceTargetApplicationShutdown -DependencyPath '$PLUGINSDIR\Microsoft.NET.Native.Framework.2.2.appx','$PLUGINSDIR\Microsoft.NET.Native.Runtime.2.2.appx','$PLUGINSDIR\Microsoft.VCLibs.x64.14.00.appx','$PLUGINSDIR\Microsoft.VCLibs.x64.14.00.Desktop.appx'"
        Pop $0
        ${If} $0 != 0 
            MessageBox MB_OK|MB_ICONSTOP "安装程序组件出错，请检查错误信息。"
            DetailPrint "> 安装程序组件出错，请检查错误信息。"
            Abort
        ${EndIf}
        DetailPrint "> 放行本地连接..."
        nsExec::ExecToLog "CheckNetIsolation LoopbackExempt -a -n=zhiqiong_tbtgzvrf5srwm"
        DetailPrint "> 注册辅助插件..."
        File /oname=$PLUGINSDIR\cocogoat-control.exe cocogoat-control.exe
        nsExec::ExecToLog "$PLUGINSDIR\cocogoat-control.exe --install"
        Pop $0
        ${If} $0 != 0
            MessageBox MB_OK|MB_ICONSTOP "注册辅助插件出错，追踪功能将可能无法正常使用。"
            DetailPrint ">    - 注册辅助插件失败！"
        ${EndIf}
        DetailPrint "    - 更新辅助插件..."
        nsExec::ExecToLog "powershell -Command Invoke-WebRequest -Uri (Invoke-RestMethod -Uri 'https://77.xyget.cn/upgrade/frostflake.json').url -OutFile '$PLUGINSDIR\cocogoat-control.exe'"
        Pop $0
        ${If} $0 == 0 
            DetailPrint "    - 下载更新成功..."
            nsExec::ExecToLog "$PLUGINSDIR\cocogoat-control.exe --install"Pop $0
            ${If} $0 == 0 
                DetailPrint "    - 安装更新成功..."
            ${EndIf}
        ${EndIf}
        DetailPrint "> 激活应用模块..."
        nsExec::ExecToLog "powershell.exe -Command start ms-gamebar:activate/zhiqiong_tbtgzvrf5srwm_App_zhiqiong"
        DetailPrint "> 即将完成..."
        MessageBox MB_OK|MB_ICONINFORMATION "安装完成！请按Win+G打开Xbox Game Bar使用悬浮地图。"
        Quit
SectionEnd