[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ExecutablePath,

    [Parameter(Mandatory)]
    [string]$IconPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if ($null -eq ('SvgLiveEditor.NativeIconResourceReader' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SvgLiveEditor
{
    public static class NativeIconResourceReader
    {
        private const uint LoadLibraryAsDataFile = 0x00000002;
        private const uint LoadLibraryAsImageResource = 0x00000020;

        private delegate bool EnumResourceNameCallback(
            IntPtr module,
            IntPtr type,
            IntPtr name,
            IntPtr parameter);

        public static int[] EnumerateIds(string path, int type)
        {
            IntPtr module = LoadLibraryEx(
                path,
                IntPtr.Zero,
                LoadLibraryAsDataFile | LoadLibraryAsImageResource);
            if (module == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                List<int> ids = new List<int>();
                bool namedResource = false;
                EnumResourceNameCallback callback = delegate(
                    IntPtr callbackModule,
                    IntPtr callbackType,
                    IntPtr name,
                    IntPtr parameter)
                {
                    ulong rawName = unchecked((ulong)name.ToInt64());
                    if ((rawName >> 16) != 0)
                    {
                        namedResource = true;
                        return false;
                    }
                    ids.Add((ushort)rawName);
                    return true;
                };

                bool completed = EnumResourceNames(
                    module,
                    (IntPtr)type,
                    callback,
                    IntPtr.Zero);
                GC.KeepAlive(callback);
                if (namedResource)
                {
                    throw new InvalidOperationException(
                        "Named icon resources are not supported by the package audit.");
                }
                if (!completed)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                ids.Sort();
                return ids.ToArray();
            }
            finally
            {
                FreeLibrary(module);
            }
        }

        public static byte[] Read(string path, int type, int id)
        {
            IntPtr module = LoadLibraryEx(
                path,
                IntPtr.Zero,
                LoadLibraryAsDataFile | LoadLibraryAsImageResource);
            if (module == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                IntPtr resourceInfo = FindResource(
                    module,
                    (IntPtr)id,
                    (IntPtr)type);
                if (resourceInfo == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                uint length = SizeofResource(module, resourceInfo);
                IntPtr loaded = LoadResource(module, resourceInfo);
                IntPtr pointer = LockResource(loaded);
                if (length == 0
                    || loaded == IntPtr.Zero
                    || pointer == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                byte[] bytes = new byte[checked((int)length)];
                Marshal.Copy(pointer, bytes, 0, bytes.Length);
                return bytes;
            }
            finally
            {
                FreeLibrary(module);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(
            string fileName,
            IntPtr file,
            uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr module);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumResourceNames(
            IntPtr module,
            IntPtr type,
            EnumResourceNameCallback callback,
            IntPtr parameter);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindResource(
            IntPtr module,
            IntPtr name,
            IntPtr type);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadResource(
            IntPtr module,
            IntPtr resourceInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LockResource(IntPtr resource);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SizeofResource(
            IntPtr module,
            IntPtr resourceInfo);
    }
}
'@
}

function Get-IconEncoding {
    param([byte[]]$Payload)

    $pngSignature = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
    if ($Payload.Length -ge $pngSignature.Length) {
        $isPng = $true
        for ($index = 0; $index -lt $pngSignature.Length; $index++) {
            if ($Payload[$index] -ne $pngSignature[$index]) {
                $isPng = $false
                break
            }
        }
        if ($isPng) {
            return 'PNG'
        }
    }
    return 'DIB'
}

function Get-IconFileFrames {
    param([string]$Path)

    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 6 -or
        [BitConverter]::ToUInt16($bytes, 0) -ne 0 -or
        [BitConverter]::ToUInt16($bytes, 2) -ne 1) {
        throw "The source ICO directory is invalid: $Path"
    }

    $count = [BitConverter]::ToUInt16($bytes, 4)
    if ($count -le 0 -or $bytes.Length -lt (6 + (16 * $count))) {
        throw "The source ICO directory is truncated: $Path"
    }

    $ranges = @()
    $frames = @()
    for ($index = 0; $index -lt $count; $index++) {
        $entry = 6 + (16 * $index)
        $width = if ($bytes[$entry] -eq 0) { 256 } else { $bytes[$entry] }
        $height = if ($bytes[$entry + 1] -eq 0) { 256 } else { $bytes[$entry + 1] }
        if ($bytes[$entry + 2] -ne 0 -or $bytes[$entry + 3] -ne 0) {
            throw "ICO frame $index has invalid color-count or reserved fields."
        }
        $planes = [BitConverter]::ToUInt16($bytes, $entry + 4)
        $bitDepth = [BitConverter]::ToUInt16($bytes, $entry + 6)
        $length = [BitConverter]::ToUInt32($bytes, $entry + 8)
        $offset = [BitConverter]::ToUInt32($bytes, $entry + 12)
        $end = [uint64]$offset + [uint64]$length
        if ($length -eq 0 -or
            $offset -lt (6 + (16 * $count)) -or
            $end -gt $bytes.Length) {
            throw "ICO frame $index has an invalid offset or length."
        }

        $payload = [byte[]]::new($length)
        [Array]::Copy($bytes, $offset, $payload, 0, $length)
        $ranges += [PSCustomObject]@{ Start = [uint64]$offset; End = $end }
        $frames += [PSCustomObject]@{
            Width = [int]$width
            Height = [int]$height
            Planes = [uint16]$planes
            BitDepth = [uint16]$bitDepth
            Payload = $payload
            Encoding = Get-IconEncoding -Payload $payload
        }
    }

    $orderedRanges = $ranges | Sort-Object Start
    for ($index = 1; $index -lt $orderedRanges.Count; $index++) {
        if ($orderedRanges[$index].Start -lt $orderedRanges[$index - 1].End) {
            throw 'ICO frame payloads overlap.'
        }
    }
    return $frames
}

function Get-ExecutableIconFrames {
    param([string]$Path)

    $groupIds = [SvgLiveEditor.NativeIconResourceReader]::EnumerateIds($Path, 14)
    if ($groupIds.Count -ne 1) {
        throw "The executable must contain exactly one RT_GROUP_ICON resource."
    }

    $group = [SvgLiveEditor.NativeIconResourceReader]::Read(
        $Path,
        14,
        $groupIds[0])
    if ($group.Length -lt 6 -or
        [BitConverter]::ToUInt16($group, 0) -ne 0 -or
        [BitConverter]::ToUInt16($group, 2) -ne 1) {
        throw 'The executable RT_GROUP_ICON directory is invalid.'
    }

    $count = [BitConverter]::ToUInt16($group, 4)
    if ($count -le 0 -or $group.Length -ne (6 + (14 * $count))) {
        throw 'The executable RT_GROUP_ICON directory is truncated.'
    }

    $frames = @()
    for ($index = 0; $index -lt $count; $index++) {
        $entry = 6 + (14 * $index)
        $width = if ($group[$entry] -eq 0) { 256 } else { $group[$entry] }
        $height = if ($group[$entry + 1] -eq 0) { 256 } else { $group[$entry + 1] }
        if ($group[$entry + 2] -ne 0 -or $group[$entry + 3] -ne 0) {
            throw "RT_GROUP_ICON frame $index has invalid color-count or reserved fields."
        }
        $declaredLength = [BitConverter]::ToUInt32($group, $entry + 8)
        $resourceId = [BitConverter]::ToUInt16($group, $entry + 12)
        $payload = [SvgLiveEditor.NativeIconResourceReader]::Read(
            $Path,
            3,
            $resourceId)
        if ($payload.Length -ne $declaredLength) {
            throw "RT_ICON $resourceId does not match its group length."
        }

        $frames += [PSCustomObject]@{
            Width = [int]$width
            Height = [int]$height
            Planes = [BitConverter]::ToUInt16($group, $entry + 4)
            BitDepth = [BitConverter]::ToUInt16($group, $entry + 6)
            ResourceId = [int]$resourceId
            Payload = $payload
            Encoding = Get-IconEncoding -Payload $payload
        }
    }

    $availableIconIds = @(
        [SvgLiveEditor.NativeIconResourceReader]::EnumerateIds($Path, 3))
    $referencedIconIds = @(
        $frames | ForEach-Object { $_.ResourceId } | Sort-Object)
    if ($availableIconIds.Count -ne $referencedIconIds.Count) {
        throw 'The executable contains unreferenced or duplicate RT_ICON resources.'
    }
    for ($index = 0; $index -lt $availableIconIds.Count; $index++) {
        if ($availableIconIds[$index] -ne $referencedIconIds[$index]) {
            throw 'The executable contains unreferenced or duplicate RT_ICON resources.'
        }
    }
    return $frames
}

function Assert-IconDibPayload {
    param(
        [Parameter(Mandatory)]
        [object]$Frame,

        [Parameter(Mandatory)]
        [string]$Description
    )

    $payload = $Frame.Payload
    $width = $Frame.Width
    $height = $Frame.Height
    if ($payload.Length -lt 40 -or
        [BitConverter]::ToUInt32($payload, 0) -ne 40 -or
        [BitConverter]::ToInt32($payload, 4) -ne $width -or
        [BitConverter]::ToInt32($payload, 8) -ne ($height * 2) -or
        [BitConverter]::ToUInt16($payload, 12) -ne 1 -or
        [BitConverter]::ToUInt16($payload, 14) -ne 32 -or
        [BitConverter]::ToUInt32($payload, 16) -ne 0 -or
        [BitConverter]::ToInt32($payload, 24) -ne 0 -or
        [BitConverter]::ToInt32($payload, 28) -ne 0 -or
        [BitConverter]::ToUInt32($payload, 32) -ne 0 -or
        [BitConverter]::ToUInt32($payload, 36) -ne 0) {
        throw "$Description has an invalid 32-bit BITMAPINFOHEADER."
    }

    $xorStride = $width * 4
    $xorLength = $xorStride * $height
    $maskStride = [int]([Math]::Ceiling($width / 32.0) * 4)
    $expectedLength = 40 + $xorLength + ($maskStride * $height)
    if (($xorStride % 4) -ne 0 -or
        ($maskStride % 4) -ne 0 -or
        [BitConverter]::ToUInt32($payload, 20) -ne $xorLength -or
        $payload.Length -ne $expectedLength) {
        throw "$Description has invalid XOR or AND-mask lengths."
    }

    $hasTransparentPixel = $false
    $hasOpaquePixel = $false
    $maskOffset = 40 + $xorLength
    for ($row = 0; $row -lt $height; $row++) {
        for ($x = 0; $x -lt $width; $x++) {
            $alpha = $payload[40 + ($row * $xorStride) + ($x * 4) + 3]
            $hasTransparentPixel = $hasTransparentPixel -or $alpha -eq 0
            $hasOpaquePixel = $hasOpaquePixel -or $alpha -eq 255
            $maskByte = $payload[
                $maskOffset + ($row * $maskStride) +
                [int][Math]::Floor($x / 8.0)]
            $maskBit = ($maskByte -band (0x80 -shr ($x % 8))) -ne 0
            if ($maskBit -ne ($alpha -eq 0)) {
                throw "$Description has an AND-mask bit that disagrees with its alpha channel."
            }
        }

        for ($x = $width; $x -lt ($maskStride * 8); $x++) {
            $maskByte = $payload[
                $maskOffset + ($row * $maskStride) +
                [int][Math]::Floor($x / 8.0)]
            if (($maskByte -band (0x80 -shr ($x % 8))) -ne 0) {
                throw "$Description has non-zero AND-mask padding bits."
            }
        }
    }
    if (-not $hasTransparentPixel -or -not $hasOpaquePixel) {
        throw "$Description must preserve both transparent and opaque pixels."
    }
}

function Get-ByteArrayHash {
    param([byte[]]$Bytes)

    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return ($algorithm.ComputeHash($Bytes) |
          ForEach-Object { $_.ToString('X2') }) -join ''
    }
    finally {
        $algorithm.Dispose()
    }
}

function Test-NearColor {
    param(
        [Drawing.Color]$Actual,
        [Drawing.Color]$Expected
    )

    return $Actual.A -ge 224 -and
      [Math]::Abs($Actual.R - $Expected.R) -le 12 -and
      [Math]::Abs($Actual.G - $Expected.G) -le 12 -and
      [Math]::Abs($Actual.B - $Expected.B) -le 12
}

function Test-BrandPalette {
    param([Drawing.Icon]$Icon)

    $bitmap = $Icon.ToBitmap()
    try {
        $navy = 0
        $white = 0
        $blue = 0
        $purple = 0
        for ($y = 0; $y -lt $bitmap.Height; $y++) {
            for ($x = 0; $x -lt $bitmap.Width; $x++) {
                $color = $bitmap.GetPixel($x, $y)
                if (Test-NearColor `
                        -Actual $color `
                        -Expected ([Drawing.Color]::FromArgb(255, 11, 31, 58))) {
                    $navy++
                }
                if (Test-NearColor `
                        -Actual $color `
                        -Expected ([Drawing.Color]::White)) {
                    $white++
                }
                if (Test-NearColor `
                        -Actual $color `
                        -Expected ([Drawing.Color]::FromArgb(255, 96, 165, 250))) {
                    $blue++
                }
                if (Test-NearColor `
                        -Actual $color `
                        -Expected ([Drawing.Color]::FromArgb(255, 139, 92, 246))) {
                    $purple++
                }
            }
        }

        $area = $bitmap.Width * $bitmap.Height
        return $navy -ge [Math]::Max(4, [int]($area / 6)) -and
          $white -ge [Math]::Max(2, [int]($area / 50)) -and
          ($blue + $purple) -gt 0 -and
          ($area -lt 1024 -or ($blue -gt 0 -and $purple -gt 0))
    }
    finally {
        $bitmap.Dispose()
    }
}

$resolvedExecutablePath = (Resolve-Path -LiteralPath $ExecutablePath).Path
$resolvedIconPath = (Resolve-Path -LiteralPath $IconPath).Path
$sourceFrames = @(Get-IconFileFrames -Path $resolvedIconPath)
$executableFrames = @(Get-ExecutableIconFrames -Path $resolvedExecutablePath)

$expectedSizes = @(16, 24, 32, 48, 64, 128, 256)
if ($sourceFrames.Count -ne $expectedSizes.Count -or
    $executableFrames.Count -ne $expectedSizes.Count) {
    throw 'The source ICO and executable must contain all seven required frames.'
}

for ($index = 0; $index -lt $expectedSizes.Count; $index++) {
    $source = $sourceFrames[$index]
    $embedded = $executableFrames[$index]
    $expectedSize = $expectedSizes[$index]
    $expectedEncoding = if ($expectedSize -eq 256) { 'PNG' } else { 'DIB' }
    if ($source.Width -ne $expectedSize -or
        $source.Height -ne $expectedSize -or
        $source.Planes -ne 1 -or
        $source.BitDepth -ne 32 -or
        $source.Encoding -ne $expectedEncoding) {
        throw "The source ICO frame at index $index is not the required $expectedSize px $expectedEncoding frame."
    }
    if ($source.Encoding -eq 'DIB') {
        Assert-IconDibPayload `
          -Frame $source `
          -Description "Source ICO frame $index ($expectedSize px)"
    }
    if ($embedded.Width -ne $source.Width -or
        $embedded.Height -ne $source.Height -or
        $embedded.Planes -ne $source.Planes -or
        $embedded.BitDepth -ne $source.BitDepth -or
        $embedded.Encoding -ne $source.Encoding -or
        (Get-ByteArrayHash $embedded.Payload) -ne
          (Get-ByteArrayHash $source.Payload)) {
        throw "The executable RT_ICON frame at index $index does not match the intended ICO payload."
    }
}

$associated = [Drawing.Icon]::ExtractAssociatedIcon($resolvedExecutablePath)
if ($null -eq $associated) {
    throw 'Windows could not extract an associated icon from the executable.'
}
try {
    if (-not (Test-BrandPalette -Icon $associated)) {
        throw 'ExtractAssociatedIcon did not expose the distinctive SvgLiveEditor artwork and palette.'
    }
    if (Test-BrandPalette -Icon ([Drawing.SystemIcons]::Application)) {
        throw 'The custom-palette check also matched the generic Windows icon.'
    }

    $bitmap = $associated.ToBitmap()
    try {
        Write-Host (
            "Validated application icon: {0} RT_ICON frames; associated icon {1}x{2}" -f
              $sourceFrames.Count,
              $bitmap.Width,
              $bitmap.Height)
    }
    finally {
        $bitmap.Dispose()
    }
}
finally {
    $associated.Dispose()
}
