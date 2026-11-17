using System.Collections.Immutable;

namespace Estiblazor.Domain;

public sealed record RoomTemplate(string Name, string Description, ImmutableArray<Phase> Phases);

/// <summary>Built-in starting points. Saved rooms contain independent configurations, not template references.</summary>
public static class RoomTemplates
{
    public static ImmutableArray<RoomTemplate> All { get; } =
    [
        new(
            "Planning poker",
            "Estimate effort and complexity in separate phases.",
            [
                new("Effort", ["0.5", "1", "2", "3", "4", "5", "8", "13", "20", "∞"]),
                new("Complexity", ["S", "M", "L", "∞"]),
            ]
        ),
        new(
            "Fibonacci",
            "Estimate effort with an increasing number sequence. Choose ? when more information is needed.",
            [new("Effort", ["0", "1", "2", "3", "5", "8", "13", "21", "34", "55", "89", "?"])]
        ),
        new(
            "Modified Fibonacci",
            "Use rounded larger estimates, plus unsure and unlimited cards.",
            [
                new(
                    "Effort",
                    ["0", "0.5", "1", "2", "3", "5", "8", "13", "20", "40", "100", "?", "∞"]
                ),
            ]
        ),
        new(
            "T-shirt sizes",
            "Compare relative size using familiar clothing sizes. Choose ? when unsure.",
            [new("Size", ["XXS", "XS", "S", "M", "L", "XL", "XXL", "?"])]
        ),
        new(
            "Powers of two",
            "Estimate effort with doubling steps. Choose ? when more information is needed.",
            [new("Effort", ["0", "1", "2", "4", "8", "16", "32", "64", "?"])]
        ),
        new(
            "Score",
            "Choose a whole-number score from one to ten.",
            [new("Score", ["1", "2", "3", "4", "5", "6", "7", "8", "9", "10"])]
        ),
        new(
            "Like / dislike",
            "Gather a simple positive or negative reaction.",
            [new("Opinion", ["👍", "👎"])]
        ),
        new(
            "Confidence",
            "Discuss how confident the team feels about a proposal or plan.",
            [new("Confidence", ["Very low", "Low", "Medium", "High", "Very high"])]
        ),
        new(
            "Risk",
            "Compare perceived risk, including an explicit unknown option.",
            [new("Risk", ["Very low", "Low", "Medium", "High", "Very high", "Unknown"])]
        ),
        new(
            "Priority",
            "Compare perceived priority before discussing what matters most.",
            [new("Priority", ["Low", "Medium", "High", "Critical"])]
        ),
        new(
            "Agreement",
            "Collect a five-point response from strongly disagree to strongly agree.",
            [
                new(
                    "Agreement",
                    ["Strongly disagree", "Disagree", "Neutral", "Agree", "Strongly agree"]
                ),
            ]
        ),
        new(
            "Effort and risk",
            "Estimate effort and risk independently in a two-phase round.",
            [
                new("Effort", ["0", "1", "2", "3", "5", "8", "13", "21", "?"]),
                new("Risk", ["Low", "Medium", "High", "Unknown"]),
            ]
        ),
    ];

    public static Configuration CreateConfiguration(string name)
    {
        var template = All.FirstOrDefault(t => t.Name == name)
            ?? throw new RoomException("Choose a known room template.");
        // Sharing immutable phase values is safe; editors create their own mutable draft lists.
        return new Configuration("New room", template.Phases);
    }
}
