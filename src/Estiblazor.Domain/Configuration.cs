using System.Collections.Immutable;

namespace Estiblazor.Domain;

public sealed record Phase(string Name, ImmutableArray<string> Options);

public sealed record Configuration(string Name, ImmutableArray<Phase> Phases)
{
    public Configuration Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > 100)
            throw new RoomException("Room name must contain 1–100 characters.");
        if (Phases.IsDefault || Phases.Length is < 1 or > 10)
            throw new RoomException("Use 1–10 phases.");
        foreach (var phase in Phases)
        {
            if (string.IsNullOrWhiteSpace(phase.Name) || phase.Name.Trim().Length > 50)
                throw new RoomException("Phase names must contain 1–50 characters.");
            if (
                phase.Options.IsDefault
                || phase.Options.Length is < 2 or > 30
                || phase.Options.Any(x => string.IsNullOrWhiteSpace(x) || x.Trim().Length > 30)
            )
                throw new RoomException("Each phase needs 2–30 options of 1–30 characters.");
            if (
                phase
                    .Options.Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count() != phase.Options.Length
            )
                throw new RoomException("Options within a phase must be unique.");
        }
        return this with
        {
            Name = Name.Trim(),
            Phases = Phases
                .Select(p =>
                    p with
                    {
                        Name = p.Name.Trim(),
                        Options = p.Options.Select(x => x.Trim()).ToImmutableArray(),
                    }
                )
                .ToImmutableArray(),
        };
    }

    public static Configuration Template(string name) => RoomTemplates.CreateConfiguration(name);
}
