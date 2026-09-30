using Nexus.Core;
namespace Nexus.Optimization;

public sealed class ProfileManager(IProfileStore store)
{
    public void Apply(string name)
    {
        Profiles.Get(name);
        store.Apply(name);
    }
    public Profile Current => Profiles.Get(store.Current);
}
