using System.Reflection;
using Schreadt_Engine.Component.Logic;

namespace Schreadt_Engine.Component;

public class Scene : GameObject
{
    public readonly int SceneId;
    public readonly SceneLogic SceneLogic;

    private static SceneLogic CreateSceneLogic(Scene scene)
    {
        var className = $"Scene{scene.SceneId}";
        Type? t = null;

        var all = AppDomain.CurrentDomain.GetAssemblies().GetEnumerator();
        while (all.MoveNext())
        {
            var t2 = (Assembly)all.Current;
            if (t2.FullName?.Contains("System.Private.CoreLib") == true) continue;

            foreach (var exportedType in t2.GetExportedTypes())
            {
                if (!exportedType.IsSubclassOf(typeof(SceneLogic))) continue;
                if (!exportedType.IsPublic) continue;
                if (exportedType.FullName?.EndsWith(className) != true) continue;
                t = exportedType;
            }
        }

        if (t is null) throw new Exception("SceneLogic not found");

        return Activator.CreateInstance(t, scene) as SceneLogic
               ?? throw new Exception($"Could not create scene logic '{t.FullName}'.");
    }

    public Scene(int sceneId)
    {
        SceneId = sceneId;
        SceneLogic = CreateSceneLogic(this);
    }

    protected override void OnInit()
    {
        SceneLogic.Init();
    }

    protected override void OnUpdate(double dt)
    {
        SceneLogic.Update(dt);
    }
}
