using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace SvgLiveEditor.Tests;

internal enum IconFrameEncoding
{
    Dib,
    Png
}

internal sealed record IconFrame(
    int Width,
    int Height,
    byte DirectoryWidth,
    byte DirectoryHeight,
    ushort Planes,
    ushort BitDepth,
    byte[] Payload,
    IconFrameEncoding Encoding);

internal sealed record ExecutableIconFrame(
    int Width,
    int Height,
    ushort Planes,
    ushort BitDepth,
    int ResourceId,
    byte[] Payload,
    IconFrameEncoding Encoding);

internal static class WindowsIconTestSupport
{
    internal const int IconSmall = 0;
    internal const int IconBig = 1;
    internal const int IconSmall2 = 2;
    internal const int ClassBigIcon = -14;
    internal const int ClassSmallIcon = -34;

    private const int RtIcon = 3;
    private const int RtGroupIcon = 14;
    private const uint LoadLibraryAsDataFile = 0x00000002;
    private const uint LoadLibraryAsImageResource = 0x00000020;
    private const uint WmGetIcon = 0x007F;
    private const uint SmtoAbortIfHung = 0x0002;
    private static readonly byte[] PngSignature =
        [137, 80, 78, 71, 13, 10, 26, 10];

    internal static string GetSourceIconPath() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "..",
        "src",
        "SvgLiveEditor",
        "Assets",
        "SvgLiveEditor.ico"));

    internal static IReadOnlyList<IconFrame> ReadIconFile(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, writable: false);
        using BinaryReader reader = new(stream);

        if (reader.ReadUInt16() != 0 || reader.ReadUInt16() != 1)
        {
            throw new InvalidDataException("The file is not a Windows icon.");
        }

        int count = reader.ReadUInt16();
        if (count <= 0 || bytes.Length < 6 + (16 * count))
        {
            throw new InvalidDataException("The icon directory is truncated.");
        }

        List<(long Start, long End)> ranges = [];
        List<IconFrame> frames = [];
        for (int index = 0; index < count; index++)
        {
            stream.Position = 6 + (16 * index);
            byte directoryWidth = reader.ReadByte();
            byte directoryHeight = reader.ReadByte();
            int width = ReadDimension(directoryWidth);
            int height = ReadDimension(directoryHeight);
            byte colorCount = reader.ReadByte();
            byte reserved = reader.ReadByte();
            if (colorCount != 0 || reserved != 0)
            {
                throw new InvalidDataException(
                    $"Icon frame {index} has invalid directory flags.");
            }
            ushort planes = reader.ReadUInt16();
            ushort bitDepth = reader.ReadUInt16();
            uint length = reader.ReadUInt32();
            uint offset = reader.ReadUInt32();

            long end = checked((long)offset + length);
            if (offset < 6 + (16 * count) || length == 0 || end > bytes.Length)
            {
                throw new InvalidDataException(
                    $"Icon frame {index} has an invalid offset or length.");
            }
            ranges.Add((offset, end));

            byte[] payload = bytes.AsSpan((int)offset, (int)length).ToArray();
            frames.Add(new IconFrame(
                width,
                height,
                directoryWidth,
                directoryHeight,
                planes,
                bitDepth,
                payload,
                GetEncoding(payload)));
        }

        (long Start, long End)[] orderedRanges = ranges
            .OrderBy(range => range.Start)
            .ToArray();
        for (int index = 1; index < orderedRanges.Length; index++)
        {
            if (orderedRanges[index].Start < orderedRanges[index - 1].End)
            {
                throw new InvalidDataException("Icon frame payloads overlap.");
            }
        }

        return frames;
    }

    internal static IReadOnlyList<ExecutableIconFrame> ReadExecutableIconFrames(
        string executablePath)
    {
        nint module = LoadLibraryEx(
            executablePath,
            nint.Zero,
            LoadLibraryAsDataFile | LoadLibraryAsImageResource);
        if (module == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            int[] groupIds = EnumerateResourceIds(module, RtGroupIcon);
            if (groupIds.Length != 1)
            {
                throw new InvalidDataException(
                    $"Expected one RT_GROUP_ICON resource, found {groupIds.Length}.");
            }

            byte[] group = ReadResource(module, RtGroupIcon, groupIds[0]);
            if (group.Length < 6
                || BitConverter.ToUInt16(group, 0) != 0
                || BitConverter.ToUInt16(group, 2) != 1)
            {
                throw new InvalidDataException("The executable icon group is invalid.");
            }

            int count = BitConverter.ToUInt16(group, 4);
            if (count <= 0 || group.Length != 6 + (14 * count))
            {
                throw new InvalidDataException(
                    "The executable icon group directory is truncated.");
            }

            List<ExecutableIconFrame> frames = [];
            for (int index = 0; index < count; index++)
            {
                int offset = 6 + (14 * index);
                int width = ReadDimension(group[offset]);
                int height = ReadDimension(group[offset + 1]);
                if (group[offset + 2] != 0 || group[offset + 3] != 0)
                {
                    throw new InvalidDataException(
                        $"Executable icon frame {index} has invalid group flags.");
                }
                ushort planes = BitConverter.ToUInt16(group, offset + 4);
                ushort bitDepth = BitConverter.ToUInt16(group, offset + 6);
                uint declaredLength = BitConverter.ToUInt32(group, offset + 8);
                int resourceId = BitConverter.ToUInt16(group, offset + 12);
                byte[] payload = ReadResource(module, RtIcon, resourceId);
                if (payload.Length != declaredLength)
                {
                    throw new InvalidDataException(
                        $"RT_ICON {resourceId} does not match its group length.");
                }

                frames.Add(new ExecutableIconFrame(
                    width,
                    height,
                    planes,
                    bitDepth,
                    resourceId,
                    payload,
                    GetEncoding(payload)));
            }

            int[] availableIconIds = EnumerateResourceIds(module, RtIcon);
            int[] referencedIconIds = frames
                .Select(frame => frame.ResourceId)
                .OrderBy(id => id)
                .ToArray();
            if (!availableIconIds.SequenceEqual(referencedIconIds))
            {
                throw new InvalidDataException(
                    "The executable contains unreferenced or duplicate RT_ICON resources.");
            }

            return frames;
        }
        finally
        {
            _ = FreeLibrary(module);
        }
    }

    internal static Bitmap DecodeFrame(IconFrame frame)
    {
        if (frame.Encoding == IconFrameEncoding.Png)
        {
            using MemoryStream pngStream = new(frame.Payload, writable: false);
            using Bitmap pngDecoded = new(pngStream);
            return new Bitmap(pngDecoded);
        }

        using MemoryStream iconStream = new();
        using (BinaryWriter writer = new(iconStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)1);
            writer.Write(WriteDimension(frame.Width));
            writer.Write(WriteDimension(frame.Height));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write(frame.Planes);
            writer.Write(frame.BitDepth);
            writer.Write((uint)frame.Payload.Length);
            writer.Write((uint)22);
            writer.Write(frame.Payload);
        }
        iconStream.Position = 0;
        using Icon icon = new(iconStream);
        using Bitmap iconDecoded = icon.ToBitmap();
        return new Bitmap(iconDecoded);
    }

    internal static nint GetWindowIcon(nint hwnd, int kind)
    {
        nint result;
        nint sent = SendMessageTimeout(
            hwnd,
            WmGetIcon,
            (nint)kind,
            nint.Zero,
            SmtoAbortIfHung,
            2000,
            out result);
        if (sent == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        return result;
    }

    internal static nint GetClassIcon(nint hwnd, int index) =>
        GetClassLongPtr(hwnd, index);

    internal static bool ContainsSvgLiveEditorBrandPalette(Icon icon)
    {
        using Bitmap bitmap = icon.ToBitmap();
        int navy = 0;
        int white = 0;
        int blue = 0;
        int purple = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color color = bitmap.GetPixel(x, y);
                if (IsNear(color, Color.FromArgb(255, 11, 31, 58)))
                {
                    navy++;
                }
                if (IsNear(color, Color.White))
                {
                    white++;
                }
                if (IsNear(color, Color.FromArgb(255, 96, 165, 250)))
                {
                    blue++;
                }
                if (IsNear(color, Color.FromArgb(255, 139, 92, 246)))
                {
                    purple++;
                }
            }
        }

        int area = checked(bitmap.Width * bitmap.Height);
        return navy >= Math.Max(4, area / 6)
            && white >= Math.Max(2, area / 50)
            && blue + purple > 0
            && (area < 1024 || (blue > 0 && purple > 0));
    }

    internal static bool HandleContainsSvgLiveEditorBrandPalette(nint iconHandle)
    {
        if (iconHandle == nint.Zero)
        {
            return false;
        }

        using Icon borrowed = Icon.FromHandle(iconHandle);
        using Icon copy = (Icon)borrowed.Clone();
        return ContainsSvgLiveEditorBrandPalette(copy);
    }

    private static int[] EnumerateResourceIds(nint module, int type)
    {
        List<int> ids = [];
        bool hasNamedResource = false;
        EnumResourceNameCallback callback = (_, _, name, _) =>
        {
            ulong rawName = unchecked((ulong)name.ToInt64());
            if ((rawName >> 16) != 0)
            {
                hasNamedResource = true;
                return false;
            }
            ids.Add((ushort)rawName);
            return true;
        };

        bool completed = EnumResourceNames(
            module,
            (nint)type,
            callback,
            nint.Zero);
        GC.KeepAlive(callback);
        if (hasNamedResource)
        {
            throw new InvalidDataException(
                "Named icon resources are not supported by this audit.");
        }
        if (!completed)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        return [.. ids.OrderBy(id => id)];
    }

    private static byte[] ReadResource(nint module, int type, int id)
    {
        nint resourceInfo = FindResource(module, (nint)id, (nint)type);
        if (resourceInfo == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        uint length = SizeofResource(module, resourceInfo);
        nint loadedResource = LoadResource(module, resourceInfo);
        nint pointer = LockResource(loadedResource);
        if (length == 0 || loadedResource == nint.Zero || pointer == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        byte[] bytes = new byte[checked((int)length)];
        Marshal.Copy(pointer, bytes, 0, checked((int)length));
        return bytes;
    }

    private static IconFrameEncoding GetEncoding(byte[] payload) =>
        payload.AsSpan().StartsWith(PngSignature)
            ? IconFrameEncoding.Png
            : IconFrameEncoding.Dib;

    private static int ReadDimension(byte value) => value == 0 ? 256 : value;

    private static byte WriteDimension(int value) =>
        value == 256 ? (byte)0 : checked((byte)value);

    private static bool IsNear(Color actual, Color expected)
    {
        const int tolerance = 12;
        return actual.A >= 224
            && Math.Abs(actual.R - expected.R) <= tolerance
            && Math.Abs(actual.G - expected.G) <= tolerance
            && Math.Abs(actual.B - expected.B) <= tolerance;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool EnumResourceNameCallback(
        nint module,
        nint type,
        nint name,
        nint parameter);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadLibraryEx(
        string fileName,
        nint file,
        uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(nint module);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumResourceNames(
        nint module,
        nint type,
        EnumResourceNameCallback callback,
        nint parameter);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindResource(nint module, nint name, nint type);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint LoadResource(nint module, nint resourceInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint LockResource(nint resource);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SizeofResource(nint module, nint resourceInfo);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint SendMessageTimeout(
        nint hwnd,
        uint message,
        nint wParam,
        nint lParam,
        uint flags,
        uint timeout,
        out nint result);

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW", SetLastError = true)]
    private static extern nint GetClassLongPtr(nint hwnd, int index);

}
