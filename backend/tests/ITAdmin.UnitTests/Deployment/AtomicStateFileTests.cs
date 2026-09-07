using ITAdmin.HostAgent;

namespace ITAdmin.UnitTests.Deployment;

public sealed class AtomicStateFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "itadmin-atomic-" + Guid.NewGuid().ToString("N"));

    public AtomicStateFileTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Read_ReturnsNull_WhenTheFileDoesNotExist() =>
        Assert.Null(AtomicStateFile.Read(Path.Combine(_dir, "missing.json")));

    [Fact]
    public void Write_ThenRead_RoundTripsAndOverwrites()
    {
        var path = Path.Combine(_dir, "state.json");

        AtomicStateFile.Write(path, "{\"phase\":\"Pulling\"}");
        Assert.Equal("{\"phase\":\"Pulling\"}", AtomicStateFile.Read(path));

        AtomicStateFile.Write(path, "{\"phase\":\"Completed\"}");
        Assert.Equal("{\"phase\":\"Completed\"}", AtomicStateFile.Read(path));

        Assert.Empty(Directory.GetFiles(_dir, "state.json.*.tmp"));
    }

    [Fact]
    public void Write_SucceedsAndClearsStaleTemporaries_WhenAReaderHoldsTheDestinationOpen()
    {
        var path = Path.Combine(_dir, "held.json");
        AtomicStateFile.Write(path, "old");

        // A leftover .tmp from a previously failed write.
        File.WriteAllText(path + "." + Guid.NewGuid().ToString("N") + ".tmp", "junk");

        // A concurrent reader that opened the file share-read only (as File.ReadAllText would).
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            AtomicStateFile.Write(path, "new");
        }

        Assert.Equal("new", AtomicStateFile.Read(path));
        Assert.Empty(Directory.GetFiles(_dir, "held.json.*.tmp"));
    }
}
