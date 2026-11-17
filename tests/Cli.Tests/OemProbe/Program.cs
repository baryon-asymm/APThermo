using System.Text;

namespace APThermo.Cli.Tests.OemProbe;

/// <summary>Calls the real CLI with a controlled initial redirected-output encoding.</summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (!Console.IsOutputRedirected)
        {
            throw new InvalidOperationException("the encoding probe requires redirected standard output");
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var writer = new StreamWriter(Console.OpenStandardOutput(), Encoding.GetEncoding(866), leaveOpen: true) { AutoFlush = true };
        Console.SetOut(writer);
        Console.Error.WriteLine($"initialWriterCodePage={Console.Out.Encoding.CodePage}");
        return APThermo.Cli.Program.Main(args);
    }
}
