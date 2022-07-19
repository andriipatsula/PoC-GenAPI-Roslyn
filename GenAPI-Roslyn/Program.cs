using System;

namespace ApiView
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                /// path to dll
                var dllPath = args[0];
                var assemblySymbol = CompilationFactory.GetCompilation(dllPath);
                var renderer = new CodeFileRenderer();
                var codeNode = new CodeFileBuilder().Build(assemblySymbol, null);
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
