using System.IO;
using System.Text;

namespace SvgLiveEditor.Services;

public sealed class Utf8FileService
{
    public const long MaximumFileBytes = 10_000_000;
    public const int MaximumFileMegabytes = 10;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public string ReadAllText(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 16_384,
            FileOptions.SequentialScan);
        if (stream.Length > MaximumFileBytes)
        {
            throw new FileSizeLimitExceededException(
                path,
                MaximumFileBytes);
        }

        using MemoryStream buffer = new(
            capacity: checked((int)Math.Min(
                stream.Length,
                MaximumFileBytes)));
        byte[] chunk = new byte[16_384];
        int totalBytes = 0;
        while (true)
        {
            int bytesRead = stream.Read(chunk, 0, chunk.Length);
            if (bytesRead == 0)
            {
                break;
            }

            totalBytes = checked(totalBytes + bytesRead);
            if (totalBytes > MaximumFileBytes)
            {
                throw new FileSizeLimitExceededException(
                    path,
                    MaximumFileBytes);
            }

            buffer.Write(chunk, 0, bytesRead);
        }

        byte[] bytes = buffer.ToArray();
        ReadOnlySpan<byte> content = bytes;

        if (content.StartsWith(Encoding.UTF8.Preamble))
        {
            content = content[Encoding.UTF8.Preamble.Length..];
        }

        return StrictUtf8.GetString(content);
    }

    public void WriteAllText(string path, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);
        File.WriteAllText(path, content, StrictUtf8);
    }
}

public sealed class FileSizeLimitExceededException : IOException
{
    public FileSizeLimitExceededException(
        string path,
        long maximumBytes)
        : base(
            $"The file '{Path.GetFileName(path)}' exceeds the {maximumBytes:N0}-byte limit.")
    {
        MaximumBytes = maximumBytes;
    }

    public long MaximumBytes { get; }
}
