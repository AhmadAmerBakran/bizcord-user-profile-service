using System.Text.Json;

namespace PremiumSagaDemo;

// One writer per service instance. Atomic file replacement protects the saved snapshots.
public sealed class JsonStateStore<T> where T : class
{
    private readonly string _path;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public JsonStateStore(string path)
    {
        _path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
    }

    public async Task<T?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var all = await ReadAsync(cancellationToken);
            return all.GetValueOrDefault(id);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyCollection<T>> AllAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var all = await ReadAsync(cancellationToken);
            return all.Values.ToArray();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task SaveAsync(Guid id, T value, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var all = await ReadAsync(cancellationToken);
            all[id] = value;
            var tempPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(all), cancellationToken);
                File.Move(tempPath, _path, true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<Dictionary<Guid, T>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return new Dictionary<Guid, T>();
        }

        var json = await File.ReadAllTextAsync(_path, cancellationToken);
        return JsonSerializer.Deserialize<Dictionary<Guid, T>>(json)
            ?? throw new InvalidDataException("The saved saga state was empty.");
    }
}
