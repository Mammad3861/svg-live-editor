[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $repositoryRoot 'src\SvgLiveEditor\Assets\SvgLiveEditor.ico'
$sizes = @(16, 24, 32, 48, 64, 128, 256)

function New-RoundedRectanglePath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $diameter = $Radius * 2
    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc(
        $X + $Width - $diameter,
        $Y + $Height - $diameter,
        $diameter,
        $diameter,
        0,
        90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconPng {
    param([int]$Size)

    $scale = $Size / 256.0
    $bitmap = [Drawing.Bitmap]::new(
        $Size,
        $Size,
        [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $background = [Drawing.SolidBrush]::new(
            [Drawing.ColorTranslator]::FromHtml('#0b1f3a'))
        $whitePen = [Drawing.Pen]::new(
            [Drawing.Color]::White,
            [Math]::Max(1.5, 22 * $scale))
        $accentPen = [Drawing.Pen]::new(
            [Drawing.ColorTranslator]::FromHtml('#8b5cf6'),
            [Math]::Max(1, 7 * $scale))
        $blueBrush = [Drawing.SolidBrush]::new(
            [Drawing.ColorTranslator]::FromHtml('#60a5fa'))
        $purpleBrush = [Drawing.SolidBrush]::new(
            [Drawing.ColorTranslator]::FromHtml('#8b5cf6'))
        $backgroundPath = New-RoundedRectanglePath `
            (12 * $scale) `
            (12 * $scale) `
            (232 * $scale) `
            (232 * $scale) `
            (44 * $scale)
        try {
            $whitePen.StartCap = [Drawing.Drawing2D.LineCap]::Round
            $whitePen.EndCap = [Drawing.Drawing2D.LineCap]::Round
            $whitePen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
            $accentPen.StartCap = [Drawing.Drawing2D.LineCap]::Round
            $accentPen.EndCap = [Drawing.Drawing2D.LineCap]::Round

            $graphics.FillPath($background, $backgroundPath)
            $graphics.DrawLines(
                $whitePen,
                [Drawing.PointF[]]@(
                    [Drawing.PointF]::new(92 * $scale, 76 * $scale),
                    [Drawing.PointF]::new(52 * $scale, 128 * $scale),
                    [Drawing.PointF]::new(92 * $scale, 180 * $scale)))
            $graphics.DrawLines(
                $whitePen,
                [Drawing.PointF[]]@(
                    [Drawing.PointF]::new(164 * $scale, 76 * $scale),
                    [Drawing.PointF]::new(204 * $scale, 128 * $scale),
                    [Drawing.PointF]::new(164 * $scale, 180 * $scale)))
            $graphics.DrawLine(
                $accentPen,
                160 * $scale,
                190 * $scale,
                198 * $scale,
                210 * $scale)
            $graphics.FillEllipse(
                $blueBrush,
                (160 - 9) * $scale,
                (190 - 9) * $scale,
                18 * $scale,
                18 * $scale)
            $graphics.FillEllipse(
                $purpleBrush,
                (198 - 11) * $scale,
                (210 - 11) * $scale,
                22 * $scale,
                22 * $scale)
        }
        finally {
            $backgroundPath.Dispose()
            $background.Dispose()
            $whitePen.Dispose()
            $accentPen.Dispose()
            $blueBrush.Dispose()
            $purpleBrush.Dispose()
        }

        $stream = [IO.MemoryStream]::new()
        try {
            $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
            return $stream.ToArray()
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$frames = foreach ($size in $sizes) {
    [PSCustomObject]@{
        Size = $size
        Bytes = New-IconPng $size
    }
}

$outputDirectory = Split-Path -Parent $outputPath
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$fileStream = [IO.File]::Open(
    $outputPath,
    [IO.FileMode]::Create,
    [IO.FileAccess]::Write,
    [IO.FileShare]::None)
$writer = [IO.BinaryWriter]::new($fileStream)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$frames.Count)

    $offset = 6 + (16 * $frames.Count)
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -ge 256) { [byte]0 } else { [byte]$frame.Size }
        $writer.Write($dimension)
        $writer.Write($dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length)
        $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }

    foreach ($frame in $frames) {
        $writer.Write([byte[]]$frame.Bytes)
    }
}
finally {
    $writer.Dispose()
}

Write-Host "Created $outputPath with sizes: $($sizes -join ', ')"
