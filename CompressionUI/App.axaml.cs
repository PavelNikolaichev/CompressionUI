using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CompressionUI.Services;
using CompressionUI.Services.Execution;
using CompressionUI.ViewModels;
using CompressionUI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace CompressionUI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Set up logging
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        // Set up dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = serviceProvider.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddSerilog());
        
        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<PythonConsoleViewModel>();
        
        // Python service
        services.AddSingleton<PythonService>();
        
        // Nodes registry
        services.AddSingleton<INodeRegistry, NodeRegistry>();
        services.AddSingleton<NodeFactory>();
        services.AddSingleton<NodeSerializationService>();
        
        // Execution services
        services.AddSingleton<NodeDependencyResolver>();
        services.AddSingleton<INodeExecutionStrategy, SequentialExecutionStrategy>();
        services.AddSingleton<NodeExecutionService>();
        
        // Node Types
        
        services.AddTransient<Models.Nodes.Utility.DebugPrintNode>();
        services.AddTransient<Models.Nodes.Utility.VariableNode>();
        services.AddTransient<Models.Nodes.Utility.MemoryCleanupNode>();
        services.AddTransient<Models.Nodes.Data.TextDataLoaderNode>();
        services.AddTransient<Models.Nodes.Data.ImageDataLoaderNode>();
        services.AddTransient<Models.Nodes.Data.DatasetNode>();
        services.AddTransient<Models.Nodes.Math.ArithmeticNode>();
        services.AddTransient<Models.Nodes.Math.TensorOperationNode>();
        services.AddTransient<Models.Nodes.Model.PyTorchModelNode>();
        services.AddTransient<Models.Nodes.Model.TrainingNode>();
        services.AddTransient<Models.Nodes.Model.InferenceNode>();
    }
}