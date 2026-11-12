using Finos.Rune.Runtime.Attributes;

namespace Finos.Rune.Runtime.Tests.Xml.Model;

[RuneDataType("Animal", "com.rosetta.test", "0.0.0")]
public abstract class Animal : RosettaModelObject
{
    protected Animal(AnimalBuilder builder)
    {
        Name = builder.Name;
    }

    [RuneAttribute("name")]
    public string? Name { get; }

    public override abstract AnimalBuilder ToBuilder();
}

public abstract class AnimalBuilder : RosettaModelObjectBuilder
{
    public string? Name { get; private set; }

    public AnimalBuilder SetName(string? name)
    {
        Name = name;
        return this;
    }

    public override RosettaModelObjectBuilder Prune() => this;

    public override bool HasData() => Name != null;
}

[RuneDataType("Goat", "com.rosetta.test", "0.0.0")]
public sealed class Goat : Animal
{
    private Goat(Builder builder)
        : base(builder)
    {
    }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder() => (Builder)new Builder().SetName(Name);

    public sealed class Builder : AnimalBuilder
    {
        public override Goat Build() => new Goat(this);
    }
}

[RuneDataType("Cow", "com.rosetta.test", "0.0.0")]
public sealed class Cow : Animal
{
    private Cow(Builder builder)
        : base(builder)
    {
    }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder() => (Builder)new Builder().SetName(Name);

    public sealed class Builder : AnimalBuilder
    {
        public override Cow Build() => new Cow(this);
    }
}

[RuneDataType("Shark", "com.rosetta.test", "0.0.0")]
public sealed class Shark : Animal
{
    private Shark(Builder builder)
        : base(builder)
    {
    }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder() => (Builder)new Builder().SetName(Name);

    public sealed class Builder : AnimalBuilder
    {
        public override Shark Build() => new Shark(this);
    }
}

[RuneDataType("Salmon", "com.rosetta.test", "0.0.0")]
public sealed class Salmon : Animal
{
    private Salmon(Builder builder)
        : base(builder)
    {
    }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder() => (Builder)new Builder().SetName(Name);

    public sealed class Builder : AnimalBuilder
    {
        public override Salmon Build() => new Salmon(this);
    }
}

/// <summary>An extension-namespace Llama, registered under <c>urn:my.extension/llama</c>.</summary>
[RuneDataType("Llama", "com.rosetta.extension.test", "0.0.0")]
public sealed class ExtensionLlama : Animal
{
    private ExtensionLlama(Builder builder)
        : base(builder)
    {
        Fluffiness = builder.Fluffiness;
    }

    [RuneAttribute("fluffiness")]
    public int? Fluffiness { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder();
        builder.SetName(Name);
        builder.SetFluffiness(Fluffiness);
        return builder;
    }

    public sealed class Builder : AnimalBuilder
    {
        public int? Fluffiness { get; private set; }

        public Builder SetFluffiness(int? value)
        {
            Fluffiness = value;
            return this;
        }

        public override ExtensionLlama Build() => new ExtensionLlama(this);

        public override bool HasData() => base.HasData() || Fluffiness != null;
    }
}

/// <summary>The base-namespace Llama, registered under <c>urn:my.schema/llama</c>.</summary>
[RuneDataType("Llama", "com.rosetta.test", "0.0.0")]
public sealed class Llama : Animal
{
    private Llama(Builder builder)
        : base(builder)
    {
        Fluffiness = builder.Fluffiness;
    }

    [RuneAttribute("fluffiness")]
    public int? Fluffiness { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder();
        builder.SetName(Name);
        builder.SetFluffiness(Fluffiness);
        return builder;
    }

    public sealed class Builder : AnimalBuilder
    {
        public int? Fluffiness { get; private set; }

        public Builder SetFluffiness(int? value)
        {
            Fluffiness = value;
            return this;
        }

        public override Llama Build() => new Llama(this);

        public override bool HasData() => base.HasData() || Fluffiness != null;
    }
}

[RuneDataType("AnimalContainer", "com.rosetta.test", "0.0.0")]
public sealed class AnimalContainer : RosettaModelObject
{
    private AnimalContainer(Builder builder)
    {
        Animal = (Animal?)builder.Animal?.Build();
    }

    [RuneAttribute("animal")]
    public Animal? Animal { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder() => new Builder().SetAnimal(Animal?.ToBuilder());

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public AnimalBuilder? Animal { get; private set; }

        public Builder SetAnimal(AnimalBuilder? animal)
        {
            Animal = animal;
            return this;
        }

        public override AnimalContainer Build() => new AnimalContainer(this);

        public override Builder Prune()
        {
            if (Animal != null && !Animal.Prune().HasData())
            {
                Animal = null;
            }
            return this;
        }

        public override bool HasData() => Animal?.HasData() == true;
    }
}

[RuneDataType("Zoo", "com.rosetta.test", "0.0.0")]
public sealed class Zoo : RosettaModelObject
{
    private Zoo(Builder builder)
    {
        Animal = builder.Animal.Count == 0 ? null : builder.Animal.Select(a => (Animal)a.Build()).ToArray();
    }

    [RuneAttribute("animal")]
    public IReadOnlyList<Animal>? Animal { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder();
        foreach (Animal animal in Animal ?? Array.Empty<Animal>())
        {
            builder.AddAnimal(animal.ToBuilder());
        }
        return builder;
    }

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public List<AnimalBuilder> Animal { get; } = new();

        public Builder AddAnimal(AnimalBuilder animal)
        {
            Animal.Add(animal);
            return this;
        }

        public override Zoo Build() => new Zoo(this);

        public override Builder Prune()
        {
            Animal.RemoveAll(a => a == null);
            Animal.RemoveAll(a => !a.HasData());
            return this;
        }

        public override bool HasData() => Animal.Count > 0;
    }
}
