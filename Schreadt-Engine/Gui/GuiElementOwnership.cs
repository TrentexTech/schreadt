using System.Runtime.CompilerServices;

namespace Schreadt_Engine.Gui;

/// <summary>
/// Enforces a single immediate collection owner for every GUI element. Moving an
/// element is intentionally an explicit remove-then-add operation.
/// </summary>
internal static class GuiElementOwnership
{
    private static readonly ConditionalWeakTable<IGuiElement, Ownership> Owners = new();

    internal static void Claim(IGuiElement element, object owner, string ownerRole)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerRole);

        var requestedOwner = DescribeOwner(owner, ownerRole);
        lock (Owners)
        {
            if (Owners.TryGetValue(element, out var current))
            {
                throw new InvalidOperationException(
                    $"GUI element '{element.GetType().Name}' cannot be added to {requestedOwner} because " +
                    $"it is already owned by {current.Description}. Remove it from {current.Description} " +
                    $"before adding it to {requestedOwner}.");
            }

            Owners.Add(element, new Ownership(owner, requestedOwner));
        }
    }

    internal static void Release(IGuiElement element, object owner)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(owner);

        lock (Owners)
        {
            if (!Owners.TryGetValue(element, out var current)) return;
            if (!ReferenceEquals(current.Owner, owner))
            {
                throw new InvalidOperationException(
                    $"GUI element '{element.GetType().Name}' is owned by {current.Description} and cannot " +
                    $"be released by {DescribeOwner(owner, "GUI owner")}.");
            }

            Owners.Remove(element);
        }
    }

    private static string DescribeOwner(object owner, string role) =>
        $"{role} [{owner.GetType().Name}#{RuntimeHelpers.GetHashCode(owner):X8}]";

    private sealed record Ownership(object Owner, string Description);
}
