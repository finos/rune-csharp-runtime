namespace Finos.Rune.Runtime;

/// <summary>
/// Base class of all generated builders. Builders store child builders for
/// model-object-typed attributes and raw values for basic and enum attributes; list
/// attributes are pre-initialized to empty lists.
/// </summary>
public abstract class RosettaModelObjectBuilder : IRosettaModelObjectBuilder
{
    public abstract RosettaModelObject Build();

    public abstract RosettaModelObjectBuilder Prune();

    public abstract bool HasData();

    IRosettaModelObject IRosettaModelObjectBuilder.Build() => Build();

    IRosettaModelObjectBuilder IRosettaModelObjectBuilder.Prune() => Prune();

    /// <summary>
    /// Returns the builder at <paramref name="index"/> in <paramref name="list"/>,
    /// creating it with <paramref name="factory"/> as needed. An index of -1 or
    /// int.MaxValue appends; an index past the end pads the gap with nulls (dropped by
    /// Prune()/Build()). Mirrors RosettaModelObjectBuilder.getIndex in the Java runtime.
    /// </summary>
    protected static T GetIndex<T>(List<T> list, int index, Func<T> factory)
    {
        if (index == -1 || index == int.MaxValue)
        {
            var appended = factory();
            list.Add(appended);
            return appended;
        }
        if (index < list.Count)
        {
            T existing = list[index];
            if (existing == null)
            {
                existing = factory();
                list[index] = existing;
            }
            return existing;
        }
        while (list.Count < index)
        {
            list.Add(default!);
        }
        var created = factory();
        list.Add(created);
        return created;
    }

    /// <summary>Prunes each entry of a list of child builders, dropping entries that end
    /// up empty. Mirrors the generated Java prune() list handling.</summary>
    protected static void PruneList<T>(List<T> list) where T : RosettaModelObjectBuilder
    {
        list.RemoveAll(item => item == null);
        foreach (T item in list)
        {
            item.Prune();
        }
        list.RemoveAll(item => !item.HasData());
    }
}
