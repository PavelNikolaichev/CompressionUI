using Microsoft.Extensions.Logging;
using Python.Runtime;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace CompressionUI.Services;

public class PythonService
{
    private readonly ILogger<PythonService> _logger;
    private bool _isInitialized = false;
    private bool _disposed = false;
    private readonly object _lockObject = new object();
    
    // Output and error buffers for capturing Python output
    private readonly StringBuilder _outputBuffer = new StringBuilder();
    private readonly StringBuilder _errorBuffer = new StringBuilder();
    
    public event EventHandler<string>? OutputReceived;
    public event EventHandler<string>? ErrorReceived;
    
    public bool IsInitialized => _isInitialized;
    public string PythonVersion { get; private set; } = "Unknown";
    public string PythonExecutable { get; private set; } = "Unknown";

    public PythonService(ILogger<PythonService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> InitializePythonAsync()
    {
        if (_isInitialized) return true;

        try
        {
            await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_isInitialized) return;

                    _logger.LogInformation("Initializing Python runtime...");

                    // Try to detect Python installation
                    DetectPythonEnvironment();

                    // Initialize Python.NET
                    PythonEngine.Initialize();
                    PythonEngine.BeginAllowThreads();
                    
                    _isInitialized = true;
                    _logger.LogInformation("Python runtime initialized successfully");

                    // Get Python version info
                    GetPythonInfo();
                }
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Python runtime");
            return false;
        }
    }

    private void DetectPythonEnvironment()
    {
        try
        {
            var pythonPath = Environment.GetEnvironmentVariable("PYTHONHOME") ?? 
                              Environment.GetEnvironmentVariable("PYTHONPATH") ?? 
                              "python";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            

            if (process.ExitCode == 0)
            {
                PythonExecutable = pythonPath;
                _logger.LogInformation($"Found Python: {pythonPath} - {output.Trim()}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect Python environment");
        }
    }
    
    private void GetPythonInfo()
    {
        try
        {
            using (Py.GIL())
            {
                using var scope = Py.CreateScope();
                
                // Get Python version
                var versionCode = @"
import sys
print(f'Python {sys.version}')
print(f'Executable: {sys.executable}')
";
                var result = scope.Exec(versionCode);
                // We'll capture this output in the enhanced execution method
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not get Python version info");
        }
    }


    public async Task<PythonExecutionResult> ExecutePythonCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new PythonExecutionResult
            {
                Success = true,
                Output = "",
                Error = ""
            };
        }

        if (!_isInitialized)
        {
            var initialized = await InitializePythonAsync();
            if (!initialized)
            {
                return new PythonExecutionResult
                {
                    Success = false,
                    Output = "",
                    Error = "Python runtime not initialized"
                };
            }
        }

        return await Task.Run(() =>
        {
            lock (_lockObject)
            {
                var result = new PythonExecutionResult();
                
                try
                {
                    using (Py.GIL())
                    {
                        using var scope = Py.CreateScope();
                        
                        // Capture stdout and stderr
                        var captureCode = @"
import sys
from io import StringIO

# Create string buffers for capturing output
_stdout_buffer = StringIO()
_stderr_buffer = StringIO()

# Save original streams
_original_stdout = sys.stdout
_original_stderr = sys.stderr

# Redirect streams
sys.stdout = _stdout_buffer
sys.stderr = _stderr_buffer
";
                        
                        scope.Exec(captureCode);
                        
                        // Execute user code
                        scope.Exec(code);
                        
                        // Get captured output
                        var getOutputCode = @"
# Get the captured output
_captured_stdout = _stdout_buffer.getvalue()
_captured_stderr = _stderr_buffer.getvalue()

# Restore original streams
sys.stdout = _original_stdout
sys.stderr = _original_stderr
";
                        
                        scope.Exec(getOutputCode);
                        
                        // Extract captured output
                        var stdout = scope.Get("_captured_stdout")?.ToString() ?? "";
                        var stderr = scope.Get("_captured_stderr")?.ToString() ?? "";
                        
                        result.Success = true;
                        result.Output = stdout;
                        result.Error = stderr;
                        
                        // Fire events for real-time updates
                        if (!string.IsNullOrEmpty(stdout))
                        {
                            OutputReceived?.Invoke(this, stdout);
                        }
                        
                        if (!string.IsNullOrEmpty(stderr))
                        {
                            ErrorReceived?.Invoke(this, stderr);
                        }
                    }
                }
                catch (PythonException ex)
                {
                    result.Success = false;
                    result.Output = "";
                    result.Error = ex.Message;
                    
                    ErrorReceived?.Invoke(this, ex.Message);
                    _logger.LogWarning(ex, "Python execution error");
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Output = "";
                    result.Error = $"Runtime error: {ex.Message}";
                    
                    ErrorReceived?.Invoke(this, result.Error);
                    _logger.LogError(ex, "Failed to execute Python code");
                }
                
                return result;
            }
        });
    }

    public async Task<bool> TestPythonEnvironmentAsync()
    {
        _logger.LogInformation("Testing Python environment...");
        
        var tests = new Dictionary<string, string>
        {
            ["Basic Python"] = "print('Hello from Python!')",
            ["Math operations"] = "print(f'2 + 2 = {2 + 2}')",
            ["Import sys"] = "import sys; print(f'Python version: {sys.version}')",
            ["NumPy test"] = @"
try:
    import numpy as np
    print(f'NumPy version: {np.__version__}')
    arr = np.array([1, 2, 3])
    print(f'NumPy array: {arr}')
except ImportError as e:
    print(f'NumPy not available: {e}')
",
            ["PyTorch test"] = @"
try:
    import torch
    print(f'PyTorch version: {torch.__version__}')
    tensor = torch.tensor([1.0, 2.0, 3.0])
    print(f'PyTorch tensor: {tensor}')
    print(f'CUDA available: {torch.cuda.is_available()}')
except ImportError as e:
    print(f'PyTorch not available: {e}')
"
        };

        var allPassed = true;
        foreach (var test in tests)
        {
            _logger.LogInformation($"Running test: {test.Key}");
            var result = await ExecutePythonCodeAsync(test.Value);
            
            if (result.Success)
            {
                _logger.LogInformation($"✓ {test.Key} passed");
                if (!string.IsNullOrEmpty(result.Output))
                {
                    _logger.LogInformation($"Output: {result.Output.Trim()}");
                }
            }
            else
            {
                _logger.LogWarning($"✗ {test.Key} failed: {result.Error}");
                allPassed = false;
            }
        }

        return allPassed;
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        try
        {
            if (_isInitialized)
            {
                lock (_lockObject)
                {
                    if (_isInitialized)
                    {
                        PythonEngine.Shutdown();
                        _isInitialized = false;
                        _logger.LogInformation("Python runtime shut down");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during Python runtime shutdown");
        }
        finally
        {
            _disposed = true;
        }
    }
}

public class PythonExecutionResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = "";
    public string Error { get; set; } = "";
    public TimeSpan ExecutionTime { get; set; }
}