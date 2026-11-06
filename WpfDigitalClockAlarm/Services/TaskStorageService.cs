using System.IO;
using System.Text.Json;
using System.Threading;
using WpfDigitalClockAlarm.Models;

namespace WpfDigitalClockAlarm.Services;

public class TaskStorageService
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public TaskStorageService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WpfDigitalClockAlarm", "tasks.json");
    }

    public async Task<List<TaskItem>> LoadTasksAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (!File.Exists(_filePath)) return new();
            var tasks = JsonSerializer.Deserialize<List<TaskItem>>(
                await File.ReadAllTextAsync(_filePath), Options);
            if (tasks is null || tasks.Any(t => t is null))
                throw new InvalidDataException("Expected a JSON array of tasks.");
            return tasks;
        }
        finally { _gate.Release(); }
    }

    public async Task SaveTasksAsync(IEnumerable<TaskItem> tasks)
    {
        string json = JsonSerializer.Serialize(tasks.ToList(), Options);
        await _gate.WaitAsync();
        string temporary = _filePath + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);
            await File.WriteAllTextAsync(temporary, json);
            if (File.Exists(_filePath)) File.Replace(temporary, _filePath, _filePath + ".bak");
            else File.Move(temporary, _filePath);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            _gate.Release();
        }
    }
}
