using Accessibility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Natsurainko.Wpf.UI.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace CSharpScriptExecutor;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();


        if (App.Args.Length> 0)
            this.DataContext = new ViewModel
            {
                Folder = App.Args.First()
            };
    }

    protected override void OnContentRendered(EventArgs e)
    {
        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (sender, e) => SystemCommands.CloseWindow(this)));

        int GWL_STYLE = -16;
        int WS_SYSMENU = 0x00080000;
        var hwnd = new WindowInteropHelper(this).Handle;

        SetWindowLongPtr(hwnd, GWL_STYLE, GetWindowLongPtr(hwnd, GWL_STYLE) & ~WS_SYSMENU);

        base.OnContentRendered(e);

        this.SetMicaTheme();

        ContentGrid.MouseLeftButtonDown += (object sender, MouseButtonEventArgs e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();

            e.Handled = true;
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLongPtr(IntPtr hWnd, int nIndex, int dwNewLong);

}

public partial class ViewModel : ObservableObject
{
    public ViewModel()
    {
        var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();

        MetadataReferences = new DirectoryInfo(runtimeDirectory).EnumerateFiles()
            .Where(x => x.Extension.Equals(".dll") && x.Name.StartsWith("System.") && !x.Name.Contains("Native"))
            .Select(x => MetadataReference.CreateFromFile(x.FullName))
            .ToList();

        MetadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "mscorlib.dll")));

        CSharpCompilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithOptimizationLevel(OptimizationLevel.Debug)
            .WithPlatform(Platform.AnyCpu);
    }

    private List<PortableExecutableReference> MetadataReferences { get; }

    private CSharpCompilationOptions CSharpCompilationOptions { get; }

    [ObservableProperty]
    private string folder;

    [ObservableProperty]
    private string code;

    [ObservableProperty]
    private string error;

    [ObservableProperty]
    private Visibility errorVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private bool openConsole = true;

    [RelayCommand]
    public Task Excute() => Task.Run(() =>
    {
        string compileCode =
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.IO;
            using System.Text;
            using System.Threading.Tasks;
            
            namespace CSharpScriptExecutor.DynamicCodeGenerate
            {
                public class CompiledCode
                {
                    public void ToExcute()
                    {
                        {$Folder}
                        {$Code}
                    }
                }
            }
            
            """
            .Replace("{$Code}", Code)
            .Replace("{$Folder}", $"DirectoryInfo @folder = new DirectoryInfo(\"{App.Args.First().Replace("\\", "\\\\")}\");");

        var syntaxTree = CSharpSyntaxTree.ParseText(compileCode);
        var cSharpCompilation = CSharpCompilation.Create(
            "CSharpScriptExecutor.DynamicCodeGenerate", 
            new[] { syntaxTree }, 
            MetadataReferences, 
            CSharpCompilationOptions);

        using var memoryStream = new MemoryStream();
        var emitResult = cSharpCompilation.Emit(memoryStream);

        if (emitResult.Success)
        {
            using var stream = new MemoryStream(memoryStream.ToArray());
            var loadContext = new AssemblyLoadContext("loadContext", true);
            var assembly = loadContext.LoadFromStream(stream);

            var instance = assembly.CreateInstance("CSharpScriptExecutor.DynamicCodeGenerate.CompiledCode");

            if (OpenConsole)
            {
                AllocConsole();

                using var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                using var stdin = new StreamReader(Console.OpenStandardInput());
                using var stderr = new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };

                Console.SetIn(stdin);
                Console.SetOut(stdout);
                Console.SetError(stderr);

                instance.GetType().GetMethod("ToExcute").Invoke(instance, null);

                FreeConsole();
            }
            else instance.GetType().GetMethod("ToExcute").Invoke(instance, null);

            loadContext.Unload();

            GC.Collect();

            ErrorVisibility = Visibility.Collapsed;
        }
        else
        {
            Error = string.Join("\r\n", emitResult.Diagnostics.Select(x => $"{x.Id} {x.GetMessage()}"));
            ErrorVisibility = Visibility.Visible;
        }
    });

    [DllImport("Kernel32.dll")]
    public static extern bool AllocConsole();

    [DllImport("Kernel32.dll")]
    public static extern bool FreeConsole();
}
