using System;
using System.Runtime.CompilerServices;

namespace ApiView
{
    class Program
    {
        static String _help = "Usage: .\\GenAPI-Roslyn.exe mylibrary.dll";
        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "--help")
            {
                Console.WriteLine(_help);
                return;
            }

            try
            {
                /// path to dll
                var dllPath = args[0];
                var assemblySymbol = CompilationFactory.GetCompilation(dllPath);
                var renderer = new CodeFileRenderer();
                var codeNode = new CodeFileBuilder().Build(assemblySymbol);
                //Console.WriteLine(codeNode.ToString());
                var codeLines = renderer.Render(codeNode);
                /// output ref package to console
                foreach (var cl in codeLines)
                {
                    Console.WriteLine(cl.DisplayString);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
