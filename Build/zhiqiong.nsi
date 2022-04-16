Name "志琼·原神地图"
OutFile "..\Dist\zhiqiong_installer_1.0.2.exe"
Unicode True
InstallDir "$TEMP"
RequestExecutionLevel admin
ManifestDPIAware true
ShowInstDetails show
BrandingText " "
SetCompressor zlib
AutoCloseWindow true
Icon "map.ico"
InstallColors 0 4008636142
LoadLanguageFile "${NSISDIR}\Contrib\Language files\SimpChinese.nlf"
XPStyle on
!include LogicLib.nsh
!include WinMessages.nsh
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
		DetailPrint "Webview2未安装。"
		File "MicrosoftEdgeWebview2Setup.exe"
		nsExec::ExecToLog '"$INSTDIR\MicrosoftEdgeWebview2Setup.exe" /install'
		SetDetailsPrint both
    ${Else}
		DetailPrint "Webview2已安装。"
	${EndIf}
FunctionEnd
Section "Dummy Section" SecDummy
    MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION "本次安装需要在您的系统中信任该软件的签名根证书，请确认您信任该软件的发布者！" IDOK OK IDCANCEL CANCEL
    OK:
        SetOutPath "$INSTDIR"
        DetailPrint "> 检查运行环境..."
        Call installWebView2
        DetailPrint "> 信任签名证书..."
        File /oname=@yuehaiteam_zhiqiong.cer "zhiqiong.cer"
        nsExec::ExecToLog "powershell.exe -Command certutil -addstore 'Root' '$INSTDIR\@yuehaiteam_zhiqiong.cer'"
        Delete "$INSTDIR\@yuehaiteam_zhiqiong.cer"
        DetailPrint "> 安装程序组件..."
        File /oname=@yuehaiteam_zhiqiong.msix "..\AppPackages\zhiqiong_1.0.3.0_x64_Test\zhiqiong_1.0.3.0_x64.msix"
        nsExec::ExecToLog "powershell.exe -Command Add-AppxPackage -Path '$INSTDIR\@yuehaiteam_zhiqiong.msix' -DeferRegistrationWhenPackagesAreInUse"
        Delete "$INSTDIR\@yuehaiteam_zhiqiong.msix"
        DetailPrint "> 放行本地连接..."
        nsExec::ExecToLog "CheckNetIsolation LoopbackExempt -a -n=zhiqiong_tbtgzvrf5srwm"
        DetailPrint "> 激活应用模块..."
        nsExec::ExecToLog "powershell.exe -Command start ms-gamebar:activate/zhiqiong_tbtgzvrf5srwm_App_zhiqiong"
        DetailPrint "> 即将完成..."
        MessageBox MB_OK|MB_ICONINFORMATION "安装完成！请按Win+G打开Xbox Game Bar使用悬浮地图。"
        Goto END
    CANCEL:
        Quit
    END:
SectionEnd