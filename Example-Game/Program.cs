using Example_Game.Logic;

namespace Example_Game;

public static class Program
{
    public static void Main(string[] args)
    {
        Schreadt_Engine.EntryPoint.Run(new ExampleGameLogic(), args);
    }
}
