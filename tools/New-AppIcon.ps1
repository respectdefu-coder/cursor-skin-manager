param([Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$bitmap=[Drawing.Bitmap]::new(64,64)
$graphics=[Drawing.Graphics]::FromImage($bitmap)
try {
 $graphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $graphics.Clear([Drawing.Color]::Transparent)
 $background=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(35,60,90))
 $graphics.FillRectangle($background,2,2,60,60)
 $points=[Drawing.Point[]]@([Drawing.Point]::new(18,12),[Drawing.Point]::new(18,47),[Drawing.Point]::new(28,38),[Drawing.Point]::new(35,53),[Drawing.Point]::new(42,50),[Drawing.Point]::new(35,35),[Drawing.Point]::new(49,35))
 $graphics.FillPolygon([Drawing.Brushes]::White,$points)
 $pointer=$bitmap.GetHicon()
 try {$icon=[Drawing.Icon]::FromHandle($pointer);$file=[IO.File]::Create([IO.Path]::GetFullPath($OutputPath));try{$icon.Save($file)}finally{$file.Dispose();$icon.Dispose()}}
 finally {
  if(-not ('CursorManagerIconNative' -as [type])){Add-Type 'using System;using System.Runtime.InteropServices;public static class CursorManagerIconNative{[DllImport("user32.dll")]public static extern bool DestroyIcon(IntPtr icon);}' }
  [CursorManagerIconNative]::DestroyIcon($pointer) | Out-Null
 }
 $background.Dispose()
}finally{$graphics.Dispose();$bitmap.Dispose()}
