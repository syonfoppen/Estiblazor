using System.Collections.Immutable;
using Estiblazor.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Estiblazor.UI.Components.Shared;

public partial class RoomEditor
{
    [Parameter, EditorRequired]
    public Configuration Initial { get; set; } = Configuration.Template("Planning poker");
    private Dictionary<string, string> errors = [];
    private ElementReference errorSummary;
    private bool focusErrors;
    private string templateDescription =
        "Templates are copied into your draft. You can change any phase or option without changing the original template.";
    private string announcement = "";
    private string name = "";
    private List<DraftPhase> phases = [];

    protected override void OnInitialized() => Load(Initial);

    private void Load(Configuration configuration)
    {
        errors.Clear();
        name = configuration.Name;
        phases = configuration
            .Phases.Select(p => new DraftPhase { Name = p.Name, Options = p.Options.ToList() })
            .ToList();
    }

    private void UseTemplate(ChangeEventArgs e)
    {
        var template = RoomTemplates.All.FirstOrDefault(t => t.Name == e.Value?.ToString());
        if (template == null)
            return;
        var oldName = name;
        Load(Configuration.Template(template.Name));
        name = oldName;
        templateDescription =
            template.Description + " This replaces the current draft deck. Your room name is kept.";
        announcement =
            $"{template.Name} loaded with {phases.Count} phases. Edit the copied cards below.";
    }

    private void Move(int index, int direction)
    {
        errors.Clear();
        var phase = phases[index];
        phases.RemoveAt(index);
        phases.Insert(index + direction, phase);
        announcement = $"{phase.Name} moved to phase {index + direction + 1}.";
    }

    private void MoveOption(DraftPhase phase, int index, int direction)
    {
        errors.Clear();
        var value = phase.Options[index];
        phase.Options.RemoveAt(index);
        phase.Options.Insert(index + direction, value);
        announcement = $"Option moved to position {index + direction + 1}.";
    }

    private void RemovePhase(int index)
    {
        errors.Clear();
        phases.RemoveAt(index);
        announcement = "Phase removed.";
    }

    private void RemoveOption(DraftPhase phase, int index)
    {
        errors.Clear();
        phase.Options.RemoveAt(index);
        announcement = "Option removed.";
    }

    private async Task FocusField(string id) => await JS.InvokeVoidAsync("estiblazor.focus", id);

    private void AddPhase()
    {
        errors.Clear();
        phases.Add(new DraftPhase { Name = "New phase", Options = ["Yes", "No"] });
        announcement = "Phase added.";
    }

    private void AddOption(DraftPhase phase)
    {
        errors.Clear();
        phase.Options.Add("");
        announcement = "Option added. Enter its label.";
    }

    private string Invalid(string id) => errors.ContainsKey(id) ? "true" : "false";

    private string? Description(string id, string? hint = null) =>
        errors.ContainsKey(id) ? $"{hint} {id}-error".Trim() : hint;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (focusErrors)
        {
            focusErrors = false;
            await errorSummary.FocusAsync();
        }
    }

    public Configuration GetConfiguration()
    {
        errors.Clear();
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            errors["room-name"] = "Room name: enter 1–100 characters.";
        if (phases.Count is < 1 or > 10)
            errors["add-phase"] = "Include 1–10 phases.";
        for (var i = 0; i < phases.Count; i++)
        {
            var phase = phases[i];
            if (string.IsNullOrWhiteSpace(phase.Name) || phase.Name.Trim().Length > 50)
                errors[$"phase-{i}"] = $"Phase {i + 1}: enter a name of 1–50 characters.";
            if (phase.Options.Count is < 2 or > 30)
                errors[$"add-option-{i}"] = $"Phase {i + 1}: include 2–30 options.";
            for (var j = 0; j < phase.Options.Count; j++)
            {
                var value = phase.Options[j].Trim();
                if (value.Length is < 1 or > 30)
                    errors[$"option-{i}-{j}"] =
                        $"Phase {i + 1}, option {j + 1}: enter 1–30 characters.";
                else if (
                    phase.Options.Count(o =>
                        string.Equals(o.Trim(), value, StringComparison.OrdinalIgnoreCase)
                    ) > 1
                )
                    errors[$"option-{i}-{j}"] =
                        $"Phase {i + 1}, option {j + 1}: use a unique label.";
            }
        }
        if (errors.Count > 0)
        {
            focusErrors = true;
            StateHasChanged();
            throw new RoomException("Check the room settings listed in the error summary.");
        }
        // This is input guidance; the application still validates independently on the server.
        return new(
            name,
            phases.Select(p => new Phase(p.Name, p.Options.ToImmutableArray())).ToImmutableArray()
        );
    }

    private sealed class DraftPhase
    {
        public string Name { get; set; } = "";
        public List<string> Options { get; set; } = [];
    }
}
