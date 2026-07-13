using System.Reflection;

namespace Schreadt_Engine.Asset;

public abstract class AssetLibrary
{
    private static AssetLibrary? CreateAssetLibrary(string classQualifier)
    {
        var t = Type.GetType(classQualifier);
        
        if (t is null)
        {
            var all = AppDomain.CurrentDomain.GetAssemblies().GetEnumerator();
            while (all.MoveNext())
            {
                var t2 = (Assembly)all.Current;
                t = t2.GetType(classQualifier, false, true);
                if (t is not null)
                {
                    break;
                }
            }
        }
        
        if (t is null) throw new Exception($"Could not find asset library type '{classQualifier}'!");
        
        return Activator.CreateInstance(t) as AssetLibrary;
    }

    public static AssetLibrary? LibraryFromManifest(AssetLibraryManifest manifest)
    {
        var library = CreateAssetLibrary(manifest.Type);
        
        return library;
    }

    public abstract void Load();
}