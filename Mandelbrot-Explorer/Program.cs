using Mandelbrot_Explorer.Logic;

namespace Mandelbrot_Explorer;

public static class Program
{
    public static void Main(string[] args)
    {
        Schreadt_Engine.EntryPoint.Run(new MandelbrotGameLogic(), args);
    }
}
