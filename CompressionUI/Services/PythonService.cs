using Microsoft.Extensions.Logging;
using Python.Runtime;
using System;
using System.Threading.Tasks;

namespace CompressionUI.Services;

public class PythonService
{
    private readonly ILogger<PythonService> _logger;
    private bool _isInitialized = false;

    public PythonService(ILogger<PythonService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> InitializePythonAsync()
    {
        try
        {
            if (_isInitialized) return true;

            await Task.Run(() =>
            {
                // Initialize Python.NET
                PythonEngine.Initialize();
                PythonEngine.BeginAllowThreads();
                _isInitialized = true;
            });

            _logger.LogInformation("Python runtime initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Python runtime");
            return false;
        }
    }

    public async Task<string> ExecutePythonCodeAsync(string code)
    {
        if (!_isInitialized) 
        {
            var initialized = await InitializePythonAsync();
            if (!initialized) return "Error: Python not initialized";
        }

        try
        {
            using (Py.GIL())
            {
                using var scope = Py.CreateScope();
                var result = scope.Exec(code);
                return "Code executed successfully";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute Python code");
            return $"Error: {ex.Message}";
        }
    }

    public void Dispose()
    {
        if (_isInitialized)
        {
            PythonEngine.Shutdown();
            _isInitialized = false;
        }
    }
}