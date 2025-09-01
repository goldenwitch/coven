using System.Text.Json;

namespace Coven.Durables;

public class SimpleFileStorage<T> : IDurableList<T>
{
    private readonly string _storageLocation;

    public SimpleFileStorage(string StorageLocation)
    {
        _storageLocation = StorageLocation;
    }

    public async Task Append(T item)
    {
        List<T> current;
        if (!File.Exists(_storageLocation))
        {
            current = new List<T>();
        }
        else
        {
            current = await Load().ConfigureAwait(false);
        }
        current.Add(item);
        await Save(current).ConfigureAwait(false);
    }

    public async Task<List<T>> Load()
    {
        if (!File.Exists(_storageLocation))
        {
            throw new InvalidOperationException($"File at {_storageLocation} does not exist. Save or Append before loading.");
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await using var stream = new FileStream(
                    _storageLocation,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true);
                var result = await JsonSerializer.DeserializeAsync<List<T>>(stream, options).ConfigureAwait(false);
                return result ?? new List<T>();
            }
            catch (IOException)
            {
                await Task.Delay(10).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                await Task.Delay(10).ConfigureAwait(false);
            }
        }
        // If still failing after retries, treat as not ready
        throw new InvalidOperationException($"Unable to read storage at {_storageLocation}.");
    }

    public async Task Save(List<T> input)
    {
        var directory = Path.GetDirectoryName(_storageLocation);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write to a temp file with an exclusive handle, then atomically replace.
        var tempPath = _storageLocation + ".tmp";
        var options = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        await using (var stream = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, input, options).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        // Replace destination atomically when possible; otherwise move into place.
        if (File.Exists(_storageLocation))
        {
            // File.Replace is atomic on supported platforms (Windows); on others it falls back appropriately.
            File.Replace(tempPath, _storageLocation, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, _storageLocation);
        }
    }
}
