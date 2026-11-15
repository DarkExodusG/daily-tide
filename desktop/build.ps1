# Builds Steady.exe with the C# compiler that ships with Windows.
# Needs the Microsoft.Web.WebView2 NuGet package (lib/net462 + runtimes/win-x64/native) copied into .\build first.
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $csc -nologo -target:winexe -platform:x64 -optimize+ -out:build\Steady.exe -win32icon:..\icons\steady.ico -win32manifest:app.manifest `
  -r:build\Microsoft.Web.WebView2.Core.dll -r:build\Microsoft.Web.WebView2.WinForms.dll -r:System.Windows.Forms.dll -r:System.Drawing.dll Steady.cs
