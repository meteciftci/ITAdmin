namespace ITAdmin.HostAgent;

/// <summary>
/// Reads and writes the small JSON state files under <c>%ProgramData%\ITAdmin\state</c>.
///
/// <para>
/// The obvious "write a .tmp then rename over the target" is not reliable on Windows on its own: a
/// concurrent reader (the UI polls update status while an update runs) can hold the destination
/// open without <c>FILE_SHARE_DELETE</c>, and antivirus can briefly lock a freshly written file,
/// so <see cref="File.Move(string, string, bool)"/> fails with a sharing violation and the state
/// silently stops advancing. <see cref="Write"/> retries, then falls back to copy-over and finally
/// an in-place write; <see cref="Read"/> opens share-all so it never blocks a writer.
/// </para>
/// </summary>
internal static class AtomicStateFile
{
    public static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        PruneStaleTemporaries(path);

        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, content);

            for (var attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    File.Move(temp, path, overwrite: true);
                    return;
                }
                catch (Exception exception)
                    when ((exception is IOException or UnauthorizedAccessException) && attempt < 5)
                {
                    Thread.Sleep(100);
                }
            }
        }
        catch (Exception)
        {
            // The rename kept failing. Copy over the destination instead; if even that fails,
            // overwrite it in place. A torn write here is still better than losing the record and
            // stranding the update pipeline.
            try
            {
                File.Copy(temp, path, overwrite: true);
            }
            catch (Exception)
            {
                File.WriteAllText(path, content);
            }
        }
        finally
        {
            try
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch (Exception)
            {
                // A leftover .tmp is pruned on the next write.
            }
        }
    }

    public static string? Read(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void PruneStaleTemporaries(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path)!;
            if (!Directory.Exists(directory))
            {
                return;
            }

            var prefix = Path.GetFileName(path) + ".";
            foreach (var stale in Directory.EnumerateFiles(directory, prefix + "*.tmp"))
            {
                try
                {
                    File.Delete(stale);
                }
                catch (Exception)
                {
                    // Someone else is mid-write; leave it.
                }
            }
        }
        catch (Exception)
        {
            // Best effort only.
        }
    }
}
