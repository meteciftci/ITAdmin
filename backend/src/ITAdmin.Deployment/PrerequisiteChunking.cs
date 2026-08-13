using System.Security.Cryptography;

namespace ITAdmin.Deployment;

/// <summary>
/// Splits a large third-party installer into bounded, individually-verified pieces, and puts it
/// back together.
///
/// <para>
/// <b>Why this exists.</b> The ASP.NET Core Hosting Bundle is a Microsoft redistributable well over
/// 100 MB. Git hosts reject blobs at around that size outright and warn well below it, so a
/// single-object representation would make the repository-driven lifecycle fail on exactly the file
/// that most needs to reach the server without a human carrying it. Rather than abandoning the
/// lifecycle and falling back to "download it yourself and type its hash", the file is stored as an
/// ordered set of chunks, each comfortably inside every limit.
/// </para>
///
/// <para>
/// <b>Why chunk digests are not enough.</b> Each chunk carries its own SHA-256, which proves the
/// pieces arrived intact. It does not prove they were reassembled into the file the release pinned:
/// a wrong order, a missing chunk, or a truncated write would still produce individually valid
/// pieces. So the reassembled file is hashed as a whole and compared to the manifest's
/// <c>sha256</c> before anything executes it. Chunk digests exist to localise a failure; the
/// full-file digest is the one that authorises execution.
/// </para>
/// </summary>
public static class PrerequisiteChunking
{
    /// <summary>
    /// 32 MiB. Comfortably below the ~50 MB point at which Git hosts start warning and far below the
    /// ~100 MB hard rejection, while keeping the chunk count for a ~150 MB installer in single
    /// figures.
    /// </summary>
    public const int DefaultChunkBytes = 32 * 1024 * 1024;

    /// <summary>Upper bound accepted when reading a manifest, so a hostile value cannot force a huge allocation.</summary>
    public const int MaximumChunkBytes = 64 * 1024 * 1024;

    public const string ChunkExtensionPrefix = ".part";

    /// <summary>Chunk file name for an index: <c>&lt;file&gt;.part0000</c>.</summary>
    public static string ChunkFileName(string fileName, int index) =>
        $"{fileName}{ChunkExtensionPrefix}{index:D4}";

    /// <summary>
    /// A plain file name: no directory separators, no drive letter, no traversal. This value ends up
    /// as a path on the target, so it is validated before it is ever combined with a directory.
    /// </summary>
    public static bool IsSafeFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && fileName.Length <= 200
        && !fileName.Contains('/', StringComparison.Ordinal)
        && !fileName.Contains('\\', StringComparison.Ordinal)
        && !fileName.Contains(':', StringComparison.Ordinal)
        && fileName is not "." and not ".."
        && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    /// <summary>
    /// Splits <paramref name="sourceFilePath"/> into <paramref name="destinationDirectory"/> and
    /// returns the metadata a distribution manifest needs to reassemble and verify it.
    /// </summary>
    public static PrerequisiteChunkingResult Split(
        string sourceFilePath,
        string destinationDirectory,
        int chunkBytes = DefaultChunkBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        if (chunkBytes is <= 0 or > MaximumChunkBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chunkBytes),
                chunkBytes,
                $"Chunk size must be between 1 and {MaximumChunkBytes} bytes.");
        }

        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException($"Prerequisite file not found: {sourceFilePath}", sourceFilePath);
        }

        var fileName = Path.GetFileName(sourceFilePath);
        if (!IsSafeFileName(fileName))
        {
            throw new ArgumentException($"'{fileName}' is not a usable prerequisite file name.", nameof(sourceFilePath));
        }

        Directory.CreateDirectory(destinationDirectory);

        var chunkDigests = new List<string>();
        var buffer = new byte[chunkBytes];
        long totalBytes = 0;

        using (var source = File.OpenRead(sourceFilePath))
        {
            var index = 0;
            while (true)
            {
                var read = source.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
                if (read == 0)
                {
                    break;
                }

                var chunkPath = Path.Combine(destinationDirectory, ChunkFileName(fileName, index));
                using (var chunk = File.Create(chunkPath))
                {
                    chunk.Write(buffer, 0, read);
                }

                chunkDigests.Add(Convert.ToHexStringLower(SHA256.HashData(buffer.AsSpan(0, read))));
                totalBytes += read;
                index++;

                if (read < buffer.Length)
                {
                    break;
                }
            }
        }

        if (chunkDigests.Count == 0)
        {
            throw new InvalidOperationException($"Prerequisite file is empty: {sourceFilePath}");
        }

        return new PrerequisiteChunkingResult(
            FileName: fileName,
            Sha256: ComputeFileDigest(sourceFilePath),
            SizeBytes: totalBytes,
            ChunkDigests: chunkDigests);
    }

    /// <summary>
    /// Reassembles a prerequisite from its chunks and verifies it end to end.
    ///
    /// <para>
    /// Every failure mode is reported rather than thrown, and the destination is written to a
    /// temporary file that is only moved into place once the full-file digest matches. A partially
    /// written installer must never be left somewhere that something else might decide to run.
    /// </para>
    /// </summary>
    public static PrerequisiteReassemblyResult Reassemble(
        PrerequisitePayload payload,
        string chunkDirectory,
        string destinationFilePath)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(chunkDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFilePath);

        var structural = payload.Validate();
        if (structural.Count > 0)
        {
            return PrerequisiteReassemblyResult.Failed(structural);
        }

        if (!Directory.Exists(chunkDirectory))
        {
            return PrerequisiteReassemblyResult.Failed(
                [$"Prerequisite chunk directory is missing: {chunkDirectory}"]);
        }

        var problems = new List<string>();
        var temporaryPath = destinationFilePath + ".reassembling";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath)!);

            using (var destination = File.Create(temporaryPath))
            {
                for (var index = 0; index < payload.ChunkDigests.Count; index++)
                {
                    var chunkPath = Path.Combine(chunkDirectory, payload.ChunkFileName(index));
                    if (!File.Exists(chunkPath))
                    {
                        problems.Add($"chunk {index} is missing ({payload.ChunkFileName(index)}).");
                        continue;
                    }

                    var chunkBytes = File.ReadAllBytes(chunkPath);
                    if (chunkBytes.Length > MaximumChunkBytes)
                    {
                        problems.Add($"chunk {index} is larger than the permitted chunk size.");
                        continue;
                    }

                    var actual = Convert.ToHexStringLower(SHA256.HashData(chunkBytes));
                    if (!string.Equals(actual, payload.ChunkDigests[index], StringComparison.Ordinal))
                    {
                        problems.Add($"chunk {index} digest does not match the manifest.");
                        continue;
                    }

                    destination.Write(chunkBytes, 0, chunkBytes.Length);
                }
            }

            if (problems.Count > 0)
            {
                return PrerequisiteReassemblyResult.Failed(problems);
            }

            var reassembledSize = new FileInfo(temporaryPath).Length;
            if (reassembledSize != payload.SizeBytes)
            {
                return PrerequisiteReassemblyResult.Failed(
                    [$"Reassembled size {reassembledSize} does not match the manifest size {payload.SizeBytes}."]);
            }

            // The gate that authorises execution. Chunk digests localise a fault; this proves the
            // bytes about to be run are the ones the release pinned.
            var digest = ComputeFileDigest(temporaryPath);
            if (!string.Equals(digest, payload.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return PrerequisiteReassemblyResult.Failed(
                [
                    "Reassembled file digest does not match the manifest. The prerequisite will not be executed.",
                ]);
            }

            if (File.Exists(destinationFilePath))
            {
                File.Delete(destinationFilePath);
            }

            File.Move(temporaryPath, destinationFilePath);

            return new PrerequisiteReassemblyResult(true, destinationFilePath, digest, []);
        }
        catch (IOException exception)
        {
            return PrerequisiteReassemblyResult.Failed([$"Reassembly failed: {exception.Message}"]);
        }
        catch (UnauthorizedAccessException exception)
        {
            return PrerequisiteReassemblyResult.Failed([$"Reassembly failed: {exception.Message}"]);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    public static string ComputeFileDigest(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}

public sealed record PrerequisiteChunkingResult(
    string FileName,
    string Sha256,
    long SizeBytes,
    IReadOnlyList<string> ChunkDigests);

public sealed record PrerequisiteReassemblyResult(
    bool Succeeded,
    string? FilePath,
    string? Sha256,
    IReadOnlyList<string> Problems)
{
    public static PrerequisiteReassemblyResult Failed(IReadOnlyList<string> problems) =>
        new(false, null, null, problems);
}
