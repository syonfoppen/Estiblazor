using Estiblazor.Domain;

namespace Estiblazor.UI.Services;
public static class PublicContent
{
    public sealed record QuestionAnswer(string Question, string Answer);
    public static readonly QuestionAnswer[] Faqs = [
        new("What is planning poker?", "Planning poker is a collaborative way for teams to estimate work. Each participant chooses a card independently, then the team reveals the votes and discusses differences."),
        new("How do I start a planning poker session?", "Create a room, choose a template or customize the phases and voting options, and share the invitation link. Participants enter a display name and select a card in each phase. The owner reveals the votes and starts the next round."),
        new("Do I need an account to use Estiblazor?", "No. Estiblazor is free to use without accounts. A browser cookie keeps your identity and your room list. A valid private management link restores owner access in another browser. Ownership transfer invalidates older management links."),
        new("Who can see my votes?", "Before reveal, only you can see your vote values. Other participants see whether you have voted. After the owner reveals the round, everyone in the room can see the votes."),
        new("Can I create custom voting cards?", $"Yes. Choose from {RoomTemplates.All.Length} built-in templates or create your own deck. A room supports 1–10 phases, each with 2–30 unique text options. You can edit and reorder phases and options. Owner configuration changes take effect in the next round."),
        new("Can I watch without voting?", "Yes. Join as an observer or change your participation mode. Observers can follow the session and read revealed results but cannot vote and do not count as missing voters. Changing to observer while you have open votes requires confirmation and withdraws those votes."),
        new("Can I reuse a room's custom deck?", "Yes. Duplicate the room from its page or your home-page room list. The current deck opens as an editable creation draft. The new room has its own code, owner and recovery link, with no participants, votes or history."),
        new("How can I share room ownership?", "An owner can add a joined participant as a co-owner or transfer ownership. Transfer makes the selected participant the only owner and invalidates all old management links. The new owner should create and save a fresh private recovery link."),
        new("Are planning poker rounds saved?", "Starting a new round archives the previous revealed round with its original cards, names and votes. Discarding an unrevealed round requires confirmation and does not create history.")
    ];
}
