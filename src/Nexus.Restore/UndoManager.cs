using Nexus.Core;
namespace Nexus.Restore;
// V1 only reverses application preferences. Future OS actions must capture state before mutation.
public sealed class UndoManager(IProfileStore store)
{
    public bool UndoLast() => store.Undo();
}
