using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using QSolver.Services;

namespace QSolver
{
    /// <summary>
    /// Generic base class for JSON configuration persistence.
    /// Eliminates duplicate Load/Save logic across services.
    /// </summary>
    public abstract class JsonConfigService<T> where T : new()
    {
        protected abstract string ConfigFilePath { get; }
        protected abstract string LoadErrorKey { get; }
        protected abstract string SaveErrorKey { get; }

        protected T Data { get; set; } = new();

        protected void EnsureDirectoryExists()
        {
            string? directory = Path.GetDirectoryName(ConfigFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        protected void LoadData()
        {
            try
            {
                EnsureDirectoryExists();

                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var loadedData = JsonSerializer.Deserialize<T>(json);
                    if (loadedData != null)
                    {
                        Data = loadedData;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"{LocalizationService.Get(LoadErrorKey)}: {ex.Message}",
                    LocalizationService.Get("Common.Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Data = new T();
            }
        }

        protected void SaveData()
        {
            try
            {
                EnsureDirectoryExists();

                string json = JsonSerializer.Serialize(Data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"{LocalizationService.Get(SaveErrorKey)}: {ex.Message}",
                    LocalizationService.Get("Common.Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
